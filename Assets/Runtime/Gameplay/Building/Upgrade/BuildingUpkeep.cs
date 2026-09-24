using UnityEngine;

/// <summary>
/// 建筑维护费（哨塔用）：把当前等级的维护费用登记进经济账本，
/// 从每秒净收入中实时抵扣（收入不足时只抵扣到 0，不欠费、不动已有余额）；
/// 维护费随建筑等级变化，upkeepPerLevel 依次配置 1/2/3 级的每秒消耗。
/// 等级读取自 BuildUP.CurrentLevel；建筑被摧毁/禁用/回收时自动注销登记，收入恢复全额。
/// 经 TeamEconomy 路由：敌方建筑走对应队伍账本（防守类标记），玩家建筑走全局 Coins。
/// </summary>
[RequireComponent(typeof(BuildUP))]
public class BuildingUpkeep : MonoBehaviour
{
    [Header("维护费（每秒消耗金币，按等级 1/2/3 配置）")]
    [SerializeField, Min(0)]
    private int[] upkeepPerLevel = new int[] { 10, 20, 30 };

    private BuildUP _buildUp;
    private GameObjectProperty _prop;
    private int _registeredUpkeep = -1;   // 当前已登记进账本的维护费；-1 表示尚未同步。

    private void Awake()
    {
        _buildUp = GetComponent<BuildUP>();
        _prop = GetComponent<GameObjectProperty>();
    }

    private void OnEnable()
    {
        SyncUpkeep();
    }

    private void OnDisable()
    {
        Unregister();
    }

    private void OnDestroy()
    {
        Unregister();
    }

    private void Update()
    {
        SyncUpkeep();
    }

    /// <summary>当前等级对应的每秒维护费（0 起等级直接索引配置数组）。</summary>
    private int CurrentLevelUpkeep()
    {
        if (_buildUp == null || upkeepPerLevel == null || upkeepPerLevel.Length == 0)
            return 0;

        int level = Mathf.Clamp(_buildUp.CurrentLevel, 0,
            Mathf.Max(0, upkeepPerLevel.Length - 1));
        return Mathf.Max(0, upkeepPerLevel[level]);
    }

    /// <summary>账本是否就绪：敌方需要对应队伍指挥官，玩家需要 Coins。</summary>
    private bool IsLedgerReady()
    {
        if (_prop != null && TeamRules.IsEnemySide(_prop.side))
            return TeamCommander.GetForSide(_prop.side) != null;

        return Coins.Instance != null;
    }

    /// <summary>把当前等级维护费同步进账本（等级变化时自动按新值覆盖）；账本未就绪时下一帧重试。</summary>
    private void SyncUpkeep()
    {
        int desired = CurrentLevelUpkeep();
        if (desired == _registeredUpkeep)
            return;

        if (!IsLedgerReady())
            return;

        if (desired > 0)
            TeamEconomy.RegisterUpkeep(_prop, this, desired, isDefense: true);
        else
            TeamEconomy.UnregisterUpkeep(_prop, this);

        _registeredUpkeep = desired;
    }

    /// <summary>注销维护费登记（建筑被摧毁/禁用/回收时调用），收入恢复全额。</summary>
    private void Unregister()
    {
        TeamEconomy.UnregisterUpkeep(_prop, this);
        _registeredUpkeep = 0;
    }
}
