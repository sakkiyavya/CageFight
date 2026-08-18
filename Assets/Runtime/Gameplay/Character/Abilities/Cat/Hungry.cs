using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hungry（饥饿）机制：
/// 1. 妄业之力光环：附近 auraRange 格（默认 3）内的友方单位保持 auraLayers 层（默认 3）
///    “妄业之力”（FalseLifeBuff）。层数低于上限时自动补满；单位离开光环范围时，
///    移除由本光环施加的全部层。只对从未触发过妄业之力复活
///    （FalseLifeState.HasRevivedOnce == false）的单位有效：一旦某单位触发过一次
///    妄业之力复活，本光环在该单位的本轮生命周期内不再给它加持，直至其死亡重生
///    （对象池重新启用）后重置。
/// 2. 吞噬离场：附近 watchRange 格（默认 4）内的任意单位离场（死亡/回收）时，
///    本角色最大生命与攻击各增长 growPercentPerDeath（默认 1%）。增长基于入场时的
///    基础值线性累计、无上限；成长只在本轮生命周期内有效，Hungry 死亡重生或
///    场景切换时恢复基础值（对象池复用安全）。
/// 通过低速定时 OverlapCircleNonAlloc 扫描实现，仅新增本脚本即可生效。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class Hungry : MonoBehaviour
{
    [Header("妄业之力光环")]
    [SerializeField, Min(0.1f)] private float auraRange = 3f;           // 光环半径（格）。
    [SerializeField, Min(1)] private int auraLayers = 3;                // 每名友方保持的妄业之力层数。
    [SerializeField, Min(0.1f)] private float auraTickInterval = 0.5f;  // 光环扫描间隔秒。
    [SerializeField, Min(0.1f)] private float falseLifeDuration = 5f;   // 每层妄业之力持续秒。

    [Header("吞噬离场")]
    [SerializeField, Min(0.1f)] private float watchRange = 4f;            // 离场观察半径（格）。
    [SerializeField, Min(0f)] private float growPercentPerDeath = 0.01f;  // 每次离场增长比例（1%）。
    [SerializeField] private bool includeBuildings = false;              // 离场增长是否计入建筑。

    private static readonly Collider2D[] hits = new Collider2D[64];                  // 复用的扫描缓冲区。

    private GameObjectProperty _prop;                                                // 本角色核心属性。
    private CharacterHealth _health;                                                 // 本角色生命组件（刷新血条）。
    private FalseLifeBuff _falseLife;                                                // 光环使用的妄业之力实例。

    private float _auraTimer;                                                        // 光环扫描计时。
    private readonly List<GameObjectProperty> _buffed = new List<GameObjectProperty>();     // 当前持有光环层的友方。
    private readonly List<GameObjectProperty> _inRange = new List<GameObjectProperty>();    // 本轮扫描到的友方缓存。
    private readonly List<GameObjectProperty> _scratch = new List<GameObjectProperty>();    // 离场清点临时缓存。
    private readonly HashSet<GameObjectProperty> _watched = new HashSet<GameObjectProperty>(); // 观察半径内在场单位。

    private float _growPercent;     // 已累计的增长比例（基于入场基础值）。
    private int _baseMaxHp;         // 首次吞噬时记录的基础最大生命。
    private int _baseAtk;           // 首次吞噬时记录的基础攻击。
    private bool _baseCached;       // 是否已记录基础值。

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
        _falseLife = gameObject.AddComponent<FalseLifeBuff>();
        _falseLife.SetDuration(falseLifeDuration);
    }

    private void OnEnable()
    {
        _auraTimer = 0f;
        _buffed.Clear();
        _inRange.Clear();
        _scratch.Clear();
        _watched.Clear();
        _growPercent = 0f;
        _baseCached = false;
    }

    private void OnDisable()
    {
        // 死亡/场景切换时撤掉光环层并恢复被吞噬增长的基础值，保证对象池复用安全。
        for (int i = 0; i < _buffed.Count; i++)
        {
            if (_buffed[i] != null)
                CancelAuraLayers(_buffed[i]);
        }
        _buffed.Clear();
        _watched.Clear();

        if (_prop != null && _baseCached)
        {
            _prop.maxHp = _baseMaxHp;
            _prop.atk = _baseAtk;
        }
        _baseCached = false;
        _growPercent = 0f;
    }

    private void Update()
    {
        if (_prop == null || _prop.isDead)
            return;

        _auraTimer += Time.deltaTime;
        if (_auraTimer < auraTickInterval)
            return;

        _auraTimer = 0f;
        RefreshAura();
        RefreshWatch();
    }

    #region 妄业之力光环
    /// <summary>
    /// 扫描光环范围内友方：新进入/仍在范围内的补满 auraLayers 层；
    /// 离开范围或不再满足条件（死亡、已复活过）的撤掉本光环施加的层。
    /// </summary>
    private void RefreshAura()
    {
        _inRange.Clear();
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, auraRange, hits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            GameObjectProperty target = hit.GetComponentInParent<GameObjectProperty>();
            if (!IsEligibleAuraTarget(target) || _inRange.Contains(target))
                continue;

            _inRange.Add(target);
        }

        // 撤掉不再符合条件的友方身上的光环层。
        for (int i = _buffed.Count - 1; i >= 0; i--)
        {
            GameObjectProperty target = _buffed[i];
            if (target == null || !_inRange.Contains(target))
            {
                if (target != null)
                    CancelAuraLayers(target);
                _buffed.RemoveAt(i);
            }
        }

        // 为范围内友方补满层数（登记 currentBuff，随层到期/触发由层管理器移除）。
        for (int i = 0; i < _inRange.Count; i++)
        {
            GameObjectProperty target = _inRange[i];
            FalseLifeState state = target.GetComponent<FalseLifeState>();
            int layers = state != null ? state.CountLayers(_falseLife) : 0;
            for (int add = layers; add < auraLayers; add++)
            {
                if (_falseLife.ApplyBuff(target))
                    target.currentBuff.Add(_falseLife);
            }
        }

        // 本轮范围内友方成为下一轮对比基准。
        _buffed.Clear();
        for (int i = 0; i < _inRange.Count; i++)
            _buffed.Add(_inRange[i]);
    }

    /// <summary>
    /// 光环适用条件：同阵营、未死亡、具备生命组件、且从未触发过妄业之力复活。
    /// </summary>
    private bool IsEligibleAuraTarget(GameObjectProperty target)
    {
        if (target == null || target == _prop || target.isDead)
            return false;
        if (target.side != _prop.side)
            return false;
        if (target.GetComponent<CharacterHealth>() == null)
            return false;

        FalseLifeState state = target.GetComponent<FalseLifeState>();
        return state == null || !state.HasRevivedOnce;
    }

    /// <summary>
    /// 移除本光环实例施加在目标上的全部妄业之力层。
    /// </summary>
    private void CancelAuraLayers(GameObjectProperty target)
    {
        if (target == null)
            return;

        int guard = 0;
        while (guard++ < auraLayers + 4 && _falseLife.CancelBuff(target)) { }
    }
    #endregion

    #region 吞噬离场
    /// <summary>
    /// 清点观察半径内的单位：已观察单位死亡/回收时吞噬增长；
    /// 未死亡但走出观察范围的停止跟踪（不算离场吞噬）；新进入范围的加入观察。
    /// </summary>
    private void RefreshWatch()
    {
        _scratch.Clear();
        foreach (GameObjectProperty watched in _watched)
        {
            if (watched == null)
            {
                _scratch.Add(null);
                continue;
            }

            bool leftField = watched.isDead || !watched.gameObject.activeInHierarchy;
            if (leftField)
            {
                _scratch.Add(watched);  // 离场 → 吞噬增长。
                continue;
            }

            if (!InWatchRange(watched))
                _scratch.Add(watched);  // 走出范围且未离场 → 停止观察。
        }

        for (int i = 0; i < _scratch.Count; i++)
        {
            GameObjectProperty entry = _scratch[i];
            if (entry == null)
            {
                _watched.Remove(null);
                continue;
            }

            bool counted = entry.isDead || !entry.gameObject.activeInHierarchy;
            _watched.Remove(entry);
            if (counted)
                Grow();
        }

        // 观察半径内新出现的在场单位加入观察（HashSet 自动去重）。
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, watchRange, hits);
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
                continue;

            GameObjectProperty target = hit.GetComponentInParent<GameObjectProperty>();
            if (target == null || target == _prop || target.isDead)
                continue;

            bool isCharacter = (target.objectType & GameObjectType.Character) != 0;
            bool counts = isCharacter ||
                (includeBuildings && (target.objectType & GameObjectType.Building) != 0);
            if (!counts)
                continue;

            _watched.Add(target);
        }
    }

    /// <summary>
    /// 目标当前是否仍位于观察半径内（平方距离比较，避免开方）。
    /// </summary>
    private bool InWatchRange(GameObjectProperty target)
    {
        if (target == null)
            return false;

        Vector3 offset = target.transform.position - transform.position;
        return offset.x * offset.x + offset.y * offset.y <= watchRange * watchRange;
    }

    /// <summary>
    /// 吞噬一名离场单位：最大生命与攻击按入场基础值线性累计增长，并把增长的血量补到当前生命。
    /// </summary>
    private void Grow()
    {
        if (_prop == null || _prop.isDead)
            return;

        if (!_baseCached)
        {
            _baseMaxHp = _prop.maxHp;
            _baseAtk = _prop.atk;
            _baseCached = true;
        }

        _growPercent += growPercentPerDeath;

        int newMaxHp = Mathf.Max(1, Mathf.RoundToInt(_baseMaxHp * (1f + _growPercent)));
        int newAtk = Mathf.Max(1, Mathf.RoundToInt(_baseAtk * (1f + _growPercent)));
        int hpDelta = newMaxHp - _prop.maxHp;

        _prop.maxHp = newMaxHp;
        _prop.atk = newAtk;
        if (hpDelta > 0)
            _prop.currentHp = Mathf.Min(_prop.maxHp, _prop.currentHp + hpDelta);

        if (_health != null && _health.isActiveAndEnabled)
            _health.SetHpbar();
    }
    #endregion
}
