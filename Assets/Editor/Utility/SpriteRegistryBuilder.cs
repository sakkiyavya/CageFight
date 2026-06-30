using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Object = UnityEngine.Object;

public class SpriteRegistryBuilder
{
    private const string REGISTRY_PATH = "Assets/RemoteResource/SpriteRegistry.asset";

    [MenuItem("关卡构建/资源构建/一键生成 Sprite 注册表 (Addressable版)")]
    public static void BuildRegistry()
    {
        SpriteRegistry registry = AssetDatabase.LoadAssetAtPath<SpriteRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<SpriteRegistry>();

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
            Debug.LogError("[SpriteRegistryBuilder] 未在工程中找到 Addressables Settings！");
            EditorUtility.DisplayDialog("生成失败", "未在工程中检测到 Addressables 配置。", "确定");
            return;
        }

        int addedCount = 0;
        int skippedCount = 0;
        HashSet<string> registeredKeys = new HashSet<string>();

        foreach (var group in settings.groups)
        {
            if (group == null) continue;

            foreach (var entry in group.entries)
            {
                if (entry == null) continue;

                string lowerPath = entry.AssetPath.ToLower();
                if (!(lowerPath.EndsWith(".png") || lowerPath.EndsWith(".jpg") || lowerPath.EndsWith(".jpeg") ||
                      lowerPath.EndsWith(".tga") || lowerPath.EndsWith(".psd") || lowerPath.EndsWith(".webp")))
                    continue;

                // 使用 LoadAllAssetsAtPath 以获取所有子资源（包括多图切片 Sprite）
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(entry.AssetPath);
                foreach (Object asset in assets)
                {
                    if (asset is not Sprite sprite) continue;

                    if (registeredKeys.Contains(sprite.name))
                    {
                        Debug.LogWarning($"[SpriteRegistryBuilder] 发现重复的 Sprite Key \"{sprite.name}\"，路径: {entry.AssetPath}，已跳过。");
                        skippedCount++;
                        continue;
                    }

                    registry.mappings.Add(new SpriteMapping
                    {
                        key = sprite.name,
                        spriteReference = new UnityEngine.AddressableAssets.AssetReferenceT<Sprite>(entry.guid),
#if UNITY_EDITOR
                        sprite = sprite
#endif
                    });

                    registeredKeys.Add(sprite.name);
                    addedCount++;
                }
            }
        }

        if (addedCount == 0)
        {
            Debug.LogWarning("[SpriteRegistryBuilder] 所有 Addressable 组中没有找到任何 Sprite 资源！");
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("绑定成功",
            $"Sprite 注册表已生成！\n\n共登记了 {addedCount} 个 Addressable Sprite。\n跳过了 {skippedCount} 个重复 Key。\n存放路径: {REGISTRY_PATH}", "确定");

        Debug.Log($"[SpriteRegistryBuilder] 完成！共登记 {addedCount} 个 Sprite，跳过 {skippedCount} 个重复。");
    }
}
