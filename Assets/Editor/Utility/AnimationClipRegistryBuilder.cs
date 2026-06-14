using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.IO;

public class AnimationClipRegistryBuilder
{
    private const string REGISTRY_PATH = "Assets/RemoteResource/AnimationClipRegistry.asset";

    [MenuItem("关卡构建/资源构建/一键生成动画片段注册表 (Addressable版)")]
    public static void BuildRegistry()
    {
        AnimationClipRegistry registry = AssetDatabase.LoadAssetAtPath<AnimationClipRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<AnimationClipRegistry>();
            
            string dir = Path.GetDirectoryName(REGISTRY_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
        }

        registry.mappings.Clear();

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[AnimationClipRegistryBuilder] 未在工程中找到 Addressables Settings！");
            EditorUtility.DisplayDialog("生成失败", "未在工程中检测到 Addressables 配置。", "确定");
            return;
        }

        int addedCount = 0;
        HashSet<string> registeredKeys = new HashSet<string>();

        foreach (var group in settings.groups)
        {
            if (group == null) continue;

            foreach (var entry in group.entries)
            {
                if (entry == null) continue;

                string lowerPath = entry.AssetPath.ToLower();
                if (!lowerPath.EndsWith(".anim"))
                    continue;

                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.AssetPath);
                if (clip == null) continue;

                if (registeredKeys.Contains(clip.name))
                {
                    Debug.LogWarning($"[AnimationClipRegistryBuilder] 发现重复的 animation clip 名称 \"{clip.name}\"，路径: {entry.AssetPath}，已跳过。");
                    continue;
                }

                var reference = new AssetReferenceT<AnimationClip>(entry.guid);

                registry.mappings.Add(new AnimationClipMapping
                {
                    key = clip.name,
                    animationClipReference = reference,
#if UNITY_EDITOR
                    animationClip = clip
#endif
                });

                registeredKeys.Add(clip.name);
                addedCount++;
            }
        }

        if (addedCount == 0)
        {
            Debug.LogWarning("[AnimationClipRegistryBuilder] 所有 Addressable 组中没有找到任何 AnimationClip 资源！");
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("绑定成功",
            $"动画片段注册表已生成！\n\n共登记了 {addedCount} 个 Addressable AnimationClip。\n存放路径: {REGISTRY_PATH}", "确定");

        Debug.Log($"[AnimationClipRegistryBuilder] 完成！共登记 {addedCount} 个 Addressable AnimationClip。");
    }
}
