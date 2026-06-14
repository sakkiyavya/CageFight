using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[Serializable]
public class AnimationClipMapping
{
    public string key;
    
    [Tooltip("指向 Addressable 资源的安全弱引用，避免物理打包强绑定")]
    public AssetReferenceT<AnimationClip> animationClipReference;

#if UNITY_EDITOR
    [Tooltip("仅在编辑器下保留的强引用，方便在编辑状态下免热更预览，打包时会自动剔除，不占用首包体积")]
    public AnimationClip animationClip;
#endif
}

/// <summary>
/// 动画片段注册表
/// 用于在运行时将基于纯数据的 string key 映射到实际的 Addressable 资源。
/// 同时通过条件编译宏包裹原本的硬引用，确保在编辑器下免打包预览。
/// </summary>
[CreateAssetMenu(fileName = "AnimationClipRegistry", menuName = "ResourcesSystem/AnimationClip Registry")]
public class AnimationClipRegistry : ScriptableObject
{
    public List<AnimationClipMapping> mappings = new List<AnimationClipMapping>();

    private Dictionary<string, AssetReferenceT<AnimationClip>> _dictReference;

#if UNITY_EDITOR
    private Dictionary<string, AnimationClip> _dictEditor;
#endif

    /// <summary>
    /// 初始化映射缓存
    /// </summary>
    public void Initialize()
    {
        if (_dictReference != null) return;
        
        _dictReference = new Dictionary<string, AssetReferenceT<AnimationClip>>();
        foreach (var mapping in mappings)
        {
            if (mapping != null && !string.IsNullOrEmpty(mapping.key) && !_dictReference.ContainsKey(mapping.key))
            {
                _dictReference.Add(mapping.key, mapping.animationClipReference);
            }
        }

#if UNITY_EDITOR
        _dictEditor = new Dictionary<string, AnimationClip>();
        foreach (var mapping in mappings)
        {
            if (mapping != null && !string.IsNullOrEmpty(mapping.key) && !_dictEditor.ContainsKey(mapping.key))
            {
                _dictEditor.Add(mapping.key, mapping.animationClip);
            }
        }
#endif
    }

    /// <summary>
    /// 根据 key 获取 Addressable 安全引用句柄 (运行时资源系统使用)
    /// </summary>
    public AssetReferenceT<AnimationClip> GetReference(string key)
    {
        if (_dictReference == null) Initialize();
        
        if (_dictReference.TryGetValue(key, out var reference))
        {
            return reference;
        }
        return null;
    }

    /// <summary>
    /// 获取原始资源引用 (仅在 UNITY_EDITOR 下有效，供编辑器同步免热更预览)
    /// </summary>
    public AnimationClip GetAsset(string key)
    {
#if UNITY_EDITOR
        if (_dictEditor == null) Initialize();
        
        if (_dictEditor.TryGetValue(key, out var asset))
        {
            return asset;
        }
        Debug.LogError($"[AnimationClipRegistry] 编辑器模式下找不到 Key 为 '{key}' 的资源！请检查是否忘记重新生成注册表。");
        return null;
#else
        Debug.LogWarning("[AnimationClipRegistry] 运行时严禁直接调用 GetAsset() 获取强引用！请接入 ResourcesSystem。");
        return null;
#endif
    }
}
