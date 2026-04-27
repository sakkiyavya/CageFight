using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LevelExporter
{
    public static void ExportLevel(uint levelId, string savePath)
    {
        // 1. 获取场景中所有打上隐式标记的物体
        var markers = Object.FindObjectsOfType<LevelObjectMarker>(true);
        
        LevelConfig config = ScriptableObject.CreateInstance<LevelConfig>();
        config.levelId = (int)levelId;
        config.objects = new List<LevelObjectData>();

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

        EditorUtility.DisplayDialog("导出成功", $"关卡 {levelId} 已成功导出到：\n{fullPath}\n共收集了 {config.objects.Count} 个关卡物品。", "确定");
    }
}
