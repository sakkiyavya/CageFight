using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StageExporter
{
    public static void ExportStage(uint stageId, string savePath)
    {
        // 1. 获取场景中所有打上隐式标记的物体
        var markers = Object.FindObjectsOfType<StageObjectMarker>(true);
        
        StageConfig config = ScriptableObject.CreateInstance<StageConfig>();
        config.stageId = (int)stageId;
        config.objects = new List<StageObjectData>();

        StageResourceKeyCollector.ClearConfig(config);
        var resourceCollector = new StageResourceKeyCollector(config);

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

            var objData = new StageObjectData
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
            var stageComponents = go.GetComponentsInChildren<IStageComponent>(true);
            foreach (var comp in stageComponents)
            {
                ComponentData extracted = comp.ExtractData();
                if (extracted != null)
                {
                    objData.components.Add(extracted);
                }
            }

            config.objects.Add(objData);

            // 扫描关卡物品整个子层级，以及组件引用的序列化配置对象图。
            resourceCollector.CollectStageObject(go, key);
        }

        // 2. 确保目录存在
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }

        // 3. 写入 SO 资产
        string fullPath = $"{savePath}/Stage{stageId}.asset";
        
        AssetDatabase.CreateAsset(config, fullPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("导出成功",
            $"关卡 {stageId} 已成功导出到：\n{fullPath}\n" +
            $"共收集了 {config.objects.Count} 个关卡物品。\n" +
            $"共扫描到 {resourceCollector.TotalKeyCount} 个资源 Key " +
            $"(Prefab:{config.prefabs.Count}, Audio:{config.audios.Count}, " +
            $"Texture:{config.textures.Count}, AnimClip:{config.animationClips.Count}, " +
            $"AnimCtrl:{config.animatorControllers.Count}, Sprite:{config.sprites.Count})。", "确定");
    }
}
