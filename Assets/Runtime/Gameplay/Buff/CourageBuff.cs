using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 勇气：数值类增益 Buff，每层提供 3% 最终伤害减免，
/// 并受局外“防御魔法等级”（UserGlobalInfo.DefenseMagicLevel）影响——每一级额外增加 0.3%。
/// 可叠加：每层独立计时、独立快照减免比例，总减免为各层相加（上限 100%），
/// 层到期时对应减免同步移除；层数上限由 maxLayers 配置（0 = 不设上限）。
/// 减免在伤害结算（DamageComputor）的最后阶段按比例乘算（最终伤害减免），
/// 与护甲的固定免伤（armor）、愤怒的受伤倍率（damageTakenMultiplier）互不冲突。
/// 拥有勇气期间（任意层数激活）目标图像显示淡黄色渐变呼吸光效，与层数无关。无音效。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class CourageBuff : BuffBase
{
    [Header("勇气数值")]
    [SerializeField, Min(0f)]
    private float baseReduction = 0.03f;    // 每层基础最终伤害减免比例（3%）。
    [SerializeField, Min(0f)]
    private float levelReduction = 0.003f;  // 每级局外防御魔法等级额外减免比例（0.3%）。
    [SerializeField, Min(0.1f)]
    private float duration = 10f;           // 每层持续时间秒。
    [SerializeField, Min(0)]
    private int maxLayers = 0;              // 层数上限（0 = 不设上限）。

    [Header("淡黄色呼吸表现")]
    [SerializeField, Min(0f)]
    private float breathSpeed = 2f;         // 呼吸频率（每秒周期数）。
    [SerializeField, Min(0f)]
    private float breathStrength = 0.3f;    // 呼吸强度（最大淡黄色混合比例）。
    [SerializeField]
    private Color breathColor = new Color(1f, 0.92f, 0.55f, 1f); // 呼吸目标淡黄色。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => false;

    /// <summary>每层持续时间，供层管理器读取。</summary>
    public float Duration => duration;
    /// <summary>层数上限（0 = 不设上限），供层管理器读取。</summary>
    public int MaxLayers => maxLayers;
    /// <summary>呼吸频率，供层管理器读取。</summary>
    public float BreathSpeed => breathSpeed;
    /// <summary>呼吸强度，供层管理器读取。</summary>
    public float BreathStrength => breathStrength;
    /// <summary>呼吸目标淡黄色，供层管理器读取。</summary>
    public Color BreathColor => breathColor;

    /// <summary>供运行时创建/配置实例时设置每层持续时间。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }

    /// <summary>供运行时创建/配置实例时设置层数上限（0 = 不设上限）。</summary>
    public void SetMaxLayers(int count)
    {
        maxLayers = Mathf.Max(0, count);
    }

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层勇气；不设层数上限，每层独立计时与快照减免比例。
    /// </summary>
    protected override bool ApplyBuffInternal(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        CourageState state = prop.GetComponent<CourageState>();
        if (state == null)
            state = prop.gameObject.AddComponent<CourageState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层勇气。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        CourageState state = prop.GetComponent<CourageState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 计算单层最终伤害减免比例：基础 3% + 局外防御魔法等级 × 0.3%。
    /// </summary>
    public float GetTotalReduction()
    {
        int level = UserGlobalInfo.Instance != null
            ? UserGlobalInfo.Instance.DefenseMagicLevel
            : 0;
        return baseReduction + level * levelReduction;
    }
    #endregion
}

/// <summary>
/// 目标身上的勇气层管理器：无限叠加、每层独立到期，总减免按各层相加（上限 100%）
/// 写入目标属性的 damageReduction（最终伤害减免）字段，
/// 同时驱动目标图像的淡黄色渐变呼吸表现（任意层激活即呼吸）。
/// </summary>
internal class CourageState : MonoBehaviour
{
    /// <summary>单层勇气快照，施加瞬间锁定减免比例。</summary>
    private class Layer
    {
        public CourageBuff source;  // 施加该层的实例，用于取消时匹配。
        public float reduction;     // 本层快照的减免比例。
        public float expireTime;    // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private float breathSpeed = 2f;
    private float breathStrength = 0.3f;
    private Color breathColor = new Color(1f, 0.92f, 0.55f, 1f);
    private int maxLayers = 0;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    /// <summary>
    /// 叠加一层勇气；首个层加入时快照呼吸表现参数与层数上限，
    /// 已到达上限时本次叠加不生效。
    /// </summary>
    public bool AddLayer(CourageBuff source)
    {
        if (source == null || prop == null)
            return false;

        if (layers.Count == 0)
        {
            breathSpeed = source.BreathSpeed;
            breathStrength = source.BreathStrength;
            breathColor = source.BreathColor;
            maxLayers = source.MaxLayers;
        }

        if (maxLayers > 0 && layers.Count >= maxLayers)
            return false;

        layers.Add(new Layer
        {
            source = source,
            reduction = source.GetTotalReduction(),
            expireTime = Time.time + source.Duration,
        });

        ApplyEffect();
        return true;
    }

    /// <summary>
    /// 移除由指定 Buff 实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(CourageBuff source)
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

        UpdateBreathing();
    }

    /// <summary>
    /// 按当前全部层的减免比例求和（上限 100%）后写入目标的 damageReduction 字段。
    /// </summary>
    private void ApplyEffect()
    {
        if (prop == null)
            return;

        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].reduction;

        prop.damageReduction = Mathf.Clamp01(total);
    }

    /// <summary>
    /// 驱动淡黄色渐变呼吸：以正弦波在原始颜色与淡黄色之间往复混合，层数多少不影响表现。
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
        {
            prop.damageReduction = 0f;
            Destroy(this);
        }
    }

    private void OnDisable()
    {
        RestoreEverything();
    }

    /// <summary>
    /// 清空目标最终伤害减免，清空层并恢复所有渲染器原始颜色。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
            prop.damageReduction = 0f;

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
