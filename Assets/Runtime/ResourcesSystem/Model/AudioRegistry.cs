using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[Serializable]
public class AudioMapping
{
    public string key;                                                        // 关卡数据和业务代码用于查找该音频片段的逻辑键。
    
    [Tooltip("指向 Addressable 资源的安全弱引用，避免物理打包强绑定")]
    public AssetReferenceT<AudioClip> audioReference;                         // 玩家构建中用于异步加载音频片段的 Addressables 引用。

#if UNITY_EDITOR
    [Tooltip("仅在编辑器下保留的强引用，方便在编辑状态下免热更预览，打包时会自动剔除，不占用首包体积")]
    public AudioClip audioClip;                                               // 编辑器预览时直接使用、不会进入玩家构建的音频片段引用。
#endif
}

/// <summary>
/// 音频注册表
/// 用于在运行时将基于纯数据的 string key 映射到实际的 Addressable 资源。
/// 同时通过条件编译宏包裹原本的硬引用，确保在编辑器下免打包预览。
/// </summary>
[CreateAssetMenu(fileName = "AudioRegistry", menuName = "ResourcesSystem/Audio Registry")]
public class AudioRegistry : ScriptableObject
{
    public List<AudioMapping> mappings = new List<AudioMapping>();            // Inspector 中配置的全部音频键与资源引用。

    private Dictionary<string, AssetReferenceT<AudioClip>> _dictReference;    // 运行时按逻辑键查询 Addressables 引用的索引。

#if UNITY_EDITOR
    private Dictionary<string, AudioClip> _dictEditor;                        // 编辑器中按逻辑键查询音频强引用的预览索引。
#endif

    #region 索引初始化
    /// <summary>
    /// 首次使用时根据配置列表构建资源键到 Addressables 音频引用的运行时索引；
    /// 在编辑器环境下同时构建音频片段强引用的预览索引。
    /// </summary>
    public void Initialize()
    {
        if (_dictReference != null) return;
        
        _dictReference = new Dictionary<string, AssetReferenceT<AudioClip>>();
        foreach (var mapping in mappings)
        {
            if (mapping != null && !string.IsNullOrEmpty(mapping.key) && !_dictReference.ContainsKey(mapping.key))
            {
                _dictReference.Add(mapping.key, mapping.audioReference);
            }
        }

#if UNITY_EDITOR
        _dictEditor = new Dictionary<string, AudioClip>();
        foreach (var mapping in mappings)
        {
            if (mapping != null && !string.IsNullOrEmpty(mapping.key) && !_dictEditor.ContainsKey(mapping.key))
            {
                _dictEditor.Add(mapping.key, mapping.audioClip);
            }
        }
#endif
    }
    #endregion

    #region 资源查询
    /// <summary>
    /// 根据资源键获取音频片段的 Addressables 安全引用，供运行时资源系统异步加载。
    /// </summary>
    /// <param name="key">注册表中配置的音频资源键。</param>
    /// <returns>对应的 Addressables 引用；键不存在时返回 <see langword="null"/>。</returns>
    public AssetReferenceT<AudioClip> GetReference(string key)
    {
        if (_dictReference == null) Initialize();
        
        if (_dictReference.TryGetValue(key, out var reference))
        {
            return reference;
        }
        return null;
    }

    /// <summary>
    /// 在编辑器中根据资源键获取音频片段强引用，用于无需 Addressables 构建的同步预览。
    /// 非编辑器构建调用该方法时只记录警告并返回空值。
    /// </summary>
    /// <param name="key">注册表中配置的音频资源键。</param>
    /// <returns>编辑器预览用音频片段；键不存在或运行于玩家构建时返回 <see langword="null"/>。</returns>
    public AudioClip GetAsset(string key)
    {
#if UNITY_EDITOR
        if (_dictEditor == null) Initialize();
        
        if (_dictEditor.TryGetValue(key, out var asset))
        {
            return asset;
        }
        Debug.LogError($"[AudioRegistry] 编辑器模式下找不到 Key 为 '{key}' 的资源！请检查是否忘记重新生成注册表。");
        return null;
#else
        Debug.LogWarning("[AudioRegistry] 运行时严禁直接调用 GetAsset() 获取强引用！请接入 ResourcesSystem。");
        return null;
#endif
    }
    #endregion
}
