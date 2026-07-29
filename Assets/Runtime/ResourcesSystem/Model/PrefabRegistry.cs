using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[Serializable]
public class PrefabMapping
{
    public string key;                                                      // 关卡数据和业务代码用于查找该预制体的逻辑键。
    
    [Tooltip("指向 Addressable 资源的安全弱引用，避免物理打包强绑定")]
    public AssetReferenceGameObject prefabReference;                        // 玩家构建中用于异步加载预制体的 Addressables 引用。

#if UNITY_EDITOR
    [Tooltip("仅在编辑器下保留的强引用，方便在编辑状态下免热更预览，打包时会自动剔除，不占用首包体积")]
    public GameObject prefab;                                               // 编辑器预览时直接使用、不会进入玩家构建的预制体引用。
#endif
}

/// <summary>
/// 预制体注册表 (方案 A 的核心)
/// 用于在运行时将基于纯数据的 string key 映射到实际的 Addressable 资源。
/// 同时通过条件编译宏包裹原本的硬引用，确保在编辑器下免打包预览。
/// </summary>
[CreateAssetMenu(fileName = "PrefabRegistry", menuName = "StageSystem/Prefab Registry")]
public class PrefabRegistry : ScriptableObject
{
    public List<PrefabMapping> mappings = new List<PrefabMapping>();        // Inspector 中配置的全部预制体键与资源引用。

    private Dictionary<string, AssetReferenceGameObject> _dictReference;    // 运行时按逻辑键查询 Addressables 引用的索引。

#if UNITY_EDITOR
    private Dictionary<string, GameObject> _dictEditor;                     // 编辑器中按逻辑键查询预制体强引用的预览索引。
#endif

    #region 索引初始化
    /// <summary>
    /// 首次使用时根据配置列表构建资源键到 Addressables 引用的运行时索引；
    /// 在编辑器环境下同时构建资源键到预制体强引用的预览索引。
    /// </summary>
    public void Initialize()
    {
        if (_dictReference != null) return;
        
        _dictReference = new Dictionary<string, AssetReferenceGameObject>();
        foreach (var mapping in mappings)
        {
            if (mapping != null && !string.IsNullOrEmpty(mapping.key) && !_dictReference.ContainsKey(mapping.key))
            {
                _dictReference.Add(mapping.key, mapping.prefabReference);
            }
        }

#if UNITY_EDITOR
        _dictEditor = new Dictionary<string, GameObject>();
        foreach (var mapping in mappings)
        {
            if (mapping != null && !string.IsNullOrEmpty(mapping.key) && !_dictEditor.ContainsKey(mapping.key))
            {
                _dictEditor.Add(mapping.key, mapping.prefab);
            }
        }
#endif
    }
    #endregion

    #region 资源查询
    /// <summary>
    /// 根据资源键获取预制体的 Addressables 安全引用，供运行时资源系统异步加载。
    /// </summary>
    /// <param name="key">注册表中配置的预制体资源键。</param>
    /// <returns>对应的 Addressables 引用；键不存在时返回 <see langword="null"/>。</returns>
    public AssetReferenceGameObject GetReference(string key)
    {
        if (_dictReference == null) Initialize();
        
        if (_dictReference.TryGetValue(key, out AssetReferenceGameObject reference))
        {
            return reference;
        }
        return null;
    }

    /// <summary>
    /// 在编辑器中根据资源键获取预制体强引用，用于无需 Addressables 构建的同步预览。
    /// 非编辑器构建调用该方法时只记录警告并返回空值。
    /// </summary>
    /// <param name="key">注册表中配置的预制体资源键。</param>
    /// <returns>编辑器预览用预制体；键不存在或运行于玩家构建时返回 <see langword="null"/>。</returns>
    public GameObject GetPrefab(string key)
    {
#if UNITY_EDITOR
        if (_dictEditor == null) Initialize();
        
        if (_dictEditor.TryGetValue(key, out GameObject prefab))
        {
            return prefab;
        }
        Debug.LogError($"[PrefabRegistry] 编辑器模式下找不到 Key 为 '{key}' 的预制体！请检查是否忘记重新生成注册表。");
        return null;
#else
        Debug.LogWarning("[PrefabRegistry] 运行时严禁直接调用 GetPrefab() 获取强引用！请接入 ResourcesSystem。");
        return null;
#endif
    }
    #endregion
}
