using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 须臾之烬：数值类增益 Buff，每层获得“基础最大生命的 7%”临时生命值，
/// 并受局外“防御魔法等级”（UserGlobalInfo.DefenseMagicLevel）影响——每一级额外增加 0.7%。
/// 叠加公式与“巨化”一致：层管理、无层数上限、加法叠加、每层独立计时与快照、逐层到期；
/// 每层加入时把最大生命抬升对应数值，差值补齐为当前生命（临时生命），
/// 该层消失（时限到期/取消）时同步扣回其临时生命。
/// 被击破：当前生命降到基础最大生命及以下（临时生命全部被打光）时，全部层一起消失，
/// 只还原最大生命，不再额外扣血。
/// 拥有期间（任意层数激活）目标图像显示绿色渐变呼吸灯效果，与层数无关。无音效。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class TransientAshBuff : BuffBase
{
    [Header("须臾之烬数值")]
    [SerializeField, Min(0f)]
    private float basePercent = 0.07f;      // 每层基础临时生命比例（7% 基础最大生命）。
    [SerializeField, Min(0f)]
    private float levelPercent = 0.007f;    // 每级局外防御魔法等级额外比例（0.7%）。
    [SerializeField, Min(0.1f)]
    private float duration = 10f;           // 每层持续时间秒。

    [Header("绿色呼吸灯表现")]
    [SerializeField, Min(0f)]
    private float breathSpeed = 2f;         // 呼吸频率（每秒周期数）。
    [SerializeField, Min(0f)]
    private float breathStrength = 0.3f;    // 呼吸强度（最大绿色混合比例）。
    [SerializeField]
    private Color breathColor = new Color(0.3f, 1f, 0.45f, 1f); // 呼吸目标绿色。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => false;

    /// <summary>每层持续时间，供层管理器读取。</summary>
    public float Duration => duration;
    /// <summary>呼吸频率，供层管理器读取。</summary>
    public float BreathSpeed => breathSpeed;
    /// <summary>呼吸强度，供层管理器读取。</summary>
    public float BreathStrength => breathStrength;
    /// <summary>呼吸目标绿色，供层管理器读取。</summary>
    public Color BreathColor => breathColor;

    /// <summary>供运行时创建/配置实例时设置每层持续时间。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层须臾之烬；不设层数上限，每层独立计时与快照。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        TransientAshState state = prop.GetComponent<TransientAshState>();
        if (state == null)
            state = prop.gameObject.AddComponent<TransientAshState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层须臾之烬。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        TransientAshState state = prop.GetComponent<TransientAshState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 计算单层临时生命比例：基础 7% + 局外防御魔法等级 × 0.7%。
    /// </summary>
    public float GetTotalPercent()
    {
        int level = UserGlobalInfo.Instance != null
            ? UserGlobalInfo.Instance.DefenseMagicLevel
            : 0;
        return basePercent + level * levelPercent;
    }
    #endregion
}

/// <summary>
/// 目标身上的须臾之烬层管理器：无限叠加、每层独立到期，
/// 每层快照临时生命并在消失时同步扣回；当前生命降至基础最大生命及以下时
/// （临时生命被击破）清除全部层并只还原最大生命；
/// 同时驱动目标图像的绿色渐变呼吸灯表现（任意层激活即呼吸）。
/// </summary>
internal class TransientAshState : MonoBehaviour
{
    /// <summary>单层须臾之烬快照，施加瞬间锁定临时生命与到期时间。</summary>
    private class Layer
    {
        public TransientAshBuff source; // 施加该层的实例，用于取消时匹配。
        public int tempHp;              // 本层提供的临时生命（基础最大生命的百分比，施加瞬间快照）。
        public float expireTime;        // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private int baseMaxHp;              // 首层施加时快照的基础最大生命（临时生命的基准）。
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private float breathSpeed = 2f;
    private float breathStrength = 0.3f;
    private Color breathColor = new Color(0.3f, 1f, 0.45f, 1f);

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    /// <summary>
    /// 无限叠加一层须臾之烬：按基础最大生命快照临时生命，抬升最大生命并补齐当前生命。
    /// </summary>
    public bool AddLayer(TransientAshBuff source)
    {
        if (source == null || prop == null)
            return false;

        bool isFirstLayer = layers.Count == 0;
        if (isFirstLayer)
        {
            baseMaxHp = prop.maxHp;
            breathSpeed = source.BreathSpeed;
            breathStrength = source.BreathStrength;
            breathColor = source.BreathColor;
        }

        int tempHp = Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * source.GetTotalPercent()));
        int prevMax = prop.maxHp;

        layers.Add(new Layer
        {
            source = source,
            tempHp = tempHp,
            expireTime = Time.time + source.Duration,
        });

        int newMax = prevMax + tempHp;
        prop.maxHp = newMax;
        prop.currentHp = Mathf.Clamp(prop.currentHp + tempHp, 0, newMax);
        return true;
    }

    /// <summary>
    /// 移除由指定实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(TransientAshBuff source)
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
        if (layers.Count == 0 || prop == null)
            return;

        // 倒序清理到期层；到期层扣回其临时生命。
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (Time.time >= layers[i].expireTime)
                RemoveAt(i);
        }

        // 被击破：当前生命降到基础最大生命及以下（临时生命全部被打光），
        // 清除全部层并只还原最大生命，不额外扣血。
        if (layers.Count > 0 && prop.currentHp <= baseMaxHp)
        {
            layers.Clear();
            prop.maxHp = baseMaxHp;
            prop.currentHp = Mathf.Clamp(prop.currentHp, 0, baseMaxHp);
            Destroy(this);
            return;
        }

        UpdateBreathing();
    }

    private void RemoveAt(int index)
    {
        Layer layer = layers[index];
        layers.RemoveAt(index);

        // 重算剩余层提供的最大生命并扣回本层临时生命。
        int newMax = baseMaxHp;
        for (int i = 0; i < layers.Count; i++)
            newMax += layers[i].tempHp;

        prop.maxHp = newMax;
        prop.currentHp = Mathf.Clamp(prop.currentHp - layer.tempHp, 0, newMax);

        if (layers.Count == 0)
            Destroy(this);
    }

    /// <summary>
    /// 驱动绿色渐变呼吸灯：以正弦波在原始颜色与绿色之间往复混合，层数多少不影响表现。
    /// </summary>
    private void UpdateBreathing()
    {
        if (renderers == null || renderers.Length == 0)
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

    private void OnDisable()
    {
        RestoreEverything();
    }

    /// <summary>
    /// 还原基础最大生命（保留当前生命在有效范围内）、清空层并恢复所有渲染器原始颜色。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
        {
            prop.maxHp = baseMaxHp;
            prop.currentHp = Mathf.Clamp(prop.currentHp, 0, baseMaxHp);
        }

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
