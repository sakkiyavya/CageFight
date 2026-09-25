using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌方队伍指挥官（阶段 3）：与玩家同一套规则——只做“建造 + 生产”两类决策，
/// 不添加任何单位指令（单位只进不退，防守靠补塔 + 新兵就近迎敌）。
/// 经济：队伍独立账本（起始金币 + 增长曲线 − 维护费）；
/// 建造：按阵地槽位顺序 + 预算与合法性校验（复用玩家放置/施工流程）；
/// 生产：预算驱动开训/停训/转产（波次 = 同帧同步开训）；
/// 反应：阵地半径内玩家威胁值超阈值进入回防态（优先补哨塔、前线兵营优先生产）。
/// 由 GameplayState 进入局内时按 StageConfig.enemyTeams 逐队 Configure。
/// </summary>
[DisallowMultipleComponent]
public sealed class TeamCommander : MonoBehaviour
{
    private const float TickInterval = 0.5f;
    private const float DarkBarracksChance = 0.4f;   // 扩兵营时选择黑暗兵营的概率（普通/黑暗混搭）。

    private static readonly Dictionary<int, TeamCommander> Commanders = new Dictionary<int, TeamCommander>();

    private EnemyTeamConfig _config;
    private StageConfig _stageConfig;
    private float _startTime;

    private float _gold;
    private readonly Dictionary<object, int> _upkeepSources = new Dictionary<object, int>();
    private int _totalUpkeep;
    private int _defenseUpkeep;

    private readonly List<BuildingBase> _buildings = new List<BuildingBase>();
    private readonly List<BuildingTraining> _barracks = new List<BuildingTraining>();
    private readonly List<BuildingBase> _pending = new List<BuildingBase>();
    private readonly HashSet<int> _builtOrSkippedSlots = new HashSet<int>();
    private readonly HashSet<int> _warnedInvalidSlots = new HashSet<int>();   // 已警告过的持续非法槽位（一次性日志）。
    private readonly List<int> _slotOrder = new List<int>();   // 本局槽位建造顺序（可选随机打乱）。
    private float _nextBuildTime;                              // 下一次允许开工的时间（随机间隔）。
    private int _retrySlot = -1;
    private float _retryTime;
    private float _nextTick;
    private float _surplusSince = -1f;   // 净收入盈余的持续起点（-1 = 无盈余）。
    private float _nextUpgradeTime;      // 下一次评估建筑升级的时间。
    private float _nextRotationTime;     // 下一次评估兵种转产的时间。
    private BuildingBase _mainBase;      // 本队大本营（懒绑定；升级到 2 级解锁黑暗兵营）。
    private bool _defending;
    private bool _warnedRace;

    public int TeamId { get; private set; } = -1;
    public bool IsConfigured => _config != null;

    public int TotalUpkeep => _totalUpkeep;
    public int DefenseUpkeep => _defenseUpkeep;

    /// <summary>当前曲线毛产量（每秒金币）。</summary>
    public int CurrentCoinPerSec => ComputeCurveIncome(LevelTime);

    /// <summary>每秒净产量（毛产量 − 维护费，最小 0）。</summary>
    public int NetCoinPerSec => Mathf.Max(0, CurrentCoinPerSec - _totalUpkeep);

    private float LevelTime => Time.time - _startTime;

    /// <summary>按队伍号查询敌方指挥官（经济路由用）。</summary>
    public static TeamCommander GetForSide(int side)
    {
        return Commanders.TryGetValue(side, out TeamCommander commander) ? commander : null;
    }

    /// <summary>配置本指挥官（进入局内时由 GameplayState 调用）；config 为空则停用。</summary>
    public void Configure(EnemyTeamConfig config, StageConfig stageConfig)
    {
        ClearRound();

        _config = config;
        _stageConfig = stageConfig;
        if (config == null || stageConfig == null)
        {
            enabled = false;
            return;
        }

        TeamId = Mathf.Max(2, config.teamId);
        enabled = true;
        _startTime = Time.time;
        _gold = Mathf.Max(0, config.startGold);
        _nextTick = Time.time;
        _retrySlot = -1;
        _warnedRace = false;

        // 建造随机性：可选打乱槽位顺序（每局不同），并给首次开工一个随机延迟。
        _slotOrder.Clear();
        if (config.buildSlots != null)
        {
            for (int i = 0; i < config.buildSlots.Count; i++)
                _slotOrder.Add(i);

            if (config.randomizeBuildOrder && _slotOrder.Count > 1)
            {
                for (int i = _slotOrder.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    int tmp = _slotOrder[i];
                    _slotOrder[i] = _slotOrder[j];
                    _slotOrder[j] = tmp;
                }
            }
        }
        _nextBuildTime = Time.time + UnityEngine.Random.Range(0.5f, 3f);
        _nextUpgradeTime = Time.time + UnityEngine.Random.Range(8f, 15f);
        _nextRotationTime = Time.time + UnityEngine.Random.Range(10f, 20f);

        Commanders[TeamId] = this;
    }

    private void OnDisable()
    {
        if (TeamId >= 0 && Commanders.TryGetValue(TeamId, out TeamCommander existing) && existing == this)
            Commanders.Remove(TeamId);
        ClearRound();
    }

    private void ClearRound()
    {
        _upkeepSources.Clear();
        _totalUpkeep = 0;
        _defenseUpkeep = 0;
        _buildings.Clear();
        _barracks.Clear();
        _pending.Clear();
        _builtOrSkippedSlots.Clear();
        _warnedInvalidSlots.Clear();
        _slotOrder.Clear();
        _mainBase = null;
        _defending = false;
    }

    private void Update()
    {
        if (!IsConfigured || Time.time < _nextTick)
            return;

        _nextTick = Time.time + TickInterval;
        Tick();
    }

    #region 经济账本
    /// <summary>登记维护费（训练/建筑维护）；供 TeamEconomy 路由调用。</summary>
    public bool RegisterUpkeep(object source, int amount, bool isDefense = false)
    {
        if (source == null)
            return false;
        if (amount <= 0)
            return UnregisterUpkeep(source);

        if (_upkeepSources.TryGetValue(source, out int old))
        {
            if (old == amount)
                return false;

            _totalUpkeep += amount - old;
            if (isDefense)
                _defenseUpkeep = Mathf.Max(0, _defenseUpkeep + amount - old);
            _upkeepSources[source] = amount;
            return true;
        }

        _upkeepSources.Add(source, amount);
        _totalUpkeep += amount;
        if (isDefense)
            _defenseUpkeep += amount;
        return true;
    }

    /// <summary>注销维护费。</summary>
    public bool UnregisterUpkeep(object source)
    {
        if (source == null || !_upkeepSources.TryGetValue(source, out int amount))
            return false;

        _upkeepSources.Remove(source);
        _totalUpkeep -= amount;
        _defenseUpkeep = Mathf.Max(0, _defenseUpkeep - amount);
        return true;
    }

    /// <summary>预算判定：新增 amount 维护费后净收入仍 ≥ reserve；防守类还受 defenseSpendRatio 上限。</summary>
    public bool CanOpenUpkeep(int amount, bool isDefense)
    {
        if (_config == null)
            return false;

        int newTotal = _totalUpkeep + amount;
        if (CurrentCoinPerSec - newTotal < Mathf.Max(0, _config.reserve))
            return false;

        if (isDefense)
        {
            int newNet = Mathf.Max(0, CurrentCoinPerSec - newTotal);
            if (_defenseUpkeep + amount > newNet * Mathf.Clamp01(_config.defenseSpendRatio) + 1f)
                return false;
        }

        return true;
    }

    /// <summary>按本队增长曲线计算 t 时刻的每秒产量（基础值 + 各阶段已触发步数 × 每步增量）。</summary>
    private int ComputeCurveIncome(float t)
    {
        if (_config == null)
            return 0;

        float income = Mathf.Max(0f, _config.baseGoldPerSecond);
        if (_config.incomeGrowth != null)
        {
            for (int i = 0; i < _config.incomeGrowth.Count; i++)
            {
                IncomeGrowthPhase phase = _config.incomeGrowth[i];
                if (phase == null || t <= phase.startTime)
                    continue;

                float windowEnd = phase.endTime > phase.startTime ? Mathf.Min(t, phase.endTime) : t;
                float span = Mathf.Max(0f, windowEnd - phase.startTime);
                float interval = Mathf.Max(0.1f, phase.stepInterval);
                int steps = Mathf.FloorToInt(span / interval);
                income += steps * Mathf.Max(0f, phase.stepAmount);
            }
        }

        return Mathf.Max(0, Mathf.RoundToInt(income));
    }
    #endregion

    #region 决策
    private void Tick()
    {
        // 1. 结算收入（0.5 秒一档），并维护“盈余持续”计时（用于扩张判定）。
        _gold += NetCoinPerSec * TickInterval;
        if (NetCoinPerSec > 0)
        {
            if (_surplusSince < 0f)
                _surplusSince = Time.time;
        }
        else
        {
            _surplusSince = -1f;
        }

        // 2. 清理失效对象，把完工建筑转入活跃列表。
        Sweep();

        // 3. 一次扫描同时得到阵地威胁值与玩家主力定位（避免重复遍历网格）。
        EvaluatePlayerPresence(out float threat, out TroopTag dominantPlayerTag);
        _defending = _config != null && threat >= _config.threatThreshold;

        // 4. 经济驱动升级：金库充足时升级已有建筑（解锁高阶兵种 + 建筑属性提升），
        //    不再“只攒钱不升级”；回防时优先哨塔，平时优先兵营。
        TryUpgradeBuildings();

        // 5. 生产优先：先让完工兵营开训（训练消耗净收入、优先于盖新房），
        //    按“克制主力 > 兵种多样性 > 随机”选择兵种；同一 tick 同步开训 = 天然波次。
        UpdateTraining(dominantPlayerTag);

        // 6. 兵种轮换：玩家主力定位变化或周期到点时，把非克制兵营转产为克制兵种。
        RotateTroops(dominantPlayerTag);

        // 7. 建造：按经济状态决策（生产饱和且未达上限 → 扩兵营；兵营满或富余 → 补哨塔；
        //    槽位表用完后在阵地锚点附近动态找位扩张）。
        TryBuildNextSlot();
    }

    /// <summary>盈余是否已持续达到配置时长（扩张新生产线用；第一条生产线不受此限制）。</summary>
    private bool HasSurplus()
    {
        if (_config == null || _config.surplusSeconds <= 0f)
            return true;

        return _surplusSince >= 0f && Time.time - _surplusSince >= _config.surplusSeconds;
    }

    /// <summary>存款是否富余：达到剩余建筑中最低建造费的 3 倍（无剩余槽位时按 100 计）。</summary>
    private bool IsRich()
    {
        int cost = CheapestRemainingBuildCost();
        if (cost <= 0)
            cost = 100;

        return _gold >= cost * 3;
    }

    /// <summary>剩余未建槽位中的最低建造费用；无剩余时返回 0。</summary>
    private int CheapestRemainingBuildCost()
    {
        if (_config == null || _config.buildSlots == null)
            return 0;

        int best = int.MaxValue;
        bool found = false;
        for (int i = 0; i < _config.buildSlots.Count; i++)
        {
            if (_builtOrSkippedSlots.Contains(i))
                continue;

            EnemyBuildSlot slot = _config.buildSlots[i];
            if (slot == null)
                continue;

            GameObject prefab = BuildingButton.TryResolveBuilding(_config.raceId, slot.buildingType);
            if (prefab == null)
                continue;

            int cost = GetBuildCost(prefab);
            if (cost < best)
            {
                best = cost;
                found = true;
            }
        }

        return found ? best : 0;
    }

    /// <summary>
    /// 建筑区域规则：建筑只能修建在敌我大本营中点往右（敌方半场）。
    /// 中点 = 我方大本营 X 与敌方大本营 X（friendlyX + enemyBaseDistance）的中点。
    /// </summary>
    private int MinBuildX
    {
        get
        {
            if (_stageConfig == null)
                return int.MinValue;

            int friendlyX = _stageConfig.friendlyMainBaseGridPosition.x;
            int enemyX = friendlyX + Mathf.Max(1, _stageConfig.enemyBaseDistance);
            return (friendlyX + enemyX) / 2;
        }
    }

    /// <summary>
    /// 经济驱动升级：金库达到升级费 2 倍时升级已有完工建筑（一次一座）。
    /// 大本营优先升级（升到 2 级解锁黑暗兵营，局内规则）；随后回防时优先哨塔、平时优先兵营。
    /// 升级后兵营显示等级提升 → 高阶兵种解锁、建筑属性提升。
    /// </summary>
    private void TryUpgradeBuildings()
    {
        if (_config == null || Time.time < _nextUpgradeTime)
            return;

        _nextUpgradeTime = Time.time + UnityEngine.Random.Range(3f, 6f);

        // 大本营登记：由胜负判定在 Configure 之后生成，这里懒绑定一次。
        if (_mainBase == null)
        {
            GameObject enemyBase = StageGoalTracker.LastEnemyMainBase;
            if (enemyBase != null)
            {
                GameObjectProperty baseProp = enemyBase.GetComponent<GameObjectProperty>();
                if (baseProp != null && baseProp.side == TeamId)
                    _mainBase = enemyBase.GetComponent<BuildingBase>();
            }
        }

        // 大本营优先升级：升到 2 级解锁黑暗兵营。
        // 注意：大本营是预摆建筑（无施工流程，IsCompleted 恒为 false），只校验存活。
        if (_mainBase != null && _mainBase.gameObject.activeInHierarchy)
        {
            BuildUP baseUp = _mainBase.GetComponent<BuildUP>();
            if (baseUp != null && baseUp.CanUpgrade && _gold >= baseUp.Cost * 2)
            {
                if (baseUp.TryUpgradeByTeam())
                    return;
            }
        }

        for (int pass = 0; pass < 2; pass++)
        {
            bool wantSentry = _defending ? (pass == 0) : (pass == 1);
            for (int i = 0; i < _buildings.Count; i++)
            {
                BuildingBase building = _buildings[i];
                if (building == null || !building.gameObject.activeInHierarchy || !building.IsCompleted)
                    continue;

                bool isSentry = building.GetComponent<BuildingTowerAI>() != null;
                if (isSentry != wantSentry)
                    continue;

                BuildUP buildUp = building.GetComponent<BuildUP>();
                if (buildUp == null || !buildUp.CanUpgrade)
                    continue;

                if (_gold < buildUp.Cost * 2)
                    continue;   // 升级需保留一倍费用的缓冲，避免升级后无钱生产。

                if (buildUp.TryUpgradeByTeam())
                {
                    // 升级解锁高阶兵种后立即重新评估出兵：让新兵种尽快被选用。
                    _nextRotationTime = Mathf.Min(_nextRotationTime, Time.time + 1.5f);
                    return;
                }
            }
        }
    }

    /// <summary>
    /// 兵种轮换：轮换周期到点（或升级后）逐兵营重评当前出兵。
    /// 评分制：克制玩家主力 +100、每阶 +10、与其它兵营定位不重复 +5；
    /// 新兵种评分不低于当前即转产（克制对位/高阶解锁必然切换；平级替代兵种
    /// 因平分随机取舍随周期自然轮替）。每轮只转一座，避免同帧大换血。
    /// </summary>
    private void RotateTroops(TroopTag dominantPlayerTag)
    {
        if (Time.time < _nextRotationTime)
            return;

        _nextRotationTime = Time.time + UnityEngine.Random.Range(
            _defending ? 8f : 15f, _defending ? 12f : 25f);

        for (int i = 0; i < _barracks.Count; i++)
        {
            BuildingTraining barracks = _barracks[i];
            if (barracks == null || !barracks.IsTraining)
                continue;

            TroopDefinition current = barracks.CurrentTroop;

            // 其它兵营正在训练的定位（阵容多样性参考）。
            var otherTags = new HashSet<TroopTag>();
            for (int j = 0; j < _barracks.Count; j++)
            {
                if (j == i)
                    continue;

                BuildingTraining other = _barracks[j];
                if (other != null && other.IsTraining && other.CurrentTroop != null)
                    otherTags.Add(other.CurrentTroop.Tag);
            }

            TroopDefinition best = PickBestTroop(barracks, dominantPlayerTag, otherTags);
            if (best == null || best == current)
                continue;

            // 新兵种评分不低于当前即转产：克制对位（+100）/更高阶（每阶 +10）必然触发，
            // 平级替代兵种因平分随机取舍也会随轮换周期自然轮替（阵容不固化）。
            if (ScoreTroop(best, dominantPlayerTag, otherTags) <
                ScoreTroop(current, dominantPlayerTag, otherTags))
                continue;

            barracks.CancelTraining();
            if (barracks.TryStartTraining(best))
                return;
        }
    }

    private void Sweep()
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            BuildingBase building = _pending[i];
            if (building == null)
            {
                _pending.RemoveAt(i);
                continue;
            }
            if (!building.IsCompleted)
                continue;

            _pending.RemoveAt(i);
            BuildingTraining training = building.GetComponent<BuildingTraining>();
            if (training != null && !_barracks.Contains(training))
                _barracks.Add(training);
        }

        for (int i = _buildings.Count - 1; i >= 0; i--)
        {
            if (_buildings[i] == null || !_buildings[i].gameObject.activeInHierarchy)
                _buildings.RemoveAt(i);
        }
        for (int i = _barracks.Count - 1; i >= 0; i--)
        {
            if (_barracks[i] == null || !_barracks[i].gameObject.activeInHierarchy)
                _barracks.RemoveAt(i);
        }
    }

    /// <summary>回防态或龟缩人格时优先哨塔槽位。</summary>
    private bool PreferSentry()
    {
        return _defending || (_config != null && _config.strategy == EnemyStrategy.Defensive);
    }

    private void TryBuildNextSlot()
    {
        if (_config == null || _config.buildSlots == null || _config.buildSlots.Count == 0)
            return;

        // 预算门：净收入为正才开建；富余时（大额存款）允许动用存款开建，避免“只攒钱不扩张”。
        if (NetCoinPerSec <= 0 && !IsRich())
            return;

        if (_retrySlot >= 0)
        {
            if (Time.time < _retryTime)
                return;
            int retry = _retrySlot;
            _retrySlot = -1;
            if (TryBuildSlot(retry))
                return;
        }

        // 相邻两次开工的随机间隔：建筑节奏不再千篇一律。
        if (Time.time < _nextBuildTime)
            return;

        int barracksSlot = FindSlot(BuildingType.Barracks);
        int sentrySlot = FindSlot(BuildingType.Sentry);
        bool canExpandBarracks = CountBarracks() < _config.maxBarracks;
        bool canExpandSentries = CountSentries() < _config.maxSentries && CanBuildSentry();

        if (PreferSentry())
        {
            // 回防/龟缩：先补哨塔（槽位表 → 动态找位），再扩兵营。
            if (canExpandSentries)
            {
                if (sentrySlot >= 0) { TryBuildSlot(sentrySlot); return; }
                if (TryBuildDynamic(BuildingType.Sentry)) return;
            }
            if (canExpandBarracks)
            {
                // 兵营种类：先按设计槽位盖普通兵营；槽位用完后动态扩张阶段按概率混搭黑暗兵营。
                if (barracksSlot >= 0) { TryBuildSlot(barracksSlot); return; }

                if (UnityEngine.Random.value < DarkBarracksChance)
                {
                    int darkSlot = FindSlot(BuildingType.DarkBarracks);
                    if (darkSlot >= 0) { TryBuildSlot(darkSlot); return; }
                    if (TryBuildDynamic(BuildingType.DarkBarracks)) return;
                }
                if (TryBuildDynamic(BuildingType.Barracks)) return;
            }
        }
        else
        {
            // 经济决策（平时）：
            // ① 有完工但未开训的兵营：通常先攒钱等开训；但存款富余时不再干等，继续扩张；
            // ② 生产饱和且兵营未达上限 → 扩兵营（槽位表 → 动态找位）；
            // ③ 兵营达上限 → 哨塔未达上限且在防守花费上限内 → 补哨塔。
            if (HasIdleBarracks() && !IsRich())
                return;

            if (canExpandBarracks)
            {
                // 兵营种类：先按设计槽位盖普通兵营；槽位用完后进入动态扩张阶段，
                // 再按概率混搭黑暗兵营（避免黑暗兵营抢在普通槽位之前、显得“只会盖黑暗兵营”）。
                if (barracksSlot >= 0) { TryBuildSlot(barracksSlot); return; }

                if (UnityEngine.Random.value < DarkBarracksChance)
                {
                    int darkSlot = FindSlot(BuildingType.DarkBarracks);
                    if (darkSlot >= 0) { TryBuildSlot(darkSlot); return; }
                    if (TryBuildDynamic(BuildingType.DarkBarracks)) return;
                }
                if (TryBuildDynamic(BuildingType.Barracks)) return;
            }
            if (canExpandSentries)
            {
                if (sentrySlot >= 0) { TryBuildSlot(sentrySlot); return; }
                if (TryBuildDynamic(BuildingType.Sentry)) return;
            }
        }

        // 槽位表里其它类型按（可能随机打乱后的）顺序补。
        for (int i = 0; i < _slotOrder.Count; i++)
        {
            int slotIndex = _slotOrder[i];
            if (_builtOrSkippedSlots.Contains(slotIndex))
                continue;
            TryBuildSlot(slotIndex);
            return;
        }
    }

    /// <summary>已建造的兵营总数（含施工中与动态扩张）。</summary>
    private int CountBarracks()
    {
        int count = 0;
        for (int i = 0; i < _buildings.Count; i++)
        {
            BuildingBase building = _buildings[i];
            if (building != null && building.GetComponent<BuildingTraining>() != null)
                count++;
        }
        return count;
    }

    /// <summary>已建造的哨塔总数（含施工中与动态扩张）。</summary>
    private int CountSentries()
    {
        int count = 0;
        for (int i = 0; i < _buildings.Count; i++)
        {
            BuildingBase building = _buildings[i];
            if (building != null && building.GetComponent<BuildingTowerAI>() != null)
                count++;
        }
        return count;
    }

    /// <summary>
    /// 动态扩张：槽位表用完后，在阵地锚点附近（向敌方半场方向、含上下偏移）寻找可用位置建造。
    /// 受建筑区域规则约束：候选格不得越过敌我大本营中点。
    /// 错落策略：先按“锚点由近及远、行顺序随机”收集若干合法候选格，
    /// 再从中随机挑一个开建——阵地错落有致，不再一排排整齐推进。
    /// </summary>
    private bool TryBuildDynamic(BuildingType type)
    {
        if (_config == null || MapCells.Instance == null || GameObjectPool.Instance == null)
            return false;

        GameObject prefab = BuildingButton.TryResolveBuilding(_config.raceId, type);
        if (prefab == null)
            return false;

        int buildCost = GetBuildCost(prefab);
        if (buildCost > 0 && _gold < buildCost)
            return false;

        GameObjectProperty prefabProp = prefab.GetComponent<GameObjectProperty>();
        Vector2Int anchor = _config.homeGridPosition;
        int minX = MinBuildX;
        int maxSteps = Mathf.Max(4, anchor.x - minX + 2);

        // 候选收集：锚点由近及远；每步内的行扫描顺序随机（错落感来源之一）。
        var candidates = new List<Vector2Int>();
        const int maxCandidates = 6;
        for (int step = 1; step <= maxSteps && candidates.Count < maxCandidates; step++)
        {
            int x = anchor.x - step;
            if (x < minX)
                break;

            foreach (int y in BuildRowOrder(anchor.y))
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (IsCellPlaceable(prefabProp, cell))
                {
                    candidates.Add(cell);
                    if (candidates.Count >= maxCandidates)
                        break;
                }
            }
        }

        // 随机挑一个候选（错落感来源之二）；候选失效（单位路过等）时顺延尝试下一个。
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Vector2Int tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            if (TryPlaceAt(-1, null, prefab, candidates[i]))
                return true;
        }

        return false;
    }

    /// <summary>生成锚点周围的行扫描顺序：锚点行优先，两侧行随机打乱（每次调用顺序不同）。</summary>
    private List<int> BuildRowOrder(int centerY)
    {
        MapCells map = MapCells.Instance;
        int height = map != null ? map.height : 8;

        var offsets = new List<int>();
        int maxOffset = Mathf.Max(4, height);
        for (int d = 1; d <= maxOffset; d++)
        {
            offsets.Add(d);
            offsets.Add(-d);
        }

        for (int i = offsets.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int tmp = offsets[i];
            offsets[i] = offsets[j];
            offsets[j] = tmp;
        }

        var rows = new List<int>();
        if (centerY >= 0 && centerY < height)
            rows.Add(centerY);

        for (int i = 0; i < offsets.Count; i++)
        {
            int y = centerY + offsets[i];
            if (y >= 0 && y < height && !rows.Contains(y))
                rows.Add(y);
        }

        return rows;
    }

    /// <summary>纯校验（不生成实例）：半场规则 + 建筑间距 + 越界/占位，供动态找位候选收集。</summary>
    private bool IsCellPlaceable(GameObjectProperty prefabProp, Vector2Int gridPos)
    {
        if (gridPos.x < MinBuildX)
            return false;
        if (HasBuildingNeighbor(prefabProp, gridPos, 1))
            return false;

        MapCells map = MapCells.Instance;
        if (map == null)
            return false;

        Vector2Int occupy = prefabProp != null ? prefabProp.occupySpace : Vector2Int.one;
        for (int x = 0; x < occupy.x; x++)
        {
            for (int y = 0; y < occupy.y; y++)
            {
                int cx = gridPos.x + x;
                int cy = gridPos.y + y;
                if (!map.IsInRange(cx, cy) || map.GetOccupierCount(cx, cy) > 0)
                    return false;
            }
        }

        return true;
    }

    /// <summary>建筑建造费用 = 预制体 BuildUP 三个等级中 Element 0（第一级）的 Cost。</summary>
    private static int GetBuildCost(GameObject instance)
    {
        BuildUP buildUp = instance != null ? instance.GetComponent<BuildUP>() : null;
        if (buildUp != null && buildUp.levels != null && buildUp.levels.Length > 0)
            return Mathf.Max(0, buildUp.levels[0].cost);

        return 0;
    }

    /// <summary>是否存在完工但未在训练的兵营（经济紧张、开训等待预算）。</summary>
    private bool HasIdleBarracks()
    {
        for (int i = 0; i < _barracks.Count; i++)
        {
            BuildingTraining barracks = _barracks[i];
            if (barracks != null && !barracks.IsTraining)
                return true;
        }
        return false;
    }

    /// <summary>防守花费上限：当前防守维护费占净收入比例未到 defenseSpendRatio 才允许继续补哨塔。</summary>
    private bool CanBuildSentry()
    {
        if (_config == null)
            return false;

        int net = Mathf.Max(1, NetCoinPerSec);
        return (float)_defenseUpkeep / net < Mathf.Clamp01(_config.defenseSpendRatio);
    }

    private int FindSlot(BuildingType type)
    {
        for (int i = 0; i < _slotOrder.Count; i++)
        {
            int slotIndex = _slotOrder[i];
            EnemyBuildSlot slot = _config.buildSlots[slotIndex];
            if (slot == null || _builtOrSkippedSlots.Contains(slotIndex))
                continue;
            if (slot.buildingType == type)
                return slotIndex;
        }
        return -1;
    }

    private bool TryBuildSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _config.buildSlots.Count ||
            _builtOrSkippedSlots.Contains(slotIndex))
            return false;

        EnemyBuildSlot slot = _config.buildSlots[slotIndex];
        if (slot == null)
        {
            _builtOrSkippedSlots.Add(slotIndex);
            return false;
        }

        // 黑暗兵营解锁限制（局内规则）：本方大本营局内等级 ≥2 才可建；未达标时本槽位暂缓（升级后自动续建）。
        if (slot.buildingType == BuildingType.DarkBarracks &&
            !UpgradeLevelRules.CanBuildDarkBarracksForSide(TeamId))
            return false;

        GameObject prefab = BuildingButton.TryResolveBuilding(_config.raceId, slot.buildingType);
        if (prefab == null)
        {
            if (!_warnedRace)
            {
                _warnedRace = true;
                Debug.LogWarning(
                    $"[TeamCommander] 队伍 {TeamId} 的种族 {_config.raceId} 未配置建筑类型 {slot.buildingType}，槽位 {slotIndex} 跳过。");
            }
            _builtOrSkippedSlots.Add(slotIndex);
            return false;
        }

        if (GameObjectPool.Instance == null)
            return false;

        // 建造费用（预制体 BuildUP 一级 Cost）从队伍账本扣除；余额不足先攒钱，下个 tick 再评估。
        int buildCost = GetBuildCost(prefab);
        if (buildCost > 0 && _gold < buildCost)
            return false;

        // 位置随机：在槽位坐标周围 ±positionJitter 格内随机偏移（打破呆板），
        // 偏移位置非法时回落槽位精确位置；仍非法则 5 秒后重试。
        Vector2Int exact = slot.gridPosition;
        if (_config.positionJitter > 0)
        {
            int jitter = Mathf.Max(1, _config.positionJitter);
            Vector2Int jittered = new Vector2Int(
                exact.x + UnityEngine.Random.Range(-jitter, jitter + 1),
                exact.y + UnityEngine.Random.Range(-jitter, jitter + 1));
            if (jittered != exact && TryPlaceAt(slotIndex, slot, prefab, jittered))
                return true;
        }

        // 建筑区域规则：槽位精确位置越过敌我大本营中点（中点往左）→ 本局不可建，跳过该槽位。
        if (exact.x < MinBuildX)
        {
            if (!_warnedInvalidSlots.Contains(slotIndex))
            {
                _warnedInvalidSlots.Add(slotIndex);
                Debug.LogWarning(
                    $"[TeamCommander] 队伍 {TeamId} 槽位 {slotIndex}（{slot.buildingType} @ {exact}）位于敌我大本营中点左侧，违反“只能修建在中点往右”的建筑规则，本局跳过。");
            }
            _builtOrSkippedSlots.Add(slotIndex);
            return false;
        }

        if (TryPlaceAt(slotIndex, slot, prefab, exact))
            return true;

        // 精确位置不满足间距/占位/半场规则：在槽位周边环形搜索一个满足规则的落点
        // （最多 3 圈），保持间距的同时不浪费设计好的槽位锚点。
        if (TryPlaceNearSlot(slotIndex, slot, prefab, exact, 3))
            return true;

        // 精确位置也非法：持续重试前给出一次性警告（通常是槽位坐标与其它建筑重叠）。
        if (!_warnedInvalidSlots.Contains(slotIndex))
        {
            _warnedInvalidSlots.Add(slotIndex);
            Debug.LogWarning(
                $"[TeamCommander] 队伍 {TeamId} 槽位 {slotIndex}（{slot.buildingType} @ {exact}）位置非法（越界/被占），将每 5 秒重试。请检查槽位坐标是否与其它建筑占地重叠。");
        }

        _retrySlot = slotIndex;
        _retryTime = Time.time + 5f;
        return false;
    }

    /// <summary>在指定网格基准位置放置并开工一座槽位建筑（同一套玩家施工流程）。</summary>
    private bool TryPlaceAt(int slotIndex, EnemyBuildSlot slot, GameObject prefab, Vector2Int gridPos)
    {
        // 建筑区域规则：只能修建在敌我大本营中点往右（含偏移/动态找位一律在此拦截）。
        if (gridPos.x < MinBuildX)
            return false;

        // 黑暗兵营解锁限制（防御性拦截，局内规则）：本方大本营局内等级 ≥2 才可建。
        BuildingTraining prefabTraining = prefab != null ? prefab.GetComponent<BuildingTraining>() : null;
        if (prefabTraining != null && prefabTraining.IsDarkBarracks &&
            !UpgradeLevelRules.CanBuildDarkBarracksForSide(TeamId))
            return false;

        // 敌方建筑间距规则：与已有建筑至少隔 1 格（只统计建筑，游走的兵种不计），
        // 避免敌方阵地过于密集（贴建交错、互相卡路、阵型难看）。
        GameObjectProperty prefabProp = prefab != null ? prefab.GetComponent<GameObjectProperty>() : null;
        if (HasBuildingNeighbor(prefabProp, gridPos, 1))
            return false;

        GameObject instance = GameObjectPool.Instance.Get(prefab);
        if (instance == null)
            return false;

        GameObjectProperty prop = instance.GetComponent<GameObjectProperty>();
        if (prop != null)
            prop.side = TeamId;   // 偶数队 = 敌方。

        // 网格吸附（与放置系统一致的基准→中心换算）。
        Vector2Int occupy = prop != null ? prop.occupySpace : Vector2Int.one;
        Vector2 center = new Vector2(
            gridPos.x + occupy.x / 2f,
            gridPos.y + occupy.y / 2f);
        instance.transform.position = new Vector3(center.x, center.y, 0f);

        // 注入本关敌方等级（建筑统计 + Buff 等级上下文）。
        if (_stageConfig != null)
            StageObjectInstantiator.ApplyEnemyLevels(instance, _stageConfig);

        BuildingBase building = instance.GetComponent<BuildingBase>();
        if (building == null)
        {
            GameObjectPool.Instance.Release(instance);
            return false;
        }

        building.RefreshOccupancy();

        if (!building.ChechValid())
        {
            // 位置被占/越界：归还实例（由调用方决定回落或重试）。
            GameObjectPool.Instance.Release(instance);
            return false;
        }

        building.StartBuild();
        _gold -= GetBuildCost(instance);   // 扣除建造费用（0 费用建筑跳过）。
        if (slotIndex >= 0)
            _builtOrSkippedSlots.Add(slotIndex);
        _buildings.Add(building);
        _pending.Add(building);

        // 下次开工的随机间隔（配置的范围内抖动）。
        float minGap = Mathf.Max(0f, _config.buildGapRange.x);
        float maxGap = Mathf.Max(minGap, _config.buildGapRange.y);
        _nextBuildTime = Time.time + UnityEngine.Random.Range(minGap, maxGap);
        return true;
    }

    /// <summary>
    /// 敌方建筑间距规则：候选占地外沿 margin 圈内是否已有建筑。
    /// 只统计建筑（兵种游走不计），避免交战期永远找不到落点。
    /// </summary>
    private static bool HasBuildingNeighbor(GameObjectProperty prop, Vector2Int gridPos, int margin)
    {
        MapCells map = MapCells.Instance;
        if (map == null || prop == null)
            return false;

        Vector2Int occupy = prop.occupySpace;
        int minX = gridPos.x - margin;
        int minY = gridPos.y - margin;
        int maxX = gridPos.x + occupy.x - 1 + margin;
        int maxY = gridPos.y + occupy.y - 1 + margin;

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                // 占地本身由 ChechValid 检查，这里只查外沿的间隔圈。
                if (x >= gridPos.x && x < gridPos.x + occupy.x &&
                    y >= gridPos.y && y < gridPos.y + occupy.y)
                    continue;

                List<GameObject> occupiers = map.GetOccupiers(x, y);
                if (occupiers == null)
                    continue;

                for (int i = 0; i < occupiers.Count; i++)
                {
                    GameObject obj = occupiers[i];
                    if (obj == null)
                        continue;

                    GameObjectProperty other = obj.GetComponent<GameObjectProperty>();
                    if (other != null && (other.objectType & GameObjectType.Building) != 0)
                        return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 在 anchor 周围环形范围（半径 radius）内找可放置位置；间距/半场/占位全部规则生效。
    /// 候选格收集后随机打乱再尝试——槽位周边不再固定取第一个格子，摆放更自然。
    /// </summary>
    private bool TryPlaceNearSlot(int slotIndex, EnemyBuildSlot slot, GameObject prefab, Vector2Int anchor, int radius)
    {
        var cells = new List<Vector2Int>();
        for (int r = 1; r <= radius; r++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r)
                        continue;
                    cells.Add(new Vector2Int(anchor.x + dx, anchor.y + dy));
                }
            }
        }

        for (int i = cells.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            Vector2Int tmp = cells[i];
            cells[i] = cells[j];
            cells[j] = tmp;
        }

        for (int i = 0; i < cells.Count; i++)
        {
            if (TryPlaceAt(slotIndex, slot, prefab, cells[i]))
                return true;
        }

        return false;
    }

    /// <summary>从队伍账本扣除金币（升级/建造等支出）；余额不足返回 false。金额 ≤0 直接通过。</summary>
    public bool TrySpendGold(int amount)
    {
        if (amount <= 0)
            return true;
        if (_gold < amount)
            return false;

        _gold -= amount;
        return true;
    }

    private void UpdateTraining(TroopTag dominantPlayerTag)
    {
        // 当前已在训练的兵种定位（用于阵容多样性：不同兵营尽量练不同定位）。
        var trainedTags = new HashSet<TroopTag>();
        for (int i = 0; i < _barracks.Count; i++)
        {
            BuildingTraining existing = _barracks[i];
            if (existing != null && existing.IsTraining && existing.CurrentTroop != null)
                trainedTags.Add(existing.CurrentTroop.Tag);
        }

        for (int i = 0; i < _barracks.Count; i++)
        {
            BuildingTraining barracks = _barracks[i];
            if (barracks == null || barracks.IsTraining)
                continue;

            TroopDefinition troop = PickTroop(barracks, trainedTags, dominantPlayerTag);
            if (troop == null)
                continue;

            if (!CanOpenUpkeep(troop.Upkeep, false))
                continue;

            // 扩张门：已有生产线时，新开一条需要盈余持续 surplusSeconds 秒；
            // 第一条生产线不受此限制（可负担即开训）。
            if (_totalUpkeep > 0 && !HasSurplus())
                continue;

            if (barracks.TryStartTraining(troop))
                trainedTags.Add(troop.Tag);
        }
    }

    /// <summary>
    /// 选兵种（评分制）：克制玩家主力 +100、每阶 +10、与其它兵营定位不重复 +5。
    /// 开训与转产共用同一评分，保证升级解锁的高阶兵种会被优先选用。
    /// </summary>
    private TroopDefinition PickTroop(BuildingTraining barracks, HashSet<TroopTag> trainedTags, TroopTag dominantPlayerTag)
    {
        return PickBestTroop(barracks, dominantPlayerTag, trainedTags);
    }

    /// <summary>
    /// 在兵营可训练兵种中（白名单 + 建筑等级 + 时间门槛）取评分最高者；
    /// 平分时随机取舍，让同等优秀的兵种随轮换周期自然轮替（阵容不固化）。
    /// </summary>
    private TroopDefinition PickBestTroop(BuildingTraining barracks, TroopTag dominantPlayerTag, HashSet<TroopTag> otherTrainedTags)
    {
        TroopDefinition[] troops = barracks != null ? barracks.Troops : null;
        if (troops == null)
            return null;

        TroopDefinition best = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < troops.Length; i++)
        {
            TroopDefinition troop = troops[i];
            if (troop == null || troop.UnlockLevel > barracks.Level || !IsTroopUnlocked(troop.Id))
                continue;

            int score = ScoreTroop(troop, dominantPlayerTag, otherTrainedTags);
            if (score > bestScore || (score == bestScore && UnityEngine.Random.value < 0.5f))
            {
                bestScore = score;
                best = troop;
            }
        }

        return best;
    }

    /// <summary>兵种评分：克制玩家主力 +100；每阶 +10（高阶兵种优先）；定位不与其它兵营重复 +5。</summary>
    private static int ScoreTroop(TroopDefinition troop, TroopTag dominantPlayerTag, HashSet<TroopTag> otherTrainedTags)
    {
        if (troop == null)
            return int.MinValue;

        TroopTag counter = dominantPlayerTag != TroopTag.None
            ? CounterTag(dominantPlayerTag)
            : TroopTag.None;

        int score = Mathf.Max(0, troop.Tier) * 10;
        if (counter != TroopTag.None && troop.Tag == counter)
            score += 100;
        if (otherTrainedTags != null && !otherTrainedTags.Contains(troop.Tag))
            score += 5;

        return score;
    }

    /// <summary>
    /// 兵种白名单判定：troopUnlocks 为空 = 本局兵营全部兵种可用；
    /// 填写了条目 = 本局只能训练列表内兵种，且局内时间到达该兵种 unlockTime 门槛后才可生产。
    /// </summary>
    private bool IsTroopUnlocked(string troopId)
    {
        if (_config == null || _config.troopUnlocks == null || _config.troopUnlocks.Count == 0)
            return true;

        for (int i = 0; i < _config.troopUnlocks.Count; i++)
        {
            EnemyTroopUnlock unlock = _config.troopUnlocks[i];
            if (unlock != null && unlock.troopId == troopId && LevelTime >= unlock.unlockTime)
                return true;
        }
        return false;
    }

    /// <summary>默认克制表：攻城→近战突进、远程→肉盾、肉盾→攻城、近战→远程。</summary>
    private static TroopTag CounterTag(TroopTag tag)
    {
        switch (tag)
        {
            case TroopTag.Siege: return TroopTag.Melee;
            case TroopTag.Ranged: return TroopTag.Tank;
            case TroopTag.Tank: return TroopTag.Siege;
            case TroopTag.Melee: return TroopTag.Ranged;
            default: return TroopTag.None;
        }
    }

    /// <summary>
    /// 一次遍历阵地半径内玩家单位，同时统计：
    /// threat = Σ（攻+魔攻）×威胁权重×距离衰减（建筑 ×4）；
    /// dominantTag = 数量最多的玩家定位标签。
    /// </summary>
    private void EvaluatePlayerPresence(out float threat, out TroopTag dominantTag)
    {
        float threatAccum = 0f;   // out 参数不能进 lambda，先累计再写回。
        if (_config == null)
        {
            threat = 0f;
            dominantTag = TroopTag.None;
            return;
        }

        int[] counts = new int[6];
        int best = 0;
        int bestCount = 0;

        ForEachPlayerInRadius(prop =>
        {
            float dist = Mathf.Abs(prop.transform.position.x - _config.homeGridPosition.x);
            float falloff = 1f / (dist + 1f);
            float score = (prop.atk + prop.magicAtk) * Mathf.Max(0.5f, prop.threatScore) * falloff;
            if ((prop.objectType & GameObjectType.Building) != 0)
                score *= 4f;
            threatAccum += score;

            if (prop.troopTag != TroopTag.None && (int)prop.troopTag < counts.Length)
            {
                counts[(int)prop.troopTag]++;
                if (counts[(int)prop.troopTag] > bestCount)
                {
                    bestCount = counts[(int)prop.troopTag];
                    best = (int)prop.troopTag;
                }
            }
        });

        threat = threatAccum;
        dominantTag = (TroopTag)best;
    }

    /// <summary>遍历阵地半径内占用格上的玩家（奇数队）单位，每个对象只访问一次。</summary>
    private void ForEachPlayerInRadius(Action<GameObjectProperty> visit)
    {
        if (_config == null || visit == null || MapCells.Instance == null)
            return;

        int radius = Mathf.Max(1, Mathf.CeilToInt(_config.defendRadius));
        Vector2Int home = _config.homeGridPosition;
        HashSet<GameObject> seen = new HashSet<GameObject>();

        for (int x = home.x - radius; x <= home.x + radius; x++)
        {
            for (int y = home.y - radius; y <= home.y + radius; y++)
            {
                List<GameObject> occupiers = MapCells.Instance.GetOccupiers(x, y);
                if (occupiers == null)
                    continue;

                for (int i = 0; i < occupiers.Count; i++)
                {
                    GameObject obj = occupiers[i];
                    if (obj == null || !seen.Add(obj))
                        continue;

                    GameObjectProperty prop = obj.GetComponent<GameObjectProperty>();
                    if (prop == null || prop.side % 2 != 1)
                        continue;   // 只统计玩家（奇数队）。

                    visit(prop);
                }
            }
        }
    }
    #endregion
}

/// <summary>
/// 经济路由：偶数队（敌方）的维护费/收入查询走对应 TeamCommander 账本，
/// 奇数队（玩家）走全局 Coins。建筑与训练系统经本路由切换，不感知敌我。
/// </summary>
public static class TeamEconomy
{
    public static int TotalUpkeep(GameObjectProperty prop)
    {
        if (prop != null && TeamRules.IsEnemySide(prop.side))
        {
            TeamCommander commander = TeamCommander.GetForSide(prop.side);
            return commander != null ? commander.TotalUpkeep : 0;
        }
        return Coins.Instance != null ? Coins.Instance.TotalUpkeep : 0;
    }

    public static int CurrentCoinPerSec(GameObjectProperty prop)
    {
        if (prop != null && TeamRules.IsEnemySide(prop.side))
        {
            TeamCommander commander = TeamCommander.GetForSide(prop.side);
            return commander != null ? commander.CurrentCoinPerSec : 0;
        }
        return Coins.Instance != null ? Coins.Instance.CurrentCoinPerSec : 0;
    }

    public static bool RegisterUpkeep(GameObjectProperty prop, object source, int amount, bool isDefense = false)
    {
        if (prop != null && TeamRules.IsEnemySide(prop.side))
        {
            TeamCommander commander = TeamCommander.GetForSide(prop.side);
            return commander != null && commander.RegisterUpkeep(source, amount, isDefense);
        }
        return Coins.Instance != null && Coins.Instance.RegisterUpkeep(source, amount);
    }

    public static void UnregisterUpkeep(GameObjectProperty prop, object source)
    {
        if (prop != null && TeamRules.IsEnemySide(prop.side))
        {
            TeamCommander commander = TeamCommander.GetForSide(prop.side);
            if (commander != null)
                commander.UnregisterUpkeep(source);
            return;
        }

        if (Coins.Instance != null)
            Coins.Instance.UnregisterUpkeep(source);
    }

    /// <summary>按阵营路由扣费：敌方走队伍账本（TrySpendGold），玩家走全局金币；成功返回 true。</summary>
    public static bool TrySpend(GameObjectProperty prop, int amount)
    {
        if (prop != null && TeamRules.IsEnemySide(prop.side))
        {
            TeamCommander commander = TeamCommander.GetForSide(prop.side);
            return commander != null && commander.TrySpendGold(amount);
        }

        return Coins.Instance != null && Coins.Instance.ConsumeCoins(amount);
    }
}
