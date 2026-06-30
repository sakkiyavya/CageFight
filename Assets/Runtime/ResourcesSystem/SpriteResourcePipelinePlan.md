# Sprite Resource Pipeline Plan

## 背景

`StageTexture` 当前使用 `Texture2D` 作为资源类型，再在运行时通过 `Sprite.Create()` 重建 `Sprite`。这种方式适合整张贴图，但不适合来自多图切片的子图资源，因为运行时重建会丢失原始 `Sprite` 的 `rect`、`pivot`、`border`、`pixelsPerUnit` 等信息。

因此，`StageTexture` 应改为直接引用和加载 `Sprite` 资源。

目标用法：

```csharp
[ResourceKey(typeof(Sprite))]
public string spriteKey;
```

运行时：

```csharp
_spriteRenderer.sprite = ResourceManager.Instance.GetSprite(spriteKey);
```

## 总体方案

新增一条独立的 `Sprite` 资源链路，不复用 `TextureRegistry`：

1. 新增 `SpriteRegistry`
2. 新增 `SpriteRegistryBuilder`
3. 扩展 `LevelConfig.sprites`
4. 扩展 `LevelExporter` / `LevelConfigEditor` 的资源 Key 扫描
5. 扩展 `ResourceManager` 的 Sprite 预下载、加载、缓存和卸载
6. 修改 `StageTexture` 使用 `Sprite` key

这样可以保留多图子图的原始切片信息，并与现有 `[ResourceKey]`、Addressables、Registry、关卡导出流程保持一致。

## 1. 新增 SpriteRegistry

建议路径：

```text
Assets/Runtime/ResourcesSystem/Model/SpriteRegistry.cs
```

建议结构：

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[Serializable]
public class SpriteMapping
{
    public string key;
    public AssetReferenceT<Sprite> spriteReference;

#if UNITY_EDITOR
    public Sprite sprite;
#endif
}

[CreateAssetMenu(fileName = "SpriteRegistry", menuName = "ResourcesSystem/Sprite Registry")]
public class SpriteRegistry : ScriptableObject
{
    public List<SpriteMapping> mappings = new List<SpriteMapping>();

    private Dictionary<string, AssetReferenceT<Sprite>> _dictReference;

#if UNITY_EDITOR
    private Dictionary<string, Sprite> _dictEditor;
#endif

    public void Initialize()
    {
        if (_dictReference != null) return;

        _dictReference = new Dictionary<string, AssetReferenceT<Sprite>>();
        foreach (var mapping in mappings)
        {
            if (mapping != null && !string.IsNullOrEmpty(mapping.key) && !_dictReference.ContainsKey(mapping.key))
            {
                _dictReference.Add(mapping.key, mapping.spriteReference);
            }
        }

#if UNITY_EDITOR
        _dictEditor = new Dictionary<string, Sprite>();
        foreach (var mapping in mappings)
        {
            if (mapping != null && !string.IsNullOrEmpty(mapping.key) && !_dictEditor.ContainsKey(mapping.key))
            {
                _dictEditor.Add(mapping.key, mapping.sprite);
            }
        }
#endif
    }

    public AssetReferenceT<Sprite> GetReference(string key)
    {
        if (_dictReference == null) Initialize();
        return _dictReference.TryGetValue(key, out var reference) ? reference : null;
    }

    public Sprite GetAsset(string key)
    {
#if UNITY_EDITOR
        if (_dictEditor == null) Initialize();
        return _dictEditor.TryGetValue(key, out var asset) ? asset : null;
#else
        Debug.LogWarning("[SpriteRegistry] Do not call GetAsset() at runtime. Use ResourceManager instead.");
        return null;
#endif
    }
}
```

## 2. 新增 SpriteRegistryBuilder

建议路径：

```text
Assets/Editor/Utility/SpriteRegistryBuilder.cs
```

核心要点：

- 扫描 Addressables Groups 中的贴图资源。
- 对每个 Addressable 资源路径调用 `AssetDatabase.LoadAllAssetsAtPath(entry.AssetPath)`。
- 从返回结果中过滤 `Sprite`。
- 使用 `sprite.name` 作为 key。
- 对重复 key 发出 warning 并跳过，避免运行时映射不确定。
- 生成 `Assets/RemoteResource/SpriteRegistry.asset`。

关键扫描逻辑：

```csharp
UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(entry.AssetPath);
foreach (UnityEngine.Object asset in assets)
{
    if (asset is not Sprite sprite) continue;

    if (registeredKeys.Contains(sprite.name))
    {
        Debug.LogWarning($"[SpriteRegistryBuilder] Duplicate sprite key: {sprite.name}, path: {entry.AssetPath}");
        continue;
    }

    registry.mappings.Add(new SpriteMapping
    {
        key = sprite.name,
        spriteReference = new AssetReferenceT<Sprite>(entry.guid),
#if UNITY_EDITOR
        sprite = sprite
#endif
    });

    registeredKeys.Add(sprite.name);
}
```

注意：如果 Addressables 无法通过主资源 GUID 正确加载子 Sprite，后续需要验证 `AssetReferenceT<Sprite>` 对切片子资源的运行时加载行为。若不稳定，可改为保存 Addressables address，或使用 `Addressables.LoadAssetAsync<Sprite>(spriteKey)` 并要求每个 Sprite 子资源有唯一 Address。

## 3. 扩展 LevelConfig

在 `Assets/Runtime/StageSystem/Model/LevelConfig.cs` 增加：

```csharp
[Tooltip("Sprite 资源 Key 清单")]
public List<string> sprites = new List<string>();
```

## 4. 扩展导出和扫描

需要更新：

```text
Assets/Editor/StageSystem/LevelExporter.cs
Assets/Editor/StageSystem/LevelConfigEditor.cs
```

在清空列表时增加：

```csharp
config.sprites.Clear();
```

在资源分类方法中增加：

```csharp
else if (type == typeof(Sprite))
{
    if (!config.sprites.Contains(key)) config.sprites.Add(key);
}
```

统计数量和 Inspector 展示也应加入 `sprites`，方便导出后检查资源清单。

## 5. 扩展 ResourceManager

在 `ResourceManager` 中增加：

```csharp
public SpriteRegistry spriteRegistry;
private Dictionary<string, Sprite> _spriteDict = new Dictionary<string, Sprite>();
```

新增接口：

```csharp
public Sprite GetSprite(string key)
{
    return _spriteDict.TryGetValue(key, out var res) ? res : null;
}
```

加载流程中：

- 初始化 `spriteRegistry`
- 遍历 `level.sprites`
- 使用 `spriteRegistry.GetReference(key)` 获取 Addressable key
- 调用 `Addressables.LoadAssetAsync<Sprite>(addressableKey)`
- 成功后写入 `_spriteDict[key]`
- 卸载时清空 `_spriteDict`

## 6. 修改 StageTexture

建议将 `StageTextureData` 改为：

```csharp
[Serializable]
public class StageTextureData : ComponentData
{
    [ResourceKey(typeof(Sprite))]
    public string spriteKey;
}
```

组件字段：

```csharp
[ResourceKey(typeof(Sprite))]
[Tooltip("Sprite resource key.")]
public string spriteKey;
```

运行时应用：

```csharp
private void ApplyRuntimeResource()
{
    CacheComponent();

    if (_spriteRenderer == null) return;

    if (string.IsNullOrEmpty(spriteKey))
    {
        _spriteRenderer.sprite = null;
        return;
    }

    Sprite sprite = ResourceManager.Instance != null ? ResourceManager.Instance.GetSprite(spriteKey) : null;
    if (sprite == null)
    {
        Debug.LogWarning($"[StageTexture] Missing Sprite resource: {spriteKey}", this);
    }

    _spriteRenderer.sprite = sprite;
}
```

编辑器预览：

```csharp
SpriteRegistry registry = FindRegistry<SpriteRegistry>();
_spriteRenderer.sprite = registry != null ? registry.GetAsset(spriteKey) : null;
```

## 7. 兼容迁移建议

如果已有数据使用 `textureKey`，建议做一个过渡版本：

```csharp
[ResourceKey(typeof(Sprite))]
public string spriteKey;

[Obsolete("Use spriteKey instead.")]
public string textureKey;
```

迁移策略：

- 新数据只写 `spriteKey`
- `ApplyData()` 优先使用 `spriteKey`
- 如果 `spriteKey` 为空且旧数据 `textureKey` 不为空，可暂时走旧 `Texture2D` 逻辑
- 完成关卡资源重新配置后，再删除旧字段和旧逻辑

## 8. 验证清单

1. 将多图切片中的某个子 Sprite 标记进 Addressables。
2. 运行 `SpriteRegistryBuilder`，确认 `SpriteRegistry.asset` 中出现该子 Sprite 的 key。
3. 在 `StageTexture.spriteKey` 中拖入子 Sprite。
4. 导出关卡，确认 `LevelConfig.sprites` 包含该 key。
5. 运行资源预加载，确认 `ResourceManager.GetSprite(key)` 能返回对应 Sprite。
6. 实例化关卡对象，确认 `SpriteRenderer.sprite` 保留正确切片区域、pivot 和 border。

