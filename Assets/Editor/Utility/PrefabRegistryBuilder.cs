using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;
using System.IO;

public class PrefabRegistryBuilder
{
    // 将注册表存放在统一的 Resource 目录下，这样它就能被打包，或者被 Addressables 轻松引用
    private const string REGISTRY_PATH = "Assets/RemoteResource/PrefabRegistry.asset";
    
    // 关卡重资源分配的专属更新组名称
    private const string ADDRESSABLE_GROUP_NAME = "LevelObjects";

    [MenuItem("关卡构建/资源构建/一键生成预制体注册表 (Addressable版)")]
    public static void BuildRegistry()
    {
        // 1. 寻找或创建 Registry SO
        PrefabRegistry registry = AssetDatabase.LoadAssetAtPath<PrefabRegistry>(REGISTRY_PATH);
        if (registry == null)
        {
            registry = ScriptableObject.CreateInstance<PrefabRegistry>();
            
            string dir = Path.GetDirectoryName(REGISTRY_PATH);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            AssetDatabase.CreateAsset(registry, REGISTRY_PATH);
        }

        registry.mappings.Clear();

        // 2. 准备 Addressables 设置
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[PrefabRegistryBuilder] 未在工程中找到 Addressables Settings！请先在 Windows -> Asset Management -> Addressables -> Groups 面板点击 'Create Addressables Settings' 进行初始化！");
            EditorUtility.DisplayDialog("生成失败", "未在工程中检测到 Addressables 配置，请先初始化 Addressables 后重试。", "确定");
            return;
        }

        // 3. 遍历所有 Addressable 组，将其中的 Prefab 以 name 为 key 登记到 PrefabRegistry
        int addedCount = 0;
        HashSet<string> registeredKeys = new HashSet<string>();

        foreach (var group in settings.groups)
        {
            if (group == null) continue;

            foreach (var entry in group.entries)
            {
                if (entry == null) continue;

                // 只处理 Prefab 资源
                if (!entry.AssetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.AssetPath);
                if (prefab == null) continue;

                // 防止重复 key（同名 prefab 只登记第一个）
                if (registeredKeys.Contains(prefab.name))
                {
                    Debug.LogWarning($"[PrefabRegistryBuilder] 发现重复的 prefab 名称 \"{prefab.name}\"，路径: {entry.AssetPath}，已跳过。请确保 Addressable 中的 Prefab 名称唯一。");
                    continue;
                }

                // 创建对应的 AssetReferenceGameObject 安全弱引用
                var reference = new AssetReferenceGameObject(entry.guid);

                // 登记映射
                registry.mappings.Add(new PrefabMapping
                {
                    key = prefab.name,
                    prefabReference = reference,
#if UNITY_EDITOR
                    prefab = prefab // 编辑器下保留强引用，便于预览
#endif
                });

                registeredKeys.Add(prefab.name);
                addedCount++;
            }
        }

        if (addedCount == 0)
        {
            Debug.LogWarning("[PrefabRegistryBuilder] 所有 Addressable 组中没有找到任何 Prefab 资源！");
            EditorUtility.DisplayDialog("绑定提示", "Addressable 组中没有发现任何 Prefab 资源，注册表已清空。", "确定");
        }

        // 4. 保存资产并刷新
        EditorUtility.SetDirty(registry);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("绑定成功",
            $"预制体注册表已生成！\n\n" +
            $"共登记了 {addedCount} 个 Addressable Prefab。\n" +
            $"存放路径: {REGISTRY_PATH}", "确定");

        Debug.Log($"[PrefabRegistryBuilder] 完成！共登记 {addedCount} 个 Addressable Prefab 到 PrefabRegistry。");
    }
}
