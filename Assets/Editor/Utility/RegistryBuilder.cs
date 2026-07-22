using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// <summary>
/// 统一构建所有 Addressable 资源注册表。
/// </summary>
public static class RegistryBuilder
{
    private const string RegistryDirectory = "Assets/RemoteResource";

    private const string PrefabRegistryPath = RegistryDirectory + "/PrefabRegistry.asset";
    private const string TextureRegistryPath = RegistryDirectory + "/TextureRegistry.asset";
    private const string SpriteRegistryPath = RegistryDirectory + "/SpriteRegistry.asset";
    private const string AudioRegistryPath = RegistryDirectory + "/AudioRegistry.asset";
    private const string AnimationClipRegistryPath = RegistryDirectory + "/AnimationClipRegistry.asset";
    private const string AnimatorControllerRegistryPath = RegistryDirectory + "/AnimatorControllerRegistry.asset";

    [MenuItem("关卡构建/资源构建/一键生成全部资源注册表")]
    public static void BuildAll()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[RegistryBuilder] 未找到 Addressables Settings，请先初始化 Addressables。");
            EditorUtility.DisplayDialog(
                "生成失败",
                "未检测到 Addressables Settings，请先在 Addressables Groups 面板创建配置。",
                "确定");
            return;
        }

        Directory.CreateDirectory(RegistryDirectory);

        int prefabCount = BuildPrefabs(settings);
        int textureCount = BuildTextures(settings);
        int spriteCount = BuildSprites(settings);
        int audioCount = BuildAudios(settings);
        int animationClipCount = BuildAnimationClips(settings);
        int animatorControllerCount = BuildAnimatorControllers(settings);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string summary =
            $"Prefab: {prefabCount}\n" +
            $"Texture: {textureCount}\n" +
            $"Sprite: {spriteCount}\n" +
            $"AudioClip: {audioCount}\n" +
            $"AnimationClip: {animationClipCount}\n" +
            $"AnimatorController: {animatorControllerCount}";

        Debug.Log($"[RegistryBuilder] 全部资源注册表生成完成！\n{summary}");
        EditorUtility.DisplayDialog("生成完成", $"全部资源注册表已更新。\n\n{summary}", "确定");
    }

    private static IEnumerable<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry> GetEntries(AddressableAssetSettings settings)
    {
        foreach (var group in settings.groups)
        {
            if (group == null)
                continue;

            foreach (var entry in group.entries)
            {
                if (entry != null)
                    yield return entry;
            }
        }
    }

    private static TRegistry GetOrCreateRegistry<TRegistry>(string path)
        where TRegistry : ScriptableObject
    {
        TRegistry registry = AssetDatabase.LoadAssetAtPath<TRegistry>(path);
        if (registry != null)
            return registry;

        registry = ScriptableObject.CreateInstance<TRegistry>();
        AssetDatabase.CreateAsset(registry, path);
        return registry;
    }

    private static int BuildPrefabs(AddressableAssetSettings settings)
    {
        PrefabRegistry registry = GetOrCreateRegistry<PrefabRegistry>(PrefabRegistryPath);
        registry.mappings.Clear();

        int count = 0;
        var keys = new HashSet<string>();
        foreach (var entry in GetEntries(settings))
        {
            if (!entry.AssetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                continue;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
            if (prefab == null || !keys.Add(prefab.name))
            {
                if (prefab != null)
                    Debug.LogWarning($"[RegistryBuilder] 重复 Prefab Key：{prefab.name}，已跳过：{entry.AssetPath}");
                continue;
            }

            registry.mappings.Add(new PrefabMapping
            {
                key = prefab.name,
                prefabReference = new AssetReferenceGameObject(entry.guid),
#if UNITY_EDITOR
                prefab = prefab
#endif
            });
            count++;
        }

        MarkDirty(registry);
        return count;
    }

    private static int BuildTextures(AddressableAssetSettings settings)
    {
        TextureRegistry registry = GetOrCreateRegistry<TextureRegistry>(TextureRegistryPath);
        registry.mappings.Clear();

        int count = 0;
        var keys = new HashSet<string>();
        foreach (var entry in GetEntries(settings))
        {
            if (!HasExtension(entry.AssetPath, ".png", ".jpg", ".jpeg", ".tga", ".psd"))
                continue;

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.AssetPath);
            if (texture == null || !keys.Add(texture.name))
            {
                if (texture != null)
                    Debug.LogWarning($"[RegistryBuilder] 重复 Texture Key：{texture.name}，已跳过：{entry.AssetPath}");
                continue;
            }

            registry.mappings.Add(new TextureMapping
            {
                key = texture.name,
                textureReference = new AssetReferenceT<Texture2D>(entry.guid),
#if UNITY_EDITOR
                texture = texture
#endif
            });
            count++;
        }

        MarkDirty(registry);
        return count;
    }

    private static int BuildSprites(AddressableAssetSettings settings)
    {
        SpriteRegistry registry = GetOrCreateRegistry<SpriteRegistry>(SpriteRegistryPath);
        registry.mappings.Clear();

        int count = 0;
        var keys = new HashSet<string>();
        foreach (var entry in GetEntries(settings))
        {
            if (!HasExtension(entry.AssetPath, ".png", ".jpg", ".jpeg", ".tga", ".psd", ".webp"))
                continue;

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(entry.AssetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (!(asset is Sprite sprite) || !keys.Add(sprite.name))
                {
                    if (asset is Sprite duplicate)
                        Debug.LogWarning($"[RegistryBuilder] 重复 Sprite Key：{duplicate.name}，已跳过：{entry.AssetPath}");
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
                count++;
            }
        }

        MarkDirty(registry);
        return count;
    }

    private static int BuildAudios(AddressableAssetSettings settings)
    {
        AudioRegistry registry = GetOrCreateRegistry<AudioRegistry>(AudioRegistryPath);
        registry.mappings.Clear();

        int count = 0;
        var keys = new HashSet<string>();
        foreach (var entry in GetEntries(settings))
        {
            if (!HasExtension(entry.AssetPath, ".mp3", ".wav", ".ogg"))
                continue;

            AudioClip audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(entry.AssetPath);
            if (audioClip == null || !keys.Add(audioClip.name))
            {
                if (audioClip != null)
                    Debug.LogWarning($"[RegistryBuilder] 重复 Audio Key：{audioClip.name}，已跳过：{entry.AssetPath}");
                continue;
            }

            registry.mappings.Add(new AudioMapping
            {
                key = audioClip.name,
                audioReference = new AssetReferenceT<AudioClip>(entry.guid),
#if UNITY_EDITOR
                audioClip = audioClip
#endif
            });
            count++;
        }

        MarkDirty(registry);
        return count;
    }

    private static int BuildAnimationClips(AddressableAssetSettings settings)
    {
        AnimationClipRegistry registry = GetOrCreateRegistry<AnimationClipRegistry>(AnimationClipRegistryPath);
        registry.mappings.Clear();

        int count = 0;
        var keys = new HashSet<string>();
        foreach (var entry in GetEntries(settings))
        {
            if (!entry.AssetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
                continue;

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(entry.AssetPath);
            if (clip == null || !keys.Add(clip.name))
            {
                if (clip != null)
                    Debug.LogWarning($"[RegistryBuilder] 重复 AnimationClip Key：{clip.name}，已跳过：{entry.AssetPath}");
                continue;
            }

            registry.mappings.Add(new AnimationClipMapping
            {
                key = clip.name,
                animationClipReference = new AssetReferenceT<AnimationClip>(entry.guid),
#if UNITY_EDITOR
                animationClip = clip
#endif
            });
            count++;
        }

        MarkDirty(registry);
        return count;
    }

    private static int BuildAnimatorControllers(AddressableAssetSettings settings)
    {
        AnimatorControllerRegistry registry = GetOrCreateRegistry<AnimatorControllerRegistry>(AnimatorControllerRegistryPath);
        registry.mappings.Clear();

        int count = 0;
        var keys = new HashSet<string>();
        foreach (var entry in GetEntries(settings))
        {
            if (!entry.AssetPath.EndsWith(".controller", StringComparison.OrdinalIgnoreCase))
                continue;

            RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(entry.AssetPath);
            if (controller == null || !keys.Add(controller.name))
            {
                if (controller != null)
                    Debug.LogWarning($"[RegistryBuilder] 重复 AnimatorController Key：{controller.name}，已跳过：{entry.AssetPath}");
                continue;
            }

            registry.mappings.Add(new AnimatorControllerMapping
            {
                key = controller.name,
                animatorControllerReference = new AssetReferenceT<RuntimeAnimatorController>(entry.guid),
#if UNITY_EDITOR
                animatorController = controller
#endif
            });
            count++;
        }

        MarkDirty(registry);
        return count;
    }

    private static bool HasExtension(string path, params string[] extensions)
    {
        foreach (string extension in extensions)
        {
            if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void MarkDirty(ScriptableObject registry)
    {
        EditorUtility.SetDirty(registry);
    }
}
