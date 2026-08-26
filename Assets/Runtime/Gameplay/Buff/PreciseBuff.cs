using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 精准增益：提升目标攻速（atkRate）。
/// 基础加成 5%，并受局外“攻击魔法等级”（UserGlobalInfo.AttackMagicLevel）影响——每一级额外增加 0.5%。
/// 可无限叠加：每层独立计时、独立快照加成比例，总效果为各层比例相加（如 2 层 = 5% + 5% = 10%）。
/// 无获得音效、无视觉表现。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class PreciseBuff : BuffBase
{
    [Header("精准数值")]
    [SerializeField, Min(0f)]
    private float basePercent = 0.05f;      // 基础攻速加成比例（5%）。
    [SerializeField, Min(0f)]
    private float levelPercent = 0.005f;    // 每级局外攻击魔法等级额外加成比例（0.5%）。
    [SerializeField, Min(0.1f)]
    private float duration = 10f;           // 每层持续时间秒。

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
    /// 叠加一层精准；不设层数上限，每层独立计时与快照加成比例。
    /// </summary>
    protected override bool ApplyBuffInternal(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        PreciseState state = prop.GetComponent<PreciseState>();
        if (state == null)
            state = prop.gameObject.AddComponent<PreciseState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层精准。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        PreciseState state = prop.GetComponent<PreciseState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 计算单层加成比例：基础 5% + 局外攻击魔法等级 × 0.5%。
    /// </summary>
    public float GetTotalPercent()
    {
        int level = UserGlobalInfo.Instance != null
            ? UserGlobalInfo.Instance.AttackMagicLevel
            : 0;
        return basePercent + level * levelPercent;
    }
    #endregion
}

/// <summary>
/// 目标身上的精准层管理器：无限叠加、每层独立到期，总加成按各层比例相加，
/// 仅作用于攻速，无音效与视觉表现。
/// </summary>
internal class PreciseState : MonoBehaviour
{
    /// <summary>单层精准快照，施加瞬间锁定加成比例。</summary>
    private class Layer
    {
        public PreciseBuff source;    // 施加该层的实例，用于取消时匹配。
        public float percent;         // 本层快照的加成比例。
        public float expireTime;      // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private float baseAtkRate;        // 首层施加时快照的基础攻速。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    /// <summary>
    /// 无限叠加一层精准；首个层加入时快照基础攻速。
    /// </summary>
    public bool AddLayer(PreciseBuff source)
    {
        if (source == null || prop == null)
            return false;

        if (layers.Count == 0)
            baseAtkRate = prop.atkRate;

        layers.Add(new Layer
        {
            source = source,
            percent = source.GetTotalPercent(),
            expireTime = Time.time + source.Duration,
        });

        ApplyEffect();
        return true;
    }

    /// <summary>
    /// 移除由指定实例施加的一层精准，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(PreciseBuff source)
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
            prop.atkRate = baseAtkRate;
            Destroy(this);
        }
    }

    /// <summary>
    /// 按当前全部层的比例求和后重算目标的攻速。
    /// </summary>
    private void ApplyEffect()
    {
        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].percent;

        prop.atkRate = baseAtkRate * (1f + total);
    }

    private void OnDisable()
    {
        RestoreEverything();
    }

    /// <summary>
    /// 还原基础攻速并清空层列表。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
            prop.atkRate = baseAtkRate;

        layers.Clear();
    }
}
