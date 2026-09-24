using UnityEngine;

/// <summary>
/// 关卡对象实例化工具：资源加载完成后，根据 StageConfig 还原关卡实体。
/// </summary>
public static class StageObjectInstantiator
{
    /// <summary>本局最近生成的我方大本营实例（供胜负判定等读取；未生成时为 null）。</summary>
    public static GameObject LastFriendlyMainBase { get; private set; }

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

                // 图鉴“遇见”标记：关卡实例化出该兵种即视为玩家遇见过。
                BookProgress.MarkEncountered(objData.prefabKey);

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

                // 敌方对象（偶数队）注入本关敌方等级：建筑按类型缩放，单位按兵营等级缩放，Buff 等级按魔法等级。
                ApplyEnemyLevels(instance, config);

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

        // 优先从所选种族建筑表解析大本营（种族→建筑统一绑定入口）；
        // 种族未配置 MainBase 条目时回落选装按钮携带的 Key（旧绑定）。
        prefabKey = ResolveRaceMainBaseKey(prefabKey);

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
                "请确认该 Key 已登记到 PrefabRegistry（种族资产 Buildings 的 MainBase 条目或 LoadoutSelectionOption.prefabKey）。");
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

        // 玩家大本营：固定队伍 1 + 玩家等级上下文（与 BuildingPlace 放置注入一致）。
        // 缺失时大本营沿用预制体默认阵营（0/偶数会被队伍规则判为敌方：朝向翻转、与己方单位互为敌对）。
        if (property != null)
        {
            property.side = 1;
            if (UserGlobalInfo.Instance != null)
            {
                property.defenseMagicLevel = Mathf.Max(1, UserGlobalInfo.Instance.DefenseMagicLevel);
                property.attackMagicLevel = Mathf.Max(1, UserGlobalInfo.Instance.AttackMagicLevel);
                property.barracksLevel = Mathf.Max(1, UserGlobalInfo.Instance.BarracksLevel);
                property.darkBarracksLevel = Mathf.Max(1, UserGlobalInfo.Instance.DarkBarracksLevel);
                property.sentryTowerLevel = Mathf.Max(1, UserGlobalInfo.Instance.SentryTowerLevel);
            }
        }

        // 重算建筑统计：确保等级缩放（若有）与 1 级属性在注入后生效。
        BuildUP buildUp = instance.GetComponent<BuildUP>();
        if (buildUp != null)
            buildUp.RefreshLevelScale();

        // 大本营没有施工流程，出生即满血：
        // 否则 BuildingHealth.hp 保持 0（血条为空，且 hp<=0 会被判为已死亡、无法受击）。
        BuildingHealth health = instance.GetComponent<BuildingHealth>();
        if (health != null)
            health.SetPercentHp(1f);

        // 对象池会先激活再返回实例；这里主动刷新一次，以移除激活瞬间的旧位置占用并登记正确网格。
        BuildingBase building = instance.GetComponent<BuildingBase>();
        if (building != null)
            building.RefreshOccupancy();

        LastFriendlyMainBase = instance;   // 记录本局我方大本营（胜负判定读取）。

        spawnedCount++;
        return true;
    }

    /// <summary>
    /// 从所选种族的建筑表中解析大本营预制体 Key（BuildingType.MainBase 条目）；
    /// 种族未配置该条目时返回传入的默认 Key（选装按钮携带的旧绑定）。
    /// </summary>
    private static string ResolveRaceMainBaseKey(string fallbackKey)
    {
        if (UserGlobalInfo.Instance == null || ResourceManager.Instance == null)
            return fallbackKey;

        string raceId = UserGlobalInfo.Instance.SelectedRaceId;
        if (string.IsNullOrEmpty(raceId) ||
            !ResourceManager.Instance.TryGetRace(raceId, out RaceDefinition race) ||
            race == null || race.Buildings == null)
            return fallbackKey;

        for (int i = 0; i < race.Buildings.Count; i++)
        {
            RaceBuildingEntry entry = race.Buildings[i];
            if (entry != null && entry.buildingType == BuildingType.MainBase &&
                !string.IsNullOrEmpty(entry.prefabKey))
                return entry.prefabKey;
        }

        return fallbackKey;
    }

    /// <summary>
    /// 为敌方对象（偶数队）注入本关敌方等级：
    /// 建筑 → 哨塔用哨塔等级、兵营用兵营/黑暗兵营等级（经 BuildUP.RefreshLevelScale 缩放统计）；
    /// 单位 → 按兵营等级缩放生命/攻击/魔攻（1.1^(L-1)）；
    /// 所有敌方对象 → 防御/攻击魔法等级注入 Buff 等级上下文。奇数队（友方）不处理。
    /// 敌方 AI 建造/产出的对象也复用本入口（先设好 side 再调用）。
    /// </summary>
    public static void ApplyEnemyLevels(GameObject instance, StageConfig config)
    {
        if (instance == null || config == null)
            return;

        GameObjectProperty prop = instance.GetComponent<GameObjectProperty>();
        if (prop == null || prop.side % 2 != 0)
            return;

        // Buff 等级上下文：敌方防御/攻击魔法等级（= 敌方 Buff 等级）。
        prop.defenseMagicLevel = Mathf.Max(1, config.enemyDefenseMagicLevel);
        prop.attackMagicLevel = Mathf.Max(1, config.enemyAttackMagicLevel);

        if (instance.GetComponent<BuildingBase>() != null)
        {
            // 建筑：注入对应等级上下文，再经 BuildUP 统一重算统计（含 1.1 缩放与局内升级叠加）。
            if (instance.GetComponent<BuildingTowerAI>() != null)
            {
                prop.sentryTowerLevel = Mathf.Max(1, config.enemySentryTowerLevel);
            }
            else
            {
                BuildingTraining training = instance.GetComponent<BuildingTraining>();
                if (training != null)
                {
                    int level = training.IsDarkBarracks
                        ? config.enemyDarkBarracksLevel
                        : config.enemyBarracksLevel;
                    prop.barracksLevel = Mathf.Max(1, level);
                }
            }

            BuildUP buildUp = instance.GetComponent<BuildUP>();
            if (buildUp != null)
                buildUp.RefreshLevelScale();

            // 关卡预摆建筑没有施工流程，出生即满血（否则血量为 0、被判定为已死亡且无法受击）。
            BuildingHealth enemyHealth = instance.GetComponent<BuildingHealth>();
            if (enemyHealth != null)
                enemyHealth.SetPercentHp(1f);
        }
        else
        {
            // 单位：按普通兵营等级缩放（黑暗兵种接入后按黑暗兵营等级）。
            // ApplyLevelScale 内部经 CharacterHealth 受控 API 同步满血。
            int level = Mathf.Max(1, config.enemyBarracksLevel);
            prop.barracksLevel = level;
            prop.ApplyLevelScale(level);
        }
    }
    #endregion
}
