using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StageExporter
{
    public static void ExportStage(uint stageId, string savePath,
        UserGlobalInfo.StageType stageType, float defenseTime, Sprite icon)
    {
        // 在创建或覆盖资产前验证唯一性，失败时保留已有配置。
        if (!TryGetFriendlyMainBase(out FriendlyMainBaseMarker mainBase)) return;

        // 1. 获取场景中所有打上隐式标记的物体
        var markers = Object.FindObjectsOfType<StageObjectMarker>(true);
        
        StageConfig config = ScriptableObject.CreateInstance<StageConfig>();
        config.stageId = (int)stageId;
        config.stageType = stageType;
        config.DefenseTime = Mathf.Max(0f, defenseTime);
        config.icon = icon;
        config.hasFriendlyMainBaseGridPosition = mainBase != null;
        if (mainBase != null)
        {
            GameObjectProperty property = mainBase.GetComponent<GameObjectProperty>();
            Vector2Int size = property != null ? property.occupySpace : Vector2Int.one;
            Vector3 position = mainBase.transform.position;
            // 与 BuildingBase 的左下网格坐标计算一致。
            config.friendlyMainBaseGridPosition = new Vector2Int(
                (int)(position.x - Mathf.Max(1, size.x) / 2f + .5f),
                (int)(position.y - Mathf.Max(1, size.y) / 2f + .5f));
        }
        config.objects = new List<StageObjectData>();

        StageResourceKeyCollector.ClearConfig(config);
        var resourceCollector = new StageResourceKeyCollector(config);

        int autoInstanceId = 1000; // 实例 ID 自增起点

        foreach (var marker in markers)
        {
            GameObject go = marker.gameObject;
            // 大本营仅提供位置，避免与运行时按种族生成的大本营重复。
            if (go.GetComponentInParent<FriendlyMainBaseMarker>(true) != null) continue;
            
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

    private static bool TryGetFriendlyMainBase(out FriendlyMainBaseMarker mainBase)
    {
        mainBase = null;
        var markers = new List<FriendlyMainBaseMarker>();
        foreach (var marker in Object.FindObjectsOfType<FriendlyMainBaseMarker>(true))
        {
            if (marker.gameObject.scene.IsValid() && marker.gameObject.scene.isLoaded
                && PrefabStageUtility.GetPrefabStage(marker.gameObject) == null)
                markers.Add(marker);
        }

        if (markers.Count == 0) return true;
        bool valid = markers.Count == 1
            && PrefabUtility.IsOutermostPrefabInstanceRoot(markers[0].gameObject);
        if (!valid)
        {
            var message = new StringBuilder(markers.Count > 1
                ? "检测到多个己方大本营标记，每个关卡只能标记一个。请取消多余标记后重新构建：\n"
                : "己方大本营必须标记在场景中的最外层预制体根对象上，请修正以下对象：\n");
            var selection = new List<Object>();
            foreach (var marker in markers)
            {
                string path = marker.name + "[" + marker.transform.GetSiblingIndex() + "]";
                for (Transform parent = marker.transform.parent; parent != null; parent = parent.parent)
                    path = parent.name + "[" + parent.GetSiblingIndex() + "]/" + path;
                string scene = string.IsNullOrEmpty(marker.gameObject.scene.path)
                    ? marker.gameObject.scene.name : marker.gameObject.scene.path;
                string location = scene + ": " + path;
                message.AppendLine(location);
                Debug.LogError("[关卡构建] 己方大本营标记：" + location, marker.gameObject);
                selection.Add(marker.gameObject);
            }
            Selection.objects = selection.ToArray();
            EditorUtility.DisplayDialog("关卡构建失败", message.ToString(), "确定");
            return false;
        }

        mainBase = markers[0];
        return true;
    }
}
