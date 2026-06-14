using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.IO;

public class TextureRegistryBuilder
{
    private const string REGISTRY_PATH = "Assets/RemoteResource/TextureRegistry.asset";

    [MenuItem("关卡构建/资源构建/一键生成纹理注册表 (Addressable版)")]
    public static void BuildRegistry()
    {
        TextureRegistry registry = AssetDatabase.LoadAssetAtPath<TextureRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<TextureRegistry>();
            
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
            Debug.LogError("[TextureRegistryBuilder] 未在工程中找到 Addressables Settings！");
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
                if (!(lowerPath.EndsWith(".png") || lowerPath.EndsWith(".jpg") || lowerPath.EndsWith(".jpeg") || lowerPath.EndsWith(".tga") || lowerPath.EndsWith(".psd")))
                    continue;

                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.AssetPath);
                if (texture == null) continue;

                if (registeredKeys.Contains(texture.name))
                {
                    Debug.LogWarning($"[TextureRegistryBuilder] 发现重复的 texture 名称 \"{texture.name}\"，路径: {entry.AssetPath}，已跳过。");
                    continue;
                }

                var reference = new AssetReferenceT<Texture2D>(entry.guid);

                registry.mappings.Add(new TextureMapping
                {
                    key = texture.name,
                    textureReference = reference,
#if UNITY_EDITOR
                    texture = texture
#endif
                });

                registeredKeys.Add(texture.name);
                addedCount++;
            }
        }

        if (addedCount == 0)
        {
            Debug.LogWarning("[TextureRegistryBuilder] 所有 Addressable 组中没有找到任何 Texture 资源！");
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("绑定成功",
            $"纹理注册表已生成！\n\n共登记了 {addedCount} 个 Addressable Texture。\n存放路径: {REGISTRY_PATH}", "确定");

        Debug.Log($"[TextureRegistryBuilder] 完成！共登记 {addedCount} 个 Addressable Texture。");
    }
}
