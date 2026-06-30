using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LevelExporter
{
    public static void ExportLevel(uint levelId, string savePath)
    {
        // 1. 获取场景中所有打上隐式标记的物体
        var markers = Object.FindObjectsOfType<LevelObjectMarker>(true);
        
        LevelConfig config = ScriptableObject.CreateInstance<LevelConfig>();
        config.levelId = (int)levelId;
        config.objects = new List<LevelObjectData>();

        // 资源 Key 收集容器（全局去重与防循环引用）
        HashSet<string> visitedKeys = new HashSet<string>();
        HashSet<string> visitedPrefabs = new HashSet<string>();

        config.prefabs.Clear();
        config.audios.Clear();
        config.textures.Clear();
        config.animationClips.Clear();
        config.animatorControllers.Clear();
        config.sprites.Clear();

        int autoInstanceId = 1000; // 实例 ID 自增起点

        foreach (var marker in markers)
        {
            GameObject go = marker.gameObject;
            
            // 安全性检查：必须是 Prefab 实例
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
            {
                Debug.LogWarning($"物体 {go.name} 不是 Prefab，已被系统跳过。");
                continue;
            }

            // 提取 Prefab 资源名称作为 key
            GameObject prefabAsset = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            string key = prefabAsset != null ? prefabAsset.name : go.name;

            var objData = new LevelObjectData
            {
                instanceId = autoInstanceId++,
                prefabKey = key,
                transform = new TransformData
                {
                    position = go.transform.position,
                    rotation = go.transform.eulerAngles,
                    scale = go.transform.localScale
                },
                components = new List<ComponentData>()
            };

            // 提取组件数据
            var levelComponents = go.GetComponentsInChildren<ILevelComponent>(true);
            foreach (var comp in levelComponents)
            {
                ComponentData extracted = comp.ExtractData();
                if (extracted != null)
                {
                    objData.components.Add(extracted);
                }
            }

            config.objects.Add(objData);

            // 将 prefabKey 自身作为资源加入，并标记已访问以防递归扫描其对应的 Prefab 资源
            if (visitedKeys.Add(key))
            {
                AddKeyToConfig(key, typeof(GameObject), config);
            }
            visitedPrefabs.Add(key);

            // 扫描该 GO 上所有组件的 [ResourceKey] 字段
            CollectResourceKeys(go, visitedKeys, visitedPrefabs, config);
        }

        // 2. 确保目录存在
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        // 3. 写入 SO 资产
        string fullPath = $"{savePath}/Stage{levelId}.asset";
        
        AssetDatabase.CreateAsset(config, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        int totalKeys = config.prefabs.Count + config.audios.Count + config.textures.Count + config.animationClips.Count + config.animatorControllers.Count;
        EditorUtility.DisplayDialog("导出成功",
            $"关卡 {levelId} 已成功导出到：\n{fullPath}\n" +
            $"共收集了 {config.objects.Count} 个关卡物品。\n" +
            $"共扫描到 {totalKeys} 个资源 Key (Prefab:{config.prefabs.Count}, Audio:{config.audios.Count}, Texture:{config.textures.Count}, AnimClip:{config.animationClips.Count}, AnimCtrl:{config.animatorControllers.Count})。", "确定");
    }

    /// <summary>
    /// 将资源 Key 按照其资源类型存入 LevelConfig 对应的列表中
    /// </summary>
    private static void AddKeyToConfig(string key, Type type, LevelConfig config)
    {
        if (type == typeof(GameObject))
        {
            if (!config.prefabs.Contains(key)) config.prefabs.Add(key);
        }
        else if (type == typeof(AudioClip))
        {
            if (!config.audios.Contains(key)) config.audios.Add(key);
        }
        else if (type == typeof(Texture2D))
        {
            if (!config.textures.Contains(key)) config.textures.Add(key);
        }
        else if (type == typeof(AnimationClip))
        {
            if (!config.animationClips.Contains(key)) config.animationClips.Add(key);
        }
        else if (type == typeof(RuntimeAnimatorController))
        {
            if (!config.animatorControllers.Contains(key)) config.animatorControllers.Add(key);
        }
        else if (type == typeof(Sprite))
        {
            if (!config.sprites.Contains(key)) config.sprites.Add(key);
        }
    }

    /// <summary>
    /// 扫描场景 GameObject 上所有 Component 中标有 [ResourceKey] 的字段，收集资源 Key。
    /// 若 ResourceType 为 GameObject，则进一步递归扫描目标 Prefab。
    /// </summary>
    private static void CollectResourceKeys(
        GameObject go,
        HashSet<string> visitedKeys,
        HashSet<string> visitedPrefabs,
        LevelConfig config)
    {
        var components = go.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null) continue;

            var type = comp.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.FieldType != typeof(string)) continue;

                var attr = field.GetCustomAttribute<ResourceKeyAttribute>();
                if (attr == null) continue;

                string resKey = field.GetValue(comp) as string;
                if (string.IsNullOrEmpty(resKey)) continue;

                // 去重加入
                if (visitedKeys.Add(resKey))
                {
                    AddKeyToConfig(resKey, attr.ResourceType, config);
                }

                // 若是 GameObject 类型的 Key，递归扫描目标 Prefab 的依赖
                if (attr.ResourceType == typeof(GameObject))
                {
                    RecursiveCollectFromPrefabAsset(resKey, visitedKeys, visitedPrefabs, config);
                }
            }
        }
    }

    /// <summary>
    /// 通过 prefabKey 在 Addressables 中找到对应的 Prefab Asset，
    /// 递归扫描其 Component 上的 [ResourceKey] 字段。
    /// 通过 visitedPrefabs 防止循环引用。
    /// </summary>
    private static void RecursiveCollectFromPrefabAsset(
        string prefabKey,
        HashSet<string> visitedKeys,
        HashSet<string> visitedPrefabs,
        LevelConfig config)
    {
        if (!visitedPrefabs.Add(prefabKey)) return; // 防循环引用

        // 优先从 Addressables 中查找对应的 Prefab Asset 路径
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;

        string assetPath = null;
        foreach (var group in settings.groups)
        {
            if (group == null) continue;
            foreach (var entry in group.entries)
            {
                if (entry == null) continue;
                if (entry.address == prefabKey && entry.AssetPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    assetPath = entry.AssetPath;
                    break;
                }
            }
            if (assetPath != null) break;
        }

        if (assetPath == null)
        {
            Debug.LogWarning($"[LevelExporter] RecursiveCollect: 在 Addressables 中未找到 address 为 '{prefabKey}' 的 Prefab，已跳过递归扫描。");
            return;
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
        {
            Debug.LogWarning($"[LevelExporter] RecursiveCollect: 加载 Prefab 失败，路径：{assetPath}");
            return;
        }

        // 扫描 Prefab Asset 上的所有 Component
        var components = prefab.GetComponents<Component>();
        foreach (var comp in components)
        {
            if (comp == null) continue;

            var type = comp.GetType();
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in fields)
            {
                if (field.FieldType != typeof(string)) continue;

                var attr = field.GetCustomAttribute<ResourceKeyAttribute>();
                if (attr == null) continue;

                string resKey = field.GetValue(comp) as string;
                if (string.IsNullOrEmpty(resKey)) continue;

                if (visitedKeys.Add(resKey))
                {
                    AddKeyToConfig(resKey, attr.ResourceType, config);
                }

                // 继续递归
                if (attr.ResourceType == typeof(GameObject))
                {
                    RecursiveCollectFromPrefabAsset(resKey, visitedKeys, visitedPrefabs, config);
                }
            }
        }
    }
}
