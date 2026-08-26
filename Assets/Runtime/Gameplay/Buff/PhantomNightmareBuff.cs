using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 虚魇之力：数值类增益 Buff，每层增加吸血（suckBlood）3%，
/// 并受局外“防御魔法等级”（UserGlobalInfo.DefenseMagicLevel）影响——每一级额外增加 0.3%。
/// 可无限叠加：层管理、无层数上限、加法叠加、每层独立计时与快照、逐层到期
/// （层消失时对应吸血加成同步移除）。
/// 无任何视觉表现（不染色、无呼吸、无弹动、无音效）。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class PhantomNightmareBuff : BuffBase
{
    [Header("虚魇之力数值")]
    [SerializeField, Min(0f)]
    private float baseSuck = 0.03f;     // 每层基础吸血比例（3%）。
    [SerializeField, Min(0f)]
    private float levelSuck = 0.003f;   // 每级局外防御魔法等级额外吸血比例（0.3%）。
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
    /// 叠加一层虚魇之力；不设层数上限，每层独立计时与快照加成。
    /// </summary>
    protected override bool ApplyBuffInternal(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        PhantomNightmareState state = prop.GetComponent<PhantomNightmareState>();
        if (state == null)
            state = prop.gameObject.AddComponent<PhantomNightmareState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层虚魇之力。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        PhantomNightmareState state = prop.GetComponent<PhantomNightmareState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 计算单层吸血加成：基础 3% + 局外防御魔法等级 × 0.3%。
    /// </summary>
    public float GetTotalSuck()
    {
        int level = UserGlobalInfo.Instance != null
            ? UserGlobalInfo.Instance.DefenseMagicLevel
            : 0;
        return baseSuck + level * levelSuck;
    }
    #endregion
}

/// <summary>
/// 目标身上的虚魇之力层管理器：无限叠加、每层独立到期，总吸血加成按各层相加，
/// 写入目标属性的 suckBlood（攻击吸血比例）字段。
/// </summary>
internal class PhantomNightmareState : MonoBehaviour
{
    /// <summary>单层虚魇之力快照，施加瞬间锁定吸血加成。</summary>
    private class Layer
    {
        public PhantomNightmareBuff source; // 施加该层的实例，用于取消时匹配。
        public float suckBonus;             // 本层快照的吸血加成。
        public float expireTime;            // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private float baseSuck;             // 首层施加时快照的基础吸血。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    /// <summary>
    /// 无限叠加一层虚魇之力；首个层加入时快照基础吸血。
    /// </summary>
    public bool AddLayer(PhantomNightmareBuff source)
    {
        if (source == null || prop == null)
            return false;

        if (layers.Count == 0)
            baseSuck = prop.suckBlood;

        layers.Add(new Layer
        {
            source = source,
            suckBonus = source.GetTotalSuck(),
            expireTime = Time.time + source.Duration,
        });

        ApplyEffect();
        return true;
    }

    /// <summary>
    /// 移除由指定实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(PhantomNightmareBuff source)
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
            prop.suckBlood = baseSuck;
            Destroy(this);
        }
    }

    /// <summary>
    /// 按当前全部层的吸血加成求和后写入目标的 suckBlood 字段。
    /// </summary>
    private void ApplyEffect()
    {
        if (prop == null)
            return;

        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].suckBonus;

        prop.suckBlood = baseSuck + total;
    }

    private void OnDisable()
    {
        RestoreEverything();
    }

    /// <summary>
    /// 还原基础吸血并清空层列表。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
            prop.suckBlood = baseSuck;

        layers.Clear();
    }
}
