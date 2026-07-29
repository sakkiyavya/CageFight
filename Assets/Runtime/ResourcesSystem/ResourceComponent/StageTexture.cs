using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[Serializable]
public class StageTextureData : ComponentData
{
    [ResourceKey(typeof(Sprite))]
    public string spriteKey;                                                                                        // 序列化保存的精灵资源键。
}

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class StageTexture : MonoBehaviour, IStageComponent
{
    [ResourceKey(typeof(Sprite))]
    [Tooltip("Sprite resource key.")]
    public string spriteKey;                                                                                        // 当前渲染器需要使用的精灵资源键。

    private SpriteRenderer _spriteRenderer;                                                                         // 接收运行时或编辑器预览精灵的渲染器。

    public Type DataType => typeof(StageTextureData);                                                               // 该组件对应的序列化数据类型。

    #region 生命周期与回调
    /// <summary>
    /// 缓存精灵渲染器，并在运行时从资源管理器应用当前资源键对应的精灵。
    /// </summary>
    private void Start()
    {
        CacheComponent();

        if (Application.isPlaying)
        {
            ApplyRuntimeResource();
        }
    }

    /// <summary>
    /// 组件启用时在编辑器环境刷新精灵预览；运行时资源由启动或数据注入流程负责。
    /// </summary>
    private void OnEnable()
    {
        // CacheComponent();

        // if (Application.isPlaying)
        // {
        //     ApplyRuntimeResource();
        // }
#if UNITY_EDITOR
        // else
        // {
            UpdateEditorPreview();
        // }
#endif
    }

    /// <summary>
    /// 编辑器预览状态下停用组件时清除渲染器上的临时精灵引用。
    /// </summary>
    private void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            ClearEditorPreview();
        }
#endif
    }
    #endregion

//     private void OnValidate()
//     {
//         CacheComponent();

// #if UNITY_EDITOR
//         if (!Application.isPlaying)
//         {
//             UpdateEditorPreview();
//         }
// #endif
//     }

    #region 关卡数据转换
    /// <summary>
    /// 将当前精灵资源键导出为可写入关卡配置的组件数据。
    /// </summary>
    /// <returns>包含当前精灵资源键的 <see cref="StageTextureData"/>。</returns>
    public ComponentData ExtractData()
    {
        return new StageTextureData
        {
            spriteKey = spriteKey
        };
    }

    /// <summary>
    /// 从纹理组件数据恢复精灵资源键，并根据运行环境应用已加载资源或刷新编辑器预览。
    /// </summary>
    /// <param name="data">期望为 <see cref="StageTextureData"/> 的关卡组件数据；类型不匹配时忽略。</param>
    public void ApplyData(ComponentData data)
    {
        if (data is not StageTextureData textureData) return;

        spriteKey = textureData.spriteKey;

        if (Application.isPlaying)
        {
            ApplyRuntimeResource();
        }
#if UNITY_EDITOR
        else
        {
            UpdateEditorPreview();
        }
#endif
    }
    #endregion

    #region 运行时资源注入
    /// <summary>
    /// 在尚未缓存时获取同一对象上的精灵渲染器。
    /// </summary>
    private void CacheComponent()
    {
        if (_spriteRenderer == null)
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    /// <summary>
    /// 根据当前资源键从资源管理器取得已加载精灵并写入渲染器；
    /// 空资源键会清空精灵，资源缺失时会记录警告。
    /// </summary>
    private void ApplyRuntimeResource()
    {
        CacheComponent();

        if (_spriteRenderer == null) return;

        if (string.IsNullOrEmpty(spriteKey))
        {
            _spriteRenderer.sprite = null;
            return;
        }

        Sprite sprite = ResourceManager.Instance != null ? ResourceManager.Instance.GetSprite(spriteKey) : null;    // 当前资源键对应的运行时精灵。
        // Debug.Log("ResourceManager实例：" + ResourceManager.Instance.name);
        if (sprite == null)
        {
            Debug.LogWarning($"[StageTexture] Missing Sprite resource: {spriteKey}", this);
        }

        // Debug.Log(name + " 的sprite: " + sprite.name);
        _spriteRenderer.sprite = sprite;
    }
    #endregion

#if UNITY_EDITOR
    #region 编辑器预览
    /// <summary>
    /// 在编辑器中查找精灵注册表并同步当前资源键对应的强引用，用于无需运行游戏的场景预览。
    /// </summary>
    private void UpdateEditorPreview()
    {
        CacheComponent();

        if (_spriteRenderer == null || string.IsNullOrEmpty(spriteKey))
        {
            ClearEditorPreview();
            return;
        }

        SpriteRegistry registry = FindRegistry<SpriteRegistry>();                                                   // 编辑器预览使用的精灵注册表。
        _spriteRenderer.sprite = registry != null ? registry.GetAsset(spriteKey) : null;

        if (_spriteRenderer.sprite == null)
        {
            ClearEditorPreview();
        }
    }

    /// <summary>
    /// 清除编辑器预览写入的精灵，避免组件停用后继续保留临时显示。
    /// </summary>
    private void ClearEditorPreview()
    {
        CacheComponent();

        if (_spriteRenderer != null)
        {
            _spriteRenderer.sprite = null;
        }
    }

    /// <summary>
    /// 在编辑器资产数据库中查找指定类型的第一个资源注册表。
    /// </summary>
    /// <typeparam name="T">需要查找的注册表 ScriptableObject 类型。</typeparam>
    /// <returns>找到的第一个注册表资产；没有匹配资产时返回 <see langword="null"/>。</returns>
    private static T FindRegistry<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");                                           // 匹配指定类型的资产 GUID。
        if (guids.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);                                                      // 第一个匹配注册表的资产路径。
        return AssetDatabase.LoadAssetAtPath<T>(path);
    }
    #endregion
#endif
}
