using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 虚假力量：数值类增益 Buff，每层增加击退（repel）1 点，
/// 并受局外“攻击魔法等级”（UserGlobalInfo.AttackMagicLevel）影响——每一级额外增加 0.1 点击退。
/// 叠加公式与“巨化”一致：层管理、无层数上限、加法叠加、每层独立计时与快照、
/// 逐层到期（层消失时对应加成同步移除）。
/// 无任何视觉表现（不染色、无呼吸、无弹动、无音效）。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class FalsePowerBuff : BuffBase
{
    [Header("虚假力量数值")]
    [SerializeField, Min(0f)]
    private float baseRepel = 1f;       // 每层基础击退加成（1 点）。
    [SerializeField, Min(0f)]
    private float levelRepel = 0.1f;    // 每级局外攻击魔法等级额外击退加成（0.1 点）。
    [SerializeField, Min(0.1f)]
    private float duration = 10f;       // 每层持续时间秒。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => false;

    /// <summary>每层持续时间，供层管理器读取。</summary>
    public float Duration => duration;

    /// <summary>供运行时创建/配置实例时设置每层持续时间。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层虚假力量；不设层数上限，每层独立计时与快照加成。
    /// </summary>
    protected override bool ApplyBuffInternal(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        FalsePowerState state = prop.GetComponent<FalsePowerState>();
        if (state == null)
            state = prop.gameObject.AddComponent<FalsePowerState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层虚假力量。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        FalsePowerState state = prop.GetComponent<FalsePowerState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 计算单层击退加成：基础 1 点 + 局外攻击魔法等级 × 0.1 点。
    /// </summary>
    public float GetTotalRepel()
    {
        int level = UserGlobalInfo.Instance != null
            ? UserGlobalInfo.Instance.AttackMagicLevel
            : 0;
        return baseRepel + level * levelRepel;
    }
    #endregion
}

/// <summary>
/// 目标身上的虚假力量层管理器：无限叠加、每层独立到期，总击退加成按各层相加，
/// 写入目标属性的 repel（攻击击退强度）字段。
/// </summary>
internal class FalsePowerState : MonoBehaviour
{
    /// <summary>单层虚假力量快照，施加瞬间锁定击退加成。</summary>
    private class Layer
    {
        public FalsePowerBuff source;   // 施加该层的实例，用于取消时匹配。
        public float repelBonus;        // 本层快照的击退加成。
        public float expireTime;        // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private float baseRepel;            // 首层施加时快照的基础击退。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    /// <summary>
    /// 无限叠加一层虚假力量；首个层加入时快照基础击退。
    /// </summary>
    public bool AddLayer(FalsePowerBuff source)
    {
        if (source == null || prop == null)
            return false;

        if (layers.Count == 0)
            baseRepel = prop.repel;

        layers.Add(new Layer
        {
            source = source,
            repelBonus = source.GetTotalRepel(),
            expireTime = Time.time + source.Duration,
        });

        ApplyEffect();
        return true;
    }

    /// <summary>
    /// 移除由指定实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(FalsePowerBuff source)
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
        // 倒序清理到期层；全部到期后本管理器会销毁自身并精确还原。
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (Time.time >= layers[i].expireTime)
                RemoveAt(i);
        }
    }

    private void RemoveAt(int index)
    {
        layers.RemoveAt(index);
        ApplyEffect();

        if (layers.Count == 0)
        {
            prop.repel = baseRepel;
            Destroy(this);
        }
    }

    /// <summary>
    /// 按当前全部层的击退加成求和后写入目标的 repel 字段。
    /// </summary>
    private void ApplyEffect()
    {
        if (prop == null)
            return;

        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].repelBonus;

        prop.repel = baseRepel + total;
    }

    private void OnDisable()
    {
        RestoreEverything();
    }

    /// <summary>
    /// 还原基础击退并清空层列表。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
            prop.repel = baseRepel;

        layers.Clear();
    }
}
