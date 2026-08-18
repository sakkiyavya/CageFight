using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 创伤：持续直伤减益。每隔 tickInterval 秒对目标造成一次直接伤害，层数无限叠加，
/// 每层持续 duration 秒后自动消失。所有层共享同一次统一伤害结算：
/// 每 tick 将当前全部存活层的伤害求和，仅构造一次伤害事件交给目标结算，
/// 避免叠层越多、每秒伤害事件越多造成的性能开销（面向抖音小游戏优化）。
/// 伤害与时长均由施加者决定：基础伤害固定为 baseDamage，
/// 等级加成通过 GetLevelBonus 接缝接入（等级系统未建成前返回 0，即每 tick 恰好造成 baseDamage 点伤害）；
/// 时长由携带本组件的施加者攻击预制体配置。
/// </summary>
public class TraumaDebuff : BuffBase
{
    [Header("创伤配置")]
    [SerializeField, Min(0.1f)] private float duration = 5f;          // 单层持续时长，由施加者预制体配置。
    [SerializeField, Min(0.1f)] private float tickInterval = 1f;      // 两次统一伤害结算之间的间隔秒。
    [SerializeField, Min(0)] private int baseDamage = 10;             // 每层每 tick 的基础伤害。
    [SerializeField] private DamageType damageType = DamageType.normal; // 创伤按物理伤害结算。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => true;

    /// <summary>两次伤害结算之间的间隔，供层管理器读取。</summary>
    public float TickInterval => tickInterval;
    /// <summary>每 tick 的伤害类型，供层管理器构造伤害时读取。</summary>
    public DamageType DamageType => damageType;

    /// <summary>供运行时创建/配置实例时设置单层持续时长。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }

    #region Buff 生命周期
    /// <summary>
    /// 不带来源信息的施加入口：没有施法者数据，每 tick 伤害退化为仅基础伤害。
    /// 直接调用方应优先使用带 <see cref="Damage"/> 的重载，以继承施法者阵营与等级加成。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        return ApplyBuff(prop, Damage.DefaultDamage);
    }

    /// <summary>
    /// 从来源伤害中解析施法者，计算每层伤害（基础伤害 + 等级加成）并叠加一层创伤。
    /// </summary>
    /// <param name="prop">需要施加创伤的目标属性组件。</param>
    /// <param name="damage">包含施法者与阵营的伤害数据，用于等级加成与阵营归属。</param>
    /// <returns>目标存活且成功叠加一层时返回 <see langword="true"/>。</returns>
    public override bool ApplyBuff(GameObjectProperty prop, Damage damage)
    {
        if (prop == null || prop.isDead)
            return false;

        GameObjectProperty casterProp = null;
        if (damage.source != null)
            casterProp = damage.source.GetComponent<GameObjectProperty>();

        int tickDamage = Mathf.Max(1, baseDamage + GetLevelBonus(casterProp));

        TraumaState state = prop.GetComponent<TraumaState>();
        if (state == null)
            state = prop.gameObject.AddComponent<TraumaState>();

        return state.AddLayer(this, tickDamage, damage.side, damage.source, duration);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层创伤。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        TraumaState state = prop.GetComponent<TraumaState>();
        return state != null && state.RemoveLayer(this);
    }

    /// <summary>
    /// 查询目标身上当前的创伤层数（供外部机制如 Fork apprentice 使用）。
    /// </summary>
    public int GetLayerCount(GameObjectProperty prop)
    {
        if (prop == null)
            return 0;

        TraumaState state = prop.GetComponent<TraumaState>();
        return state != null ? state.LayerCount : 0;
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 等级加成接缝：等级系统建成后在此读取施法者等级并换算加成伤害
    /// （例如从未来的单位等级组件取得 level，返回 level * 每级伤害）。
    /// 当前等级系统未实现，返回 0 表示每 tick 恰好造成 baseDamage 点伤害。
    /// </summary>
    /// <param name="casterProp">施法者的属性组件；缺失或已回收时返回 0。</param>
    protected virtual int GetLevelBonus(GameObjectProperty casterProp)
    {
        return 0;
    }
    #endregion
}

/// <summary>
/// 目标身上的创伤层管理器：无限叠加，每层独立到期，
/// 但所有层共享同一次统一伤害结算（每 tickInterval 秒把当前全部层伤害求和后只构造一次伤害事件），
/// 因此伤害事件频率与叠层数无关，始终为每 tickInterval 秒一次。
/// 本管理器不做任何表现修改（无染色、无材质改动），停用时只清理层引用。
/// </summary>
internal class TraumaState : MonoBehaviour
{
    /// <summary>单层创伤快照，施加瞬间锁定伤害数值。</summary>
    private class Layer
    {
        public TraumaDebuff source;    // 施加该层的减益实例，用于从 currentDebuff 移除。
        public int tickDamage;         // 施加时快照的每 tick 伤害（基础 + 等级加成）。
        public float expireTime;       // 该层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    /// <summary>当前创伤层数（供外部机制读取）。</summary>
    public int LayerCount => layers.Count;

    private GameObjectProperty prop;
    private ICollide collide;
    private Damage tickDamage = Damage.DefaultDamage;   // 复用的伤害结构，避免每 tick 重新构造分配。

    private float tickInterval = 1f;       // 统一结算间隔，首次施加时从来源读取。
    private float nextTickTime = -1f;      // 下一次统一结算的游戏时间，-1 表示尚未开始计时。
    private int side;                      // 施法者阵营快照，避免被友军判定拦截。
    private GameObject caster;             // 施法者引用，仅用于伤害归属；被回收后不影响已快照的伤害。
    private DamageType damageType;         // 统一结算的伤害类型，首次施加时从来源读取。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        collide = GetComponent<ICollide>();
    }

    /// <summary>
    /// 无限叠加一层创伤；不设层数上限。首个层加入时确定统一结算的间隔、类型与施法者归属。
    /// </summary>
    public bool AddLayer(
        TraumaDebuff source,
        int tickDamage,
        int side,
        GameObject caster,
        float duration)
    {
        if (source == null || prop == null)
            return false;

        if (layers.Count == 0)
        {
            tickInterval = source.TickInterval;
            damageType = source.DamageType;
            nextTickTime = Time.time + source.TickInterval;
            this.side = side;
            this.caster = caster;
        }

        layers.Add(new Layer
        {
            source = source,
            tickDamage = tickDamage,
            expireTime = Time.time + duration,
        });

        return true;
    }

    /// <summary>
    /// 移除由指定减益实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(TraumaDebuff source)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].source != source)
                continue;

            RemoveAt(i);
            return true;
        }

        return false;
    }

    private void Update()
    {
        // 倒序清理到期层；全部到期后本管理器会销毁自身。
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (Time.time >= layers[i].expireTime)
                RemoveAt(i);
        }

        if (layers.Count == 0)
            return;

        if (prop != null && prop.isDead)
            return;

        if (Time.time < nextTickTime)
            return;

        TickAll();
        nextTickTime = Time.time + tickInterval;
    }

    /// <summary>
    /// 将当前全部存活层的伤害求和，仅构造一次伤害事件并经目标的 ICollide 完整结算。
    /// </summary>
    private void TickAll()
    {
        if (collide == null)
            return;

        int totalDamage = 0;
        for (int i = 0; i < layers.Count; i++)
            totalDamage += layers[i].tickDamage;

        tickDamage.side = side;
        tickDamage.initialDamage = totalDamage;
        tickDamage.finalDamage = 0;
        tickDamage.type = damageType;
        tickDamage.source = caster;
        tickDamage.target = prop.gameObject;
        tickDamage.collideDir = 1;
        tickDamage.repel = 0f;
        tickDamage.buffs = null;

        collide.OnCollide(tickDamage);
    }

    private void RemoveAt(int index)
    {
        Layer layer = layers[index];
        layers.RemoveAt(index);

        if (prop != null && layer.source != null)
            prop.currentDebuff.Remove(layer.source);

        if (layers.Count == 0)
            Destroy(this);
    }

    private void OnDisable()
    {
        RestoreEverything();
    }

    /// <summary>
    /// 从目标属性移除全部层引用并清空层列表。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].source != null)
                    prop.currentDebuff.Remove(layers[i].source);
            }
        }

        layers.Clear();
    }
}
