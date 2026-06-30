using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

[Serializable]
public class SpriteMapping
{
    public string key;

    [Tooltip("指向 Addressable 资源的安全弱引用，避免物理打包强绑定")]
    public AssetReferenceT<Sprite> spriteReference;

#if UNITY_EDITOR
    [Tooltip("仅在编辑器下保留的强引用，方便在编辑状态下免热更预览，打包时会自动剔除，不占用首包体积")]
    public Sprite sprite;
#endif
}

/// <summary>
/// Sprite 注册表
/// 用于在运行时将基于纯数据的 string key 映射到实际的 Addressable Sprite 资源。
/// 支持多图切片的子图资源，保留原始 rect、pivot、border、pixelsPerUnit 等信息。
/// </summary>
[CreateAssetMenu(fileName = "SpriteRegistry", menuName = "ResourcesSystem/Sprite Registry")]
public class SpriteRegistry : ScriptableObject
{
    public List<SpriteMapping> mappings = new List<SpriteMapping>();

    private Dictionary<string, AssetReferenceT<Sprite>> _dictReference;

#if UNITY_EDITOR
    private Dictionary<string, Sprite> _dictEditor;
#endif

    /// <summary>
    /// 初始化映射缓存
    /// </summary>
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

    /// <summary>
    /// 根据 key 获取 Addressable 安全引用句柄 (运行时资源系统使用)
    /// </summary>
    public AssetReferenceT<Sprite> GetReference(string key)
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
    public Sprite GetAsset(string key)
    {
#if UNITY_EDITOR
        if (_dictEditor == null) Initialize();

        if (_dictEditor.TryGetValue(key, out var asset))
        {
            return asset;
        }
        Debug.LogError($"[SpriteRegistry] 编辑器模式下找不到 Key 为 '{key}' 的资源！请检查是否忘记重新生成注册表。");
        return null;
#else
        Debug.LogWarning("[SpriteRegistry] 运行时严禁直接调用 GetAsset() 获取强引用！请接入 ResourcesSystem。");
        return null;
#endif
    }
}
