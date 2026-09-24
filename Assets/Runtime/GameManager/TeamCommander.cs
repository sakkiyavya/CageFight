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
    private readonly List<int> _slotOrder = new List<int>();   // 本局槽位建造顺序（可选随机打乱）。
    private float _nextBuildTime;                              // 下一次允许开工的时间（随机间隔）。
    private int _retrySlot = -1;
    private float _retryTime;
    private float _nextTick;
    private float _surplusSince = -1f;   // 净收入盈余的持续起点（-1 = 无盈余）。
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
        _slotOrder.Clear();
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

        // 3. 回防判定：阵地半径内玩家威胁值超阈值 → 回防态。
        _defending = ComputeThreat() >= (_config != null ? _config.threatThreshold : float.MaxValue);

        // 4. 建造：按当前状态挑选槽位（回防/龟缩优先哨塔，否则优先兵营）。
        TryBuildNextSlot();

        // 5. 生产：为完工的兵营选择兵种并开训（同一 tick 同步开训 = 天然波次）。
        UpdateTraining();
    }

    /// <summary>盈余是否已持续达到配置时长（扩张新生产线用；第一条生产线不受此限制）。</summary>
    private bool HasSurplus()
    {
        if (_config == null || _config.surplusSeconds <= 0f)
            return true;

        return _surplusSince >= 0f && Time.time - _surplusSince >= _config.surplusSeconds;
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

        // 预算门：净收入为正才开建。预留底线（reserve）只约束“开启新的维护费”，
        // 不拦初始建造——否则基础收入低于 reserve 时 AI 永远盖不出第一座建筑。
        if (NetCoinPerSec <= 0)
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

        // 每 tick 最多尝试一个槽位；按本局（可能随机打乱后的）顺序选择。
        int preferred = PreferSentry()
            ? FindSlot(BuildingType.Sentry)
            : FindSlot(BuildingType.Barracks);
        if (preferred >= 0)
        {
            TryBuildSlot(preferred);
            return;
        }

        for (int i = 0; i < _slotOrder.Count; i++)
        {
            int slotIndex = _slotOrder[i];
            if (_builtOrSkippedSlots.Contains(slotIndex))
                continue;
            TryBuildSlot(slotIndex);
            return;
        }
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

        if (TryPlaceAt(slotIndex, slot, prefab, exact))
            return true;

        _retrySlot = slotIndex;
        _retryTime = Time.time + 5f;
        return false;
    }

    /// <summary>在指定网格基准位置放置并开工一座槽位建筑（同一套玩家施工流程）。</summary>
    private bool TryPlaceAt(int slotIndex, EnemyBuildSlot slot, GameObject prefab, Vector2Int gridPos)
    {
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
        _builtOrSkippedSlots.Add(slotIndex);
        _buildings.Add(building);
        _pending.Add(building);

        // 下次开工的随机间隔（配置的范围内抖动）。
        float minGap = Mathf.Max(0f, _config.buildGapRange.x);
        float maxGap = Mathf.Max(minGap, _config.buildGapRange.y);
        _nextBuildTime = Time.time + UnityEngine.Random.Range(minGap, maxGap);
        return true;
    }

    private void UpdateTraining()
    {
        for (int i = 0; i < _barracks.Count; i++)
        {
            BuildingTraining barracks = _barracks[i];
            if (barracks == null || barracks.IsTraining)
                continue;

            TroopDefinition troop = PickTroop(barracks);
            if (troop == null)
                continue;

            if (!CanOpenUpkeep(troop.Upkeep, false))
                continue;

            // 扩张门：已有生产线时，新开一条需要盈余持续 surplusSeconds 秒；
            // 第一条生产线不受此限制（可负担即开训）。
            if (_totalUpkeep > 0 && !HasSurplus())
                continue;

            barracks.TryStartTraining(troop);
        }
    }

    /// <summary>选兵种：回防态优先“克制玩家主力定位”的兵种；否则在可用兵种中随机选（增加变化性）。</summary>
    private TroopDefinition PickTroop(BuildingTraining barracks)
    {
        TroopDefinition[] troops = barracks.Troops;
        if (troops == null || troops.Length == 0)
            return null;

        TroopTag counter = _defending ? CounterTag(DominantPlayerTag()) : TroopTag.None;

        var available = new List<TroopDefinition>();
        TroopDefinition bestCounter = null;
        for (int i = 0; i < troops.Length; i++)
        {
            TroopDefinition troop = troops[i];
            if (troop == null || troop.UnlockLevel > barracks.Level)
                continue;
            if (!IsTroopUnlocked(troop.Id))
                continue;

            available.Add(troop);
            if (counter != TroopTag.None && troop.Tag == counter && bestCounter == null)
                bestCounter = troop;
        }

        if (available.Count == 0)
            return null;
        if (bestCounter != null)
            return bestCounter;

        // 非回防态：可用兵种随机挑选，避免每局阵容千篇一律。
        return available[UnityEngine.Random.Range(0, available.Count)];
    }

    /// <summary>兵种解锁时间门：troopUnlocks 为空 = 全部可用；否则只训练已到解锁时间的。</summary>
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

    /// <summary>统计阵地半径内玩家单位的定位标签数量，取数量最多者。</summary>
    private TroopTag DominantPlayerTag()
    {
        int[] counts = new int[6];
        ForEachPlayerInRadius(prop =>
        {
            if (prop.troopTag != TroopTag.None && (int)prop.troopTag < counts.Length)
                counts[(int)prop.troopTag]++;
        });

        int best = 0;
        int bestCount = 0;
        for (int i = 1; i < counts.Length; i++)
        {
            if (counts[i] > bestCount)
            {
                bestCount = counts[i];
                best = i;
            }
        }
        return (TroopTag)best;
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

    /// <summary>阵地威胁值：半径内玩家单位的（攻+魔攻）×威胁权重×距离衰减；建筑 ×4。</summary>
    private float ComputeThreat()
    {
        if (_config == null || MapCells.Instance == null)
            return 0f;

        float threat = 0f;
        ForEachPlayerInRadius(prop =>
        {
            float dist = Mathf.Abs(prop.transform.position.x - _config.homeGridPosition.x);
            float falloff = 1f / (dist + 1f);
            float score = (prop.atk + prop.magicAtk) * Mathf.Max(0.5f, prop.threatScore) * falloff;
            if ((prop.objectType & GameObjectType.Building) != 0)
                score *= 4f;
            threat += score;
        });
        return threat;
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
}
