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
    private const string LoadoutDefinitionRegistryPath = RegistryDirectory + "/LoadoutDefinitionRegistry.asset";

    [MenuItem("关卡构建/资源构建/一键生成全部资源注册表")]
    public static void BuildAll() => BuildAllInternal(true);

    /// <summary>供批处理和项目安装器调用的无弹窗资源注册表重建入口。</summary>
    public static void BuildAllForAutomation() => BuildAllInternal(false);

    private static void BuildAllInternal(bool showDialog)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[RegistryBuilder] 未找到 Addressables Settings，请先初始化 Addressables。");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "生成失败",
                    "未检测到 Addressables Settings，请先在 Addressables Groups 面板创建配置。",
                    "确定");
            }
            return;
        }

        Directory.CreateDirectory(RegistryDirectory);

        // 先把选装定义依赖的图标与预制体纳入 Addressables，随后构建的通用注册表
        // 才能在同一次操作中收录它们。
        int loadoutDefinitionCount = BuildLoadoutDefinitions(settings);
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
            $"AnimatorController: {animatorControllerCount}\n" +
            $"LoadoutDefinition: {loadoutDefinitionCount}";

        Debug.Log($"[RegistryBuilder] 全部资源注册表生成完成！\n{summary}");
        if (showDialog)
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

                // 记录子精灵名：运行时 Addressables 才能按名解析到正确的子精灵
                //（否则 m_SubObjectName 为空，多子精灵图集会回退到图集第一帧）。
                var spriteReference = new AssetReferenceT<Sprite>(entry.guid);
                spriteReference.SetEditorSubObject(sprite);

                registry.mappings.Add(new SpriteMapping
                {
                    key = sprite.name,
                    spriteReference = spriteReference,
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

    private static int BuildLoadoutDefinitions(AddressableAssetSettings settings)
    {
        LoadoutDefinitionRegistry registry =
            GetOrCreateRegistry<LoadoutDefinitionRegistry>(LoadoutDefinitionRegistryPath);
        List<EngineerDefinition> engineers = FindDefinitions<EngineerDefinition>();
        List<RaceDefinition> races = FindDefinitions<RaceDefinition>();
        List<SpellDefinition> spells = FindDefinitions<SpellDefinition>();

        foreach (EngineerDefinition engineer in engineers)
        {
            EnsureDefinitionAssetAddressable(settings, engineer.EditorIcon);
            EnsureDefinitionAssetAddressable(settings, engineer.EditorPortraitFrame);
            EnsureDefinitionAssetAddressable(settings, engineer.EditorPrefab);
        }
        foreach (RaceDefinition race in races)
        {
            EnsureDefinitionAssetAddressable(settings, race.EditorIcon);
            EnsureDefinitionAssetAddressable(settings, race.EditorRuntimeEffectPrefab);
        }
        foreach (SpellDefinition spell in spells)
        {
            EnsureDefinitionAssetAddressable(settings, spell.EditorIcon);
            EnsureDefinitionAssetAddressable(settings, spell.EditorCastPrefab);
            EnsureDefinitionAssetAddressable(settings, spell.EditorWarningCircle);
        }

        registry.ReplaceDefinitions(engineers, races, spells);
        MarkDirty(registry);
        EnsureAddressable(settings, LoadoutDefinitionRegistryPath, nameof(LoadoutDefinitionRegistry));
        return engineers.Count + races.Count + spells.Count;
    }

    private static List<T> FindDefinitions<T>() where T : ScriptableObject
    {
        string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
        List<T> definitions = new List<T>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T definition = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!definition) continue;

            if (definition is EngineerDefinition engineer)
                engineer.MigrateEditorReferences();
            else if (definition is RaceDefinition race)
                race.MigrateEditorReferences();
            else if (definition is SpellDefinition spell)
                spell.MigrateEditorReferences();

            EditorUtility.SetDirty(definition);
            definitions.Add(definition);
        }

        definitions.Sort((left, right) => string.CompareOrdinal(left.name, right.name));
        return definitions;
    }

    private static void EnsureAddressable(
        AddressableAssetSettings settings,
        string assetPath,
        string address)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrWhiteSpace(guid)) return;

        AddressableAssetEntry entry = settings.FindAssetEntry(guid);
        if (entry == null)
        {
            AddressableAssetGroup group = settings.FindGroup("LocalGroup") ?? settings.DefaultGroup;
            if (group == null)
            {
                Debug.LogError("[RegistryBuilder] 缺少 Addressables LocalGroup，无法注册选装定义表。");
                return;
            }

            entry = settings.CreateOrMoveEntry(guid, group);
        }

        entry.address = address;
        EditorUtility.SetDirty(settings);
    }

    /// <summary>
    /// 为定义引用的资源补充 Addressables 条目，但不会改写已有资源的地址，
    /// 以免影响其他系统按既有地址加载资源。
    /// </summary>
    private static void EnsureDefinitionAssetAddressable(
        AddressableAssetSettings settings,
        UnityEngine.Object asset)
    {
        if (!asset) return;

        string path = AssetDatabase.GetAssetPath(asset);
        string guid = AssetDatabase.AssetPathToGUID(path);
        if (string.IsNullOrWhiteSpace(guid) || settings.FindAssetEntry(guid) != null) return;

        AddressableAssetGroup group = settings.FindGroup("LocalGroup") ?? settings.DefaultGroup;
        if (group == null)
        {
            Debug.LogError("[RegistryBuilder] 缺少 Addressables LocalGroup，无法注册选装资源。");
            return;
        }

        settings.CreateOrMoveEntry(guid, group);
        EditorUtility.SetDirty(settings);
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
