using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.IO;

public class AudioRegistryBuilder
{
    private const string REGISTRY_PATH = "Assets/RemoteResource/AudioRegistry.asset";

    [MenuItem("关卡构建/资源构建/一键生成音频注册表 (Addressable版)")]
    public static void BuildRegistry()
    {
        AudioRegistry registry = AssetDatabase.LoadAssetAtPath<AudioRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<AudioRegistry>();
            
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
            Debug.LogError("[AudioRegistryBuilder] 未在工程中找到 Addressables Settings！");
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
                if (!(lowerPath.EndsWith(".mp3") || lowerPath.EndsWith(".wav") || lowerPath.EndsWith(".ogg")))
                    continue;

                AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(entry.AssetPath);
                if (audioClip == null) continue;

                if (registeredKeys.Contains(audioClip.name))
                {
                    Debug.LogWarning($"[AudioRegistryBuilder] 发现重复的 audio 名称 \"{audioClip.name}\"，路径: {entry.AssetPath}，已跳过。");
                    continue;
                }

                var reference = new AssetReferenceT<AudioClip>(entry.guid);

                registry.mappings.Add(new AudioMapping
                {
                    key = audioClip.name,
                    audioReference = reference,
#if UNITY_EDITOR
                    audioClip = audioClip
#endif
                });

                registeredKeys.Add(audioClip.name);
                addedCount++;
            }
        }

        if (addedCount == 0)
        {
            Debug.LogWarning("[AudioRegistryBuilder] 所有 Addressable 组中没有找到任何 Audio 资源！");
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("绑定成功",
            $"音频注册表已生成！\n\n共登记了 {addedCount} 个 Addressable AudioClip。\n存放路径: {REGISTRY_PATH}", "确定");

        Debug.Log($"[AudioRegistryBuilder] 完成！共登记 {addedCount} 个 Addressable AudioClip。");
    }
}
