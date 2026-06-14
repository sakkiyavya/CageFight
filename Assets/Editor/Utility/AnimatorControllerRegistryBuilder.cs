using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.IO;

public class AnimatorControllerRegistryBuilder
{
    private const string REGISTRY_PATH = "Assets/RemoteResource/AnimatorControllerRegistry.asset";

    [MenuItem("关卡构建/资源构建/一键生成动画控制器注册表 (Addressable版)")]
    public static void BuildRegistry()
    {
        AnimatorControllerRegistry registry = AssetDatabase.LoadAssetAtPath<AnimatorControllerRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<AnimatorControllerRegistry>();
            
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
            Debug.LogError("[AnimatorControllerRegistryBuilder] 未在工程中找到 Addressables Settings！");
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
                if (!lowerPath.EndsWith(".controller"))
                    continue;

                RuntimeAnimatorController controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(entry.AssetPath);
                if (controller == null) continue;

                if (registeredKeys.Contains(controller.name))
                {
                    Debug.LogWarning($"[AnimatorControllerRegistryBuilder] 发现重复的 animator controller 名称 \"{controller.name}\"，路径: {entry.AssetPath}，已跳过。");
                    continue;
                }

                var reference = new AssetReferenceT<RuntimeAnimatorController>(entry.guid);

                registry.mappings.Add(new AnimatorControllerMapping
                {
                    key = controller.name,
                    animatorControllerReference = reference,
#if UNITY_EDITOR
                    animatorController = controller
#endif
                });

                registeredKeys.Add(controller.name);
                addedCount++;
            }
        }

        if (addedCount == 0)
        {
            Debug.LogWarning("[AnimatorControllerRegistryBuilder] 所有 Addressable 组中没有找到任何 AnimatorController 资源！");
        }

        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("绑定成功",
            $"动画控制器注册表已生成！\n\n共登记了 {addedCount} 个 Addressable AnimatorController。\n存放路径: {REGISTRY_PATH}", "确定");

        Debug.Log($"[AnimatorControllerRegistryBuilder] 完成！共登记 {addedCount} 个 Addressable AnimatorController。");
    }
}
