using UnityEngine;

/// <summary>
/// 重伤：不可叠加的减益状态（所有生物单位通用）。
/// 两种获得途径：
/// 1. 自动获得：单位血量降到最大生命的 30% 以下（CharacterHealth 每帧检查并创建状态组件）；
/// 2. 被动施加：由攻击/技能等携带本 Buff 施加（不可叠加，重复施加只刷新持续时长）。
/// 效果（生效期间）：
/// - 攻速 -10%、移速 -10%（基础值按比例降低）；
/// - 被击退值 +30%（repelTakenMultiplier = 1.3）；
/// - 治疗效率 -50%（healTakenMultiplier = 0.5）。
/// 血量回升到 30% 以上且被动时长结束（或无被动）后效果解除，状态销毁。
/// 视觉：生效期间图像呈淡红色。无音效、无等级成长。
/// </summary>
public class HeavyWoundDebuff : BuffBase
{
    [Header("重伤数值")]
    [SerializeField, Range(0f, 1f)]
    private float atkPenalty = 0.1f;        // 攻速降低比例（10%）。
    [SerializeField, Range(0f, 1f)]
    private float movePenalty = 0.1f;       // 移速降低比例（10%）。
    [SerializeField, Min(0f)]
    private float repelIncrease = 0.3f;     // 被击退值增加比例（30%）。
    [SerializeField, Range(0f, 1f)]
    private float healPenalty = 0.5f;       // 治疗效率降低比例（50%）。
    [SerializeField, Min(0.1f)]
    private float duration = 5f;            // 被动施加的持续秒（重复施加只刷新时长）。
    [SerializeField, Range(0f, 1f)]
    private float lowHpPercent = 0.3f;      // 自动获得的血量阈值（30%）。

    [Header("淡红色表现")]
    [SerializeField, Range(0f, 1f)]
    private float tintStrength = 0.3f;      // 淡红色混合强度。
    [SerializeField]
    private Color paleRed = new Color(1f, 0.55f, 0.55f, 1f); // 淡红色目标色。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => true;

    /// <summary>攻速降低比例，供状态组件读取。</summary>
    public float AtkPenalty => atkPenalty;
    /// <summary>移速降低比例，供状态组件读取。</summary>
    public float MovePenalty => movePenalty;
    /// <summary>被击退值增加比例，供状态组件读取。</summary>
    public float RepelIncrease => repelIncrease;
    /// <summary>治疗效率降低比例，供状态组件读取。</summary>
    public float HealPenalty => healPenalty;
    /// <summary>被动持续秒，供状态组件读取。</summary>
    public float Duration => duration;
    /// <summary>自动获得的血量阈值比例，供状态组件读取。</summary>
    public float LowHpPercent => lowHpPercent;
    /// <summary>淡红色混合强度，供状态组件读取。</summary>
    public float TintStrength => tintStrength;
    /// <summary>淡红色目标色，供状态组件读取。</summary>
    public Color PaleRed => paleRed;

    #region Buff 生命周期
    /// <summary>
    /// 施加重伤（不可叠加）：已有状态时只刷新被动持续时长，不重复叠加。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        HeavyWoundState state = prop.GetComponent<HeavyWoundState>();
        if (state == null)
            state = prop.gameObject.AddComponent<HeavyWoundState>();

        return state.ApplyPassive(this);
    }

    /// <summary>
    /// 取消本实例施加的重伤被动来源。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        HeavyWoundState state = prop.GetComponent<HeavyWoundState>();
        return state != null && state.CancelPassive(this);
    }
    #endregion
}

/// <summary>
/// 目标身上的重伤状态（单例，不可叠加）：由低血量自动触发或被动施加激活，
/// 同时生效且共用同一份效果；血量回升且被动结束后自动解除并销毁。
/// </summary>
internal class HeavyWoundState : MonoBehaviour
{
    private GameObjectProperty prop;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;

    private bool active;                    // 效果当前是否生效。
    private float passiveExpireTime = -1f;  // 被动施加的到期时间；-1 表示无被动来源。
    private float lowHpPercent = 0.3f;
    private float atkPenalty = 0.1f;
    private float movePenalty = 0.1f;
    private float repelIncrease = 0.3f;
    private float healPenalty = 0.5f;
    private float tintStrength = 0.3f;
    private Color paleRed = new Color(1f, 0.55f, 0.55f, 1f);

    private float baseAtkRate;              // 创建时快照的基础攻速。
    private float baseMoveSpeed;            // 创建时快照的基础移速。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        if (prop != null)
        {
            baseAtkRate = prop.atkRate;
            baseMoveSpeed = prop.moveSpeed;
        }

        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    /// <summary>
    /// 被动施加：快照 Buff 配置并刷新被动持续时长（不可叠加，只刷新）。
    /// </summary>
    public bool ApplyPassive(HeavyWoundDebuff source)
    {
        if (source == null || prop == null)
            return false;

        atkPenalty = source.AtkPenalty;
        movePenalty = source.MovePenalty;
        repelIncrease = source.RepelIncrease;
        healPenalty = source.HealPenalty;
        lowHpPercent = source.LowHpPercent;
        tintStrength = source.TintStrength;
        paleRed = source.PaleRed;
        passiveExpireTime = Time.time + source.Duration;

        UpdateActive();
        return true;
    }

    /// <summary>
    /// 取消由指定实例施加的被动来源。
    /// </summary>
    public bool CancelPassive(HeavyWoundDebuff source)
    {
        passiveExpireTime = -1f;
        UpdateActive();
        return true;
    }

    private void Update()
    {
        // 自动来源：血量 <= 阈值比例。
        bool lowHp = prop != null && prop.maxHp > 0 &&
                     prop.currentHp <= prop.maxHp * lowHpPercent;
        // 被动来源：未到期。
        bool passive = passiveExpireTime > 0f && Time.time < passiveExpireTime;

        bool newActive = lowHp || passive;
        if (newActive != active)
        {
            active = newActive;
            ApplyEffect();
        }

        // 两个来源都消失时解除状态并销毁自身（血量恢复后 CharacterHealth 不再重建）。
        if (!active)
            Destroy(this);
    }

    private void UpdateActive()
    {
        bool lowHp = prop != null && prop.maxHp > 0 &&
                     prop.currentHp <= prop.maxHp * lowHpPercent;
        bool passive = passiveExpireTime > 0f && Time.time < passiveExpireTime;

        bool newActive = lowHp || passive;
        if (newActive != active)
        {
            active = newActive;
            ApplyEffect();
        }
    }

    /// <summary>
    /// 应用/解除重伤效果：攻速移速、受击退倍率、受治疗倍率与淡红色表现。
    /// </summary>
    private void ApplyEffect()
    {
        if (prop == null)
            return;

        if (active)
        {
            prop.atkRate = baseAtkRate * (1f - atkPenalty);
            prop.moveSpeed = baseMoveSpeed * (1f - movePenalty);
            prop.repelTakenMultiplier = 1f + repelIncrease;
            prop.healTakenMultiplier = 1f - healPenalty;
            TintPaleRed();
        }
        else
        {
            prop.atkRate = baseAtkRate;
            prop.moveSpeed = baseMoveSpeed;
            prop.repelTakenMultiplier = 1f;
            prop.healTakenMultiplier = 1f;
            RestoreColors();
        }
    }

    /// <summary>
    /// 生效期间图像呈淡红色（在原始颜色与淡红色之间按强度混合，保持透明度）。
    /// </summary>
    private void TintPaleRed()
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = Color.Lerp(originalColors[i], paleRed, tintStrength);
            color.a = originalColors[i].a;
            renderers[i].color = color;
        }
    }

    private void RestoreColors()
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = originalColors[i];
        }
    }

    private void OnDisable()
    {
        active = false;
        ApplyEffect();
    }
}
