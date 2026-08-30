using UnityEngine;

/// <summary>
/// 关卡对象实例化工具：资源加载完成后，根据 StageConfig 还原关卡实体。
/// </summary>
public static class StageObjectInstantiator
{
    #region 公开接口
    /// <summary>
    /// 根据关卡配置逐个取得已加载预制体，从对象池生成实例，恢复 Transform，
    /// 并将保存的组件数据注入到类型匹配的 <see cref="IStageComponent"/>。
    /// </summary>
    /// <param name="config">包含关卡对象、空间信息和组件数据的配置。</param>
    /// <param name="friendlyMainBasePrefabKey">当前选中种族的大本营预制体 Key；为空时不生成大本营。</param>
    /// <returns>全部必要系统有效且实例化过程未发生致命错误时返回 <see langword="true"/>。</returns>
    public static bool InstantiateStage(StageConfig config, string friendlyMainBasePrefabKey = null)
    {
        if (config == null)
        {
            Debug.LogError("[StageObjectInstantiator] StageConfig 为空，无法实例化关卡！");
            return false;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[StageObjectInstantiator] ResourceManager 未初始化！");
            return false;
        }

        if (GameObjectPool.Instance == null)
        {
            Debug.LogError("[StageObjectInstantiator] GameObjectPool 未初始化！");
            return false;
        }

        int spawnedCount = 0;                                                                 // 已成功生成的关卡对象数量。
        if (config.objects == null)
        {
            Debug.LogWarning($"[StageObjectInstantiator] 关卡 {config.stageId} 没有可实例化的对象。");
        }
        else
        {
            foreach (var objData in config.objects)
            {
                if (objData == null || string.IsNullOrEmpty(objData.prefabKey))
                    continue;

                GameObject prefab = ResourceManager.Instance.GetGameObject(objData.prefabKey);    // 当前关卡对象使用的已加载预制体。
                if (prefab == null)
                {
                    Debug.LogWarning($"[StageObjectInstantiator] 找不到预制体 Key：{objData.prefabKey}，跳过该对象。");
                    continue;
                }

                GameObject instance = GameObjectPool.Instance.Get(prefab);                        // 从对象池取得的关卡实例。
                if (instance == null)
                {
                    Debug.LogError($"[StageObjectInstantiator] 对象池无法生成预制体：{objData.prefabKey}");
                    return false;
                }

                instance.transform.position = objData.transform.position;
                instance.transform.eulerAngles = objData.transform.rotation;
                instance.transform.localScale = objData.transform.scale;

                var stageComponents = instance.GetComponentsInChildren<IStageComponent>(true);
                if (objData.components != null)
                {
                    foreach (var savedData in objData.components)
                    {
                        if (savedData == null)
                            continue;

                        foreach (var component in stageComponents)
                        {
                            if (component.DataType == savedData.GetType())
                            {
                                component.ApplyData(savedData);
                                break;
                            }
                        }
                    }
                }

                spawnedCount++;
            }
        }

        if (!TryInstantiateFriendlyMainBase(config, friendlyMainBasePrefabKey, ref spawnedCount))
            return false;

        Debug.Log($"[StageObjectInstantiator] 关卡 {config.stageId} 实例化完成，共生成 {spawnedCount} 个对象。");
        return true;
    }

    /// <summary>
    /// 在启用了坐标配置的关卡中，按大本营实际占地尺寸把其左下网格基准点换算为预制体中心位置。
    /// 旧 StageConfig 缺少该字段时开关默认为 false，因此会保持原有不生成行为。
    /// </summary>
    private static bool TryInstantiateFriendlyMainBase(
        StageConfig config,
        string prefabKey,
        ref int spawnedCount)
    {
        if (!config.hasFriendlyMainBaseGridPosition)
            return true;

        if (string.IsNullOrWhiteSpace(prefabKey))
        {
            Debug.LogWarning(
                $"[StageObjectInstantiator] 关卡 {config.stageId} 配置了己方大本营位置，" +
                "但当前种族未配置大本营预制体 Key，已跳过生成。");
            return true;
        }

        GameObject prefab = ResourceManager.Instance.GetGameObject(prefabKey);
        if (prefab == null)
        {
            Debug.LogError(
                $"[StageObjectInstantiator] 己方大本营预制体 '{prefabKey}' 未预载，无法生成。\n" +
                "请确认 LoadoutSelectionOption.prefabKey 已登记到 PrefabRegistry。");
            return false;
        }

        GameObject instance = GameObjectPool.Instance.Get(prefab);
        if (instance == null)
        {
            Debug.LogError($"[StageObjectInstantiator] 对象池无法生成己方大本营：{prefabKey}");
            return false;
        }

        GameObjectProperty property = instance.GetComponent<GameObjectProperty>();
        Vector2Int occupySpace = property != null
            ? new Vector2Int(Mathf.Max(1, property.occupySpace.x), Mathf.Max(1, property.occupySpace.y))
            : Vector2Int.one;
        Vector2Int gridBase = config.friendlyMainBaseGridPosition;
        instance.transform.position = new Vector3(
            gridBase.x + (occupySpace.x - 1) * .5f,
            gridBase.y + (occupySpace.y - 1) * .5f,
            prefab.transform.position.z);
        instance.transform.eulerAngles = prefab.transform.eulerAngles;
        instance.transform.localScale = prefab.transform.localScale;

        // 对象池会先激活再返回实例；这里主动刷新一次，以移除激活瞬间的旧位置占用并登记正确网格。
        BuildingBase building = instance.GetComponent<BuildingBase>();
        if (building != null)
            building.RefreshOccupancy();

        spawnedCount++;
        return true;
    }
    #endregion
}
