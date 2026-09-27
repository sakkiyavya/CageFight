using System.Collections.Generic;
using UnityEngine;

/// <summary>全局规则 ID（选关界面开启，本局全程生效）。</summary>
public enum GlobalRuleId
{
    None = 0,
    Strengthen1 = 1,   // 本局起始每秒获取金币 +80。
    Strengthen2 = 2,   // 本局友方建筑局外等级 +2（兵营/黑暗兵营/哨塔/大本营）。
}

/// <summary>
/// 全局规则管理器：选关界面开启的规则在下一局全程生效。
/// 选择状态存 UserGlobalInfo 的临时字段（不进存档，仅本局）；
/// 效果应用：Strengthen1 在 GameplayState 开局时加成每秒金币；
/// Strengthen2 在友方建筑等级注入点（BuildingPlace / StageObjectInstantiator）加局外等级。
/// 局内图鉴旁显示规则图标。
/// </summary>
public static class GlobalRuleManager
{
    public const int Strengthen1BonusIncome = 80;   // Strengthen1：本局每秒金币加成。
    public const int Strengthen2BonusLevel = 2;     // Strengthen2：友方建筑局外等级加成。

    /// <summary>Strengthen1（金币强化）的效果字幕。</summary>
    public const string Strengthen1Description = "本局起始每秒获取金币 +80。";

    /// <summary>Strengthen2（建筑等级强化）的效果字幕。</summary>
    public const string Strengthen2Description = "本局友方建筑局外等级 +2。";

    /// <summary>当前选中的全局规则位掩码（可多规则并存）。</summary>
    public static int SelectedMask
    {
        get
        {
            UserGlobalInfo info = UserGlobalInfo.Instance;
            return info != null ? info.SelectedGlobalRuleMask : 0;
        }
    }

    /// <summary>该规则是否为本局选中。</summary>
    public static bool IsEnabled(GlobalRuleId id) => id != GlobalRuleId.None && (SelectedMask & (1 << (int)id)) != 0;

    /// <summary>切换规则：再点一次同一规则则关闭（可多选并存）。</summary>
    public static void Toggle(GlobalRuleId id)
    {
        if (id == GlobalRuleId.None)
            return;

        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
            return;

        info.SetSelectedGlobalRuleMask(SelectedMask ^ (1 << (int)id));
    }

    /// <summary>规则的图标精灵键（SpriteRegistry；与选关按钮同素材）。</summary>
    public static string GetIconKey(GlobalRuleId id)
    {
        switch (id)
        {
            case GlobalRuleId.Strengthen1: return "Radiate Energy_1";
            case GlobalRuleId.Strengthen2: return "Radiate Energy_0";
            default: return string.Empty;
        }
    }

    /// <summary>敌方血量加强（负面规则）的效果实现键与效果参数（参数由开发在代码中维护）。</summary>
    public const string EnemyHpBoostRuleId = "enemyHpBoost";
    public const float EnemyHpBoostPercent = 30f;

    /// <summary>全局兵种移速（中立规则 All run）的效果实现键与效果参数。</summary>
    public const string AllRunSpeedRuleId = "allRunSpeed";
    public const float AllRunSpeedBonus = 0.5f;

    /// <summary>大师难度：本局敌方等级额外增加的级数（兵营/黑暗兵营/哨塔/魔法等级与兵种缩放统一生效）。</summary>
    public const int MasterModeEnemyLevelBonus = 2;

    /// <summary>规则池的 Addressables 固定地址（由 GlobalRulePoolAutoSync 登记）。</summary>
    public const string PoolAddress = "GlobalRulePool";

    private static GlobalRulePool _masterRulePool;
    private static readonly List<GlobalRuleDefinition> _masterExtraRules = new List<GlobalRuleDefinition>();
    private static StageConfig _masterRulesStage;   // 已为哪一关处理过抽取（幂等防重复抽取）。

    /// <summary>大师难度是否开启（选关界面 Difficult 开关）。</summary>
    public static bool IsMasterMode =>
        UserGlobalInfo.Instance != null && UserGlobalInfo.Instance.MasterModeEnabled;

    /// <summary>大师难度下本局敌方等级加成（未开启时为 0）。</summary>
    public static int EnemyLevelBonus => IsMasterMode ? MasterModeEnemyLevelBonus : 0;

    /// <summary>本局大师难度随机抽取出的追加规则（开局时生成；局内图标行与效果共用）。</summary>
    public static IReadOnlyList<GlobalRuleDefinition> MasterExtraRules => _masterExtraRules;

    /// <summary>当前注入的规则池（异步加载完成前为 null）。</summary>
    public static GlobalRulePool MasterRulePool => _masterRulePool;

    /// <summary>注册大师难度随机抽取使用的规则池（由选关界面的难度开关组件注入）。</summary>
    public static void SetMasterRulePool(GlobalRulePool pool)
    {
        _masterRulePool = pool;
    }

    /// <summary>
    /// 按需加载规则池（幂等）：编辑器下经 ResourceManager 直取路径同步完成；
    /// 玩家构建经 Addressables 固定地址异步完成（回调注入）。
    /// 由抽取逻辑与难度开关共同调用，不依赖任何单点的加载时序。
    /// </summary>
    public static void LoadMasterRulePool()
    {
        if (_masterRulePool != null || ResourceManager.Instance == null)
            return;

        ResourceManager.Instance.LoadExtraResourceAsync<GlobalRulePool>(PoolAddress, pool =>
        {
            if (pool != null)
                _masterRulePool = pool;
        });
    }

    /// <summary>
    /// 本局大师难度随机规则抽取（幂等：同一关只抽取一次）。
    /// 在 BeginStageLoad（加载/出生前）、GameplayState.OnEnter（进入局内）与局内图标行刷新前
    /// 三处调用兜底，任何进入局内的路径都保证完成抽取。
    /// 大师难度开启时：从规则池随机抽取 1 个负面规则 + 1 个中立规则加入本局；
    /// 未开启或对应类别无规则时跳过。
    /// </summary>
    public static void PrepareMasterRules(StageConfig config)
    {
        if (config != null && _masterRulesStage == config &&
            (_masterExtraRules.Count > 0 || !IsMasterMode))
        {
            return;   // 本关已抽取过（或已确认大师模式未开启）。
        }

        _masterRulesStage = config;
        _masterExtraRules.Clear();
        if (!IsMasterMode)
            return;

        // 池未到位时先请求一次加载（编辑器直取路径同步完成；异步路径由后续重试兜底）。
        if (_masterRulePool == null)
            LoadMasterRulePool();

        if (_masterRulePool == null || _masterRulePool.Rules == null)
        {
            Debug.LogWarning("[GlobalRuleManager] 大师难度已开启但规则池尚未加载完成（Addressables 地址 GlobalRulePool）。");
            return;
        }

        // 按类别收集候选（不用 LINQ，保持零分配）。
        var negatives = new List<GlobalRuleDefinition>();
        var neutrals = new List<GlobalRuleDefinition>();
        for (int i = 0; i < _masterRulePool.Rules.Count; i++)
        {
            GlobalRuleDefinition rule = _masterRulePool.Rules[i];
            if (rule == null)
                continue;

            if (rule.Category == GlobalRuleCategory.Negative)
                negatives.Add(rule);
            else if (rule.Category == GlobalRuleCategory.Neutral)
                neutrals.Add(rule);
        }

        if (negatives.Count > 0)
            _masterExtraRules.Add(negatives[Random.Range(0, negatives.Count)]);
        if (neutrals.Count > 0)
            _masterExtraRules.Add(neutrals[Random.Range(0, neutrals.Count)]);

        Debug.Log(
            $"[GlobalRuleManager] 大师难度规则抽取完成：负面候选 {negatives.Count} 条、中立候选 {neutrals.Count} 条，" +
            $"本局追加 {_masterExtraRules.Count} 条。");
    }

    /// <summary>该关卡（配置规则 + 大师难度随机追加规则）是否生效指定 ruleId 的全局规则。</summary>
    public static bool HasRule(StageConfig config, string ruleId)
    {
        if (string.IsNullOrEmpty(ruleId))
            return false;

        if (config != null && config.GlobalRules != null)
        {
            for (int i = 0; i < config.GlobalRules.Count; i++)
            {
                GlobalRuleDefinition rule = config.GlobalRules[i];
                if (rule != null && rule.RuleId == ruleId)
                    return true;
            }
        }

        for (int i = 0; i < _masterExtraRules.Count; i++)
        {
            GlobalRuleDefinition rule = _masterExtraRules[i];
            if (rule != null && rule.RuleId == ruleId)
                return true;
        }

        return false;
    }

    /// <summary>敌方血量加强：本局所有敌方建筑与单位血量额外增加的百分比（未配置该规则时返回 0）。</summary>
    public static float GetEnemyHpBonusPercent(StageConfig config)
    {
        return HasRule(config, EnemyHpBoostRuleId) ? EnemyHpBoostPercent : 0f;
    }

    /// <summary>
    /// 关卡全局规则：敌方血量加强 —— 本局所有敌方建筑与单位血量额外增加。
    /// 仅处理偶数队（敌方）；经受控 API（CharacterHealth/BuildingHealth.SetMaxHp）写入并补满血。
    /// 所有敌方出生路径统一调用：预摆对象 / 敌方 AI 建造 / 训练产出 / 敌方大本营 / Boss。
    /// </summary>
    public static void ApplyEnemyHpBonus(GameObject instance, StageConfig config)
    {
        float percent = GetEnemyHpBonusPercent(config);
        if (percent <= 0f || instance == null)
            return;

        GameObjectProperty prop = instance.GetComponent<GameObjectProperty>();
        if (prop == null || prop.maxHp <= 0 || prop.side % 2 != 0)
            return;

        int newMaxHp = Mathf.Max(1, Mathf.RoundToInt(prop.maxHp * (1f + percent * 0.01f)));

        CharacterHealth characterHealth = instance.GetComponent<CharacterHealth>();
        if (characterHealth != null)
        {
            characterHealth.SetMaxHp(newMaxHp);
            characterHealth.SetPercentHp(1f);   // SetMaxHp 会压缩当前血量，这里补满。
            return;
        }

        BuildingHealth buildingHealth = instance.GetComponent<BuildingHealth>();
        if (buildingHealth != null)
        {
            buildingHealth.SetMaxHp(newMaxHp);
            buildingHealth.SetPercentHp(1f);    // 同上，补满。
        }
    }

    /// <summary>
    /// 关卡全局规则（中立 All run）：所有兵种单位移动速度 +0.5。
    /// 在兵种出生注入点统一调用（兵营训练产出 / 预摆与 AI 敌方单位），
    /// 直接写入 GameObjectProperty.moveSpeed（基础运行时数据归属）。
    /// </summary>
    public static void ApplyTroopMoveSpeedBonus(GameObjectProperty prop, StageConfig config)
    {
        if (prop == null || !HasRule(config, AllRunSpeedRuleId))
            return;

        prop.moveSpeed += AllRunSpeedBonus;
    }
}
