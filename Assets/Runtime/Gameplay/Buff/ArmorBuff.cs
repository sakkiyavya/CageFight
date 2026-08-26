using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 护甲：数值类增益 Buff，每层提供固定免伤点数（默认 10 点）。
/// 可无限叠加：每层独立计时、独立快照免伤值，总免伤为各层相加（如 2 层 = 10 + 10 = 20 点）。
/// 免伤在伤害结算（DamageComputor）的乘算修正之后直接扣除，最低为 0。
/// 数值只跟随“创造者单位等级”（施法者单位自身的等级，通过 GetCreatorLevel 接缝读取；
/// 等级系统未建成前返回 0，即每层恰好 baseArmor 点免伤），不受局外玩家魔法等级影响。
/// 拥有护甲期间（任意层数激活）目标图像显示蓝色渐变呼吸光效，与层数无关。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class ArmorBuff : BuffBase
{
    [Header("护甲数值")]
    [SerializeField, Min(0)]
    private int baseArmor = 10;         // 每层基础免伤点数（10 点）。
    [SerializeField, Min(0)]
    private int levelArmor = 0;         // 创造者单位每级额外免伤点数（等级系统未建成前为 0）。
    [SerializeField, Min(0.1f)]
    private float duration = 10f;       // 每层持续时间秒。

    [Header("蓝色呼吸表现")]
    [SerializeField, Min(0f)]
    private float breathSpeed = 2f;     // 呼吸频率（每秒周期数）。
    [SerializeField, Min(0f)]
    private float breathStrength = 0.3f;// 呼吸强度（最大蓝色混合比例）。
    [SerializeField]
    private Color breathColor = new Color(0.25f, 0.6f, 1f, 1f); // 呼吸目标蓝色。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => false;

    /// <summary>每层持续时间，供层管理器读取。</summary>
    public float Duration => duration;
    /// <summary>呼吸频率，供层管理器读取。</summary>
    public float BreathSpeed => breathSpeed;
    /// <summary>呼吸强度，供层管理器读取。</summary>
    public float BreathStrength => breathStrength;
    /// <summary>呼吸目标蓝色，供层管理器读取。</summary>
    public Color BreathColor => breathColor;

    /// <summary>供运行时创建/配置实例时设置每层持续时间。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }

    #region Buff 生命周期
    /// <summary>
    /// 不带来源信息的施加入口：没有施法者数据，每层免伤退化为仅基础免伤。
    /// 直接调用方应优先使用带 <see cref="Damage"/> 的重载，以继承创造者单位等级。
    /// </summary>
    protected override bool ApplyBuffInternal(GameObjectProperty prop)
    {
        return ApplyBuff(prop, Damage.DefaultDamage);
    }

    /// <summary>
    /// 从来源伤害中解析创造者单位，计算每层免伤（基础 + 创造者等级加成）并叠加一层护甲。
    /// </summary>
    /// <param name="prop">需要施加护甲的目标属性组件。</param>
    /// <param name="damage">包含创造者（施法者）与阵营的伤害数据。</param>
    /// <returns>目标存活且成功叠加一层时返回 <see langword="true"/>。</returns>
    public override bool ApplyBuff(GameObjectProperty prop, Damage damage)
    {
        // 建筑免疫所有 Buff（与基类统一拦截一致）。
        if (prop == null || prop.isDead || prop.GetComponent<BuildingBase>() != null)
            return false;

        GameObjectProperty creatorProp = null;
        if (damage.source != null)
            creatorProp = damage.source.GetComponent<GameObjectProperty>();

        int armor = Mathf.Max(0, baseArmor + GetCreatorLevel(creatorProp) * levelArmor);

        ArmorState state = prop.GetComponent<ArmorState>();
        if (state == null)
            state = prop.gameObject.AddComponent<ArmorState>();

        return state.AddLayer(this, armor);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层护甲。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        ArmorState state = prop.GetComponent<ArmorState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 创造者单位等级接缝：等级系统建成后在此读取施法者单位的等级并换算免伤加成
    /// （例如从未来的单位等级组件取得 level，返回 level）。
    /// 当前等级系统未实现，返回 0 表示每层恰好造成 baseArmor 点免伤。
    /// </summary>
    /// <param name="creatorProp">创造者（施法者）的属性组件；缺失或已回收时返回 0。</param>
    protected virtual int GetCreatorLevel(GameObjectProperty creatorProp)
    {
        return 0;
    }
    #endregion
}

/// <summary>
/// 目标身上的护甲层管理器：无限叠加、每层独立到期，总免伤按各层相加后写入目标属性，
/// 同时驱动目标图像的蓝色渐变呼吸表现（任意层激活即呼吸）。
/// </summary>
internal class ArmorState : MonoBehaviour
{
    /// <summary>单层护甲快照，施加瞬间锁定免伤数值。</summary>
    private class Layer
    {
        public ArmorBuff source;    // 施加该层的实例，用于取消时匹配。
        public int armor;           // 本层快照的免伤点数。
        public float expireTime;    // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private float breathSpeed = 2f;
    private float breathStrength = 0.3f;
    private Color breathColor = new Color(0.25f, 0.6f, 1f, 1f);

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    /// <summary>
    /// 无限叠加一层护甲；首个层加入时快照呼吸表现参数。
    /// </summary>
    public bool AddLayer(ArmorBuff source, int armor)
    {
        if (source == null || prop == null)
            return false;

        if (layers.Count == 0)
        {
            breathSpeed = source.BreathSpeed;
            breathStrength = source.BreathStrength;
            breathColor = source.BreathColor;
        }

        layers.Add(new Layer
        {
            source = source,
            armor = armor,
            expireTime = Time.time + source.Duration,
        });

        ApplyEffect();
        return true;
    }

    /// <summary>
    /// 移除由指定 Buff 实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(ArmorBuff source)
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

        UpdateBreathing();
    }

    /// <summary>
    /// 按当前全部层的免伤求和后写入目标属性的 armor（免伤）字段。
    /// </summary>
    private void ApplyEffect()
    {
        if (prop == null)
            return;

        int total = 0;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].armor;

        prop.armor = total;
    }

    /// <summary>
    /// 驱动蓝色渐变呼吸：以正弦波在原始颜色与蓝色之间往复混合，层数多少不影响表现。
    /// </summary>
    private void UpdateBreathing()
    {
        if (layers.Count == 0 || renderers == null || renderers.Length == 0)
            return;

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * breathSpeed * Mathf.PI * 2f);
        float strength = breathStrength * wave;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = Color.Lerp(originalColors[i], breathColor, strength);
            color.a = originalColors[i].a;
            renderers[i].color = color;
        }
    }

    private void RemoveAt(int index)
    {
        layers.RemoveAt(index);
        ApplyEffect();

        if (layers.Count == 0)
            Destroy(this);
    }

    private void OnDisable()
    {
        RestoreEverything();
    }

    /// <summary>
    /// 清空目标免伤，清空层并恢复所有渲染器原始颜色。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
            prop.armor = 0;

        layers.Clear();

        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = originalColors[i];
        }
    }
}
