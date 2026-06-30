# Stage 弱引用资源绑定组件设计计划书

为了彻底解耦预制件（Prefab）对具体美术资源（如图片、音频、动画控制器）的强物理引用，避免首包过大和资源冗余绑定，我们设计了一套**弱引用资源绑定组件（Stage Resource Binders）**。

本方案包含三个核心绑定脚本：`StageTexture`、`StageAudio` 和 `StageAnimatorController`，用于分别管理 `SpriteRenderer`、`AudioSource` 与 `Animator` 的资源绑定。

---

## 1. 命名与冲突规避

由于 C# 和 Unity 自身存在 `UnityEngine.Texture` 和 `UnityEngine.Audio` 等重名基础类，为避免命名空间冲突与代码歧义，脚本类名与文件名定义如下：

| 职责类型 | 脚本类名 | 文件路径 | 管理的 Unity 组件 | 数据结构类 |
| :--- | :--- | :--- | :--- | :--- |
| **纹理/图片** | `StageTexture` | `Runtime/ResourcesSystem/StageTexture.cs` | `SpriteRenderer` | `StageTextureData` |
| **音频** | `StageAudio` | `Runtime/ResourcesSystem/StageAudio.cs` | `AudioSource` | `StageAudioData` |
| **动画控制器** | `StageAnimatorController` | `Runtime/ResourcesSystem/StageAnimatorController.cs` | `Animator` | `StageAnimatorControllerData` |

---

## 2. 数据结构设计 (ComponentData 扩展)

为了对接已有的关卡序列化与多态反序列化系统（`LevelConfig` 和 `LevelObjectData.components`），新增以下三个数据类，均继承自 `ComponentData`：

```csharp
using System;
using UnityEngine;

// 1. 纹理数据
[Serializable]
public class StageTextureData : ComponentData
{
    [ResourceKey(typeof(Texture2D))]
    public string textureKey;
}

// 2. 音频数据
[Serializable]
public class StageAudioData : ComponentData
{
    [ResourceKey(typeof(AudioClip))]
    public string audioKey;
}

// 3. 动画控制器数据
[Serializable]
public class StageAnimatorControllerData : ComponentData
{
    [ResourceKey(typeof(RuntimeAnimatorController))]
    public string animatorControllerKey;
}
```

---

## 3. 弱引用绑定组件核心逻辑

三个脚本均继承自 `MonoBehaviour` 和 `ILevelComponent`。以 `StageTexture` 为例，核心设计如下：

### 3.1 核心代码骨架

```csharp
using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways] // 支持在编辑器模式下运行以实现免热更预览
[RequireComponent(typeof(SpriteRenderer))]
public class StageTexture : MonoBehaviour, ILevelComponent
{
    [ResourceKey(typeof(Texture2D))]
    [Tooltip("弱引用纹理 Key")]
    public string textureKey;

    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (Application.isPlaying)
        {
            // 运行时：从 ResourceManager 获取已预加载的资源并应用
            ApplyRuntimeResource();
        }
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            // 编辑器下：实例化或启用时，加载资源用于预览
            UpdateEditorPreview();
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying)
        {
            // 编辑器下：销毁或禁用时置空，防止强引用驻留
            ClearEditorPreview();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            // 编辑器下：Inspector 字段修改时实时刷新预览
            UpdateEditorPreview();
        }
    }

    private void ApplyRuntimeResource()
    {
        if (string.IsNullOrEmpty(textureKey)) return;

        // 从运行时唯一的 ResourceManager 拿到缓存的 Texture2D
        Texture2D tex = ResourceManager.Instance.GetTexture(textureKey);
        if (tex != null && _spriteRenderer != null)
        {
            // 将 Texture2D 动态创建为 Sprite 并赋给组件
            _spriteRenderer.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }

    #if UNITY_EDITOR
    private void UpdateEditorPreview()
    {
        if (_spriteRenderer == null) _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer == null || string.IsNullOrEmpty(textureKey))
        {
            ClearEditorPreview();
            return;
        }

        // 编辑器下，通过加载 TextureRegistry 的硬引用资源进行同步预览（免去运行和打包）
        string[] guids = AssetDatabase.FindAssets("t:TextureRegistry");
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var registry = AssetDatabase.LoadAssetAtPath<TextureRegistry>(path);
            if (registry != null)
            {
                Texture2D texAsset = registry.GetAsset(textureKey);
                if (texAsset != null)
                {
                    // 完美还原：从相同路径下直接加载真正的 Sprite 资产，保证 Pivot 和 Border 完美应用
                    string assetPath = AssetDatabase.GetAssetPath(texAsset);
                    Sprite spriteAsset = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                    _spriteRenderer.sprite = spriteAsset;
                    return;
                }
            }
        }
        _spriteRenderer.sprite = null;
    }

    private void ClearEditorPreview()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = null;
        }
    }
    #endif

    #region ILevelComponent 实现

    public Type DataType => typeof(StageTextureData);

    public ComponentData ExtractData()
    {
        return new StageTextureData { textureKey = this.textureKey };
    }

    public void ApplyData(ComponentData data)
    {
        if (data is StageTextureData texData)
        {
            this.textureKey = texData.textureKey;
            
            if (Application.isPlaying)
                ApplyRuntimeResource();
            #if UNITY_EDITOR
            else
                UpdateEditorPreview();
            #endif
        }
    }

    #endregion
}
```

---

## 4. Inspector 面板交互设计

得益于之前已经实现的 `ResourceKeyAttributeDrawer.cs`，我们无需为每个脚本额外编写复杂的自定义 CustomEditor。

只要在组件的 `string` 字段上加上 `[ResourceKey(typeof(T))]` 特性标记：
1. **拖拽赋值**：在 Inspector 中会直接呈现一个 `ObjectField`（类型限定为对应的 `Sprite/Texture2D`、`AudioClip` 或 `RuntimeAnimatorController`）。开发者可以直接将项目中的资源拖拽进该输入框。Drawer 会自动将其转化为对应的安全 Key（字符串）存入。
2. **点击跳转**：对于已经赋值的资源，点击 Inspector 上的资源图标，会直接跳转（Ping）并在 Project 窗口中高亮选中对应的资源文件。
3. **安全校验**：如果拖拽了没有打入 Addressables 组的资源，或者拖拽了非 Prefab 的 GameObject 资源，Drawer 会弹出二次警告弹窗，确保依赖关系始终健康。

---

## 5. 其余两组件的差异点设计

### 5.1 StageAudio
- **关联组件**：`AudioSource`。
- **关联属性**：`AudioSource.clip`。
- **编辑器获取 Registry**：`guids = AssetDatabase.FindAssets("t:AudioRegistry")`。
- **运行时获取资源**：`ResourceManager.Instance.GetAudio(audioKey)`。

### 5.2 StageAnimatorController
- **关联组件**：`Animator`。
- **关联属性**：`Animator.runtimeAnimatorController`。
- **编辑器获取 Registry**：`guids = AssetDatabase.FindAssets("t:AnimatorControllerRegistry")`。
- **运行时获取资源**：`ResourceManager.Instance.GetAnimatorController(animatorControllerKey)`。

---

## 6. 后续实施步骤

1. **创建数据类**：分别在 `Runtime/StageSystem/Model/` 下创建 `StageTextureData.cs`、`StageAudioData.cs`、`StageAnimatorControllerData.cs`。
2. **创建绑定脚本**：在 `Runtime/ResourcesSystem/` 下创建 `StageTexture.cs`、`StageAudio.cs`、`StageAnimatorController.cs`。
3. **整合测试**：在编辑关卡时，将新脚本挂载到场景中相应的 Prefab 上，在 Inspector 中拖拽配置图片/音频/动画并保存，导出关卡并运行，验证场景中是否能在运行时从 ResourceManager 动态将这些资源实例化恢复。
