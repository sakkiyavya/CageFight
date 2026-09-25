using UnityEngine;

/// <summary>可升级的建筑类型（升级面板三行）。</summary>
public enum UpgradeBuildingKind
{
    Barracks = 0,
    DarkBarracks = 1,
    Sentry = 2,
}

/// <summary>可升级的魔法类型（Magic 页两行）。</summary>
public enum UpgradeMagicKind
{
    AttackMagic = 0,
    DefenseMagic = 1,
}

/// <summary>
/// 升级面板（局外养成）等级规则：
/// - 建筑升级花金条（UserGlobalInfo.GoldBarCount），消耗 = BaseCostPerLevel × 当前等级；
/// - 魔法升级花钻石（UserGlobalInfo.DiamondCount），消耗 = BaseDiamondCostPerLevel × 当前等级；
/// - 升级入口只写 UserGlobalInfo，显示与持久化由 Changed 事件驱动；
/// - 总等级 = 三个建筑等级中的最低值（UserGlobalInfo.TotalLevel）。
/// </summary>
public static class UpgradeLevelRules
{
    /// <summary>建筑每级基础金条消耗：升到 L+1 级需要 BaseCostPerLevel × L。</summary>
    public const int BaseCostPerLevel = 100;

    /// <summary>魔法每级基础钻石消耗：升到 L+1 级需要 BaseDiamondCostPerLevel × L。</summary>
    public const int BaseDiamondCostPerLevel = 100;

    /// <summary>从当前等级升到下一级所需的金条数（建筑）。</summary>
    public static int GetUpgradeCost(int currentLevel)
    {
        return Mathf.Max(1, currentLevel) * BaseCostPerLevel;
    }

    /// <summary>从当前等级升到下一级所需的钻石数（魔法）。</summary>
    public static int GetMagicUpgradeCost(int currentLevel)
    {
        return Mathf.Max(1, currentLevel) * BaseDiamondCostPerLevel;
    }

    /// <summary>
    /// 黑暗兵营建造解锁（局内规则）：本方大本营的**局内等级** ≥ 2 才可建造。
    /// 局内等级 = 大本营 BuildUP 在局内升级后的显示等级（1 级起步）。
    /// 玩家读我方大本营（StageObjectInstantiator.LastFriendlyMainBase），
    /// 敌方 AI 读本队大本营（StageGoalTracker.LastEnemyMainBase）；找不到大本营按未解锁处理。
    /// </summary>
    public static bool CanBuildDarkBarracksForSide(int side)
    {
        GameObject mainBase = TeamRules.IsEnemySide(side)
            ? StageGoalTracker.LastEnemyMainBase
            : StageObjectInstantiator.LastFriendlyMainBase;

        if (mainBase == null)
            return false;

        BuildingBase buildingBase = mainBase.GetComponent<BuildingBase>();
        return buildingBase != null && buildingBase.Level >= 2;
    }

    /// <summary>按建筑阵营解析本方大本营局内等级，判断黑暗兵营是否解锁。</summary>
    public static bool CanBuildDarkBarracks(GameObjectProperty prop)
    {
        return CanBuildDarkBarracksForSide(prop != null ? prop.side : 1);
    }

    /// <summary>读取该建筑当前等级（三个建筑至少 1 级起步；无全局信息时按 1）。</summary>
    public static int GetLevel(UpgradeBuildingKind kind)
    {
        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
            return 1;

        switch (kind)
        {
            case UpgradeBuildingKind.Barracks: return Mathf.Max(1, info.BarracksLevel);
            case UpgradeBuildingKind.DarkBarracks: return Mathf.Max(1, info.DarkBarracksLevel);
            case UpgradeBuildingKind.Sentry: return Mathf.Max(1, info.SentryTowerLevel);
            default: return 1;
        }
    }

    /// <summary>
    /// 扣除金条并把该建筑等级 +1；金条不足或全局信息缺失时返回 false（不产生任何变更）。
    /// </summary>
    public static bool TryUpgrade(UpgradeBuildingKind kind)
    {
        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
            return false;

        int level = GetLevel(kind);
        int cost = GetUpgradeCost(level);
        if (info.GoldBarCount < cost)
            return false;

        info.SetGoldBarCount(info.GoldBarCount - cost);
        switch (kind)
        {
            case UpgradeBuildingKind.Barracks:
                info.SetBarracksLevel(level + 1);
                break;
            case UpgradeBuildingKind.DarkBarracks:
                info.SetDarkBarracksLevel(level + 1);
                break;
            case UpgradeBuildingKind.Sentry:
                info.SetSentryTowerLevel(level + 1);
                break;
        }

        return true;
    }

    /// <summary>读取该魔法当前等级（攻击/防御魔法；无全局信息时按 0）。</summary>
    public static int GetMagicLevel(UpgradeMagicKind kind)
    {
        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
            return 0;

        switch (kind)
        {
            case UpgradeMagicKind.AttackMagic: return Mathf.Max(0, info.AttackMagicLevel);
            case UpgradeMagicKind.DefenseMagic: return Mathf.Max(0, info.DefenseMagicLevel);
            default: return 0;
        }
    }

    /// <summary>
    /// 扣除钻石并把对应魔法等级 +1；钻石不足或全局信息缺失时返回 false（不产生任何变更）。
    /// </summary>
    public static bool TryUpgradeMagic(UpgradeMagicKind kind)
    {
        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
            return false;

        int level = GetMagicLevel(kind);
        int cost = GetMagicUpgradeCost(level);
        if (info.DiamondCount < cost)
            return false;

        info.SetDiamondCount(info.DiamondCount - cost);
        if (kind == UpgradeMagicKind.AttackMagic)
            info.SetAttackMagicLevel(level + 1);
        else
            info.SetDefenseMagicLevel(level + 1);

        return true;
    }
}
