using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 狂暴增益：提升目标攻速（atkRate）与移速（moveSpeed）。
/// 基础加成 6%，并受局外“攻击魔法等级”（UserGlobalInfo.AttackMagicLevel）影响——每一级额外增加 0.6%。
/// 可无限叠加：每层独立计时、独立快照加成比例，总效果为各层比例相加（如 2 层 = 6% + 6% = 12%）。
/// 拥有狂暴期间（任意层数激活）目标图像显示红色渐变呼吸效果，与层数无关。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class RageBuff : BuffBase
{
    [Header("狂暴数值")]
    [SerializeField, Min(0f)]
    private float basePercent = 0.06f;      // 基础攻速/移速加成比例（6%）。
    [SerializeField, Min(0f)]
    private float levelPercent = 0.006f;    // 每级局外攻击魔法等级额外加成比例（0.6%）。
    [SerializeField, Min(0.1f)]
    private float duration = 10f;           // 每层持续时间秒。

    [Header("红色渐变呼吸表现")]
    [SerializeField, Min(0f)]
    private float breathSpeed = 1.2f;       // 呼吸频率（每秒周期数，放缓避免闪烁感）。
    [SerializeField, Range(0f, 1f)]
    private float breathMinAlpha = 0.15f;   // 律动透明度下限。
    [SerializeField, Range(0f, 1f)]
    private float breathMaxAlpha = 0.4f;    // 律动透明度上限。
    [SerializeField]
    private Color breathColor = new Color(1f, 0.2f, 0.15f, 1f); // 呼吸目标红色（从上往下透明→显示的渐变律动）。

    [Header("获得音效（仅首次施加触发，叠加不触发）")]
    [SerializeField, ResourceKey(typeof(AudioClip))]
    private string buffSoundKey = "Violent";  // 首次获得狂暴时播放的音频资源键。
    [SerializeField, Range(0f, 1f)]
    private float buffSoundVolume = 1f;       // 获得音效音量。
    [SerializeField, Range(0, 256)]
    private int buffSoundPriority = 32;       // 获得音效优先级（越小越高）。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => false;

    /// <summary>每层持续时间，供层管理器读取。</summary>
    public float Duration => duration;

    /// <summary>供运行时创建/配置实例时设置每层持续时间。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }
    /// <summary>呼吸频率，供层管理器读取。</summary>
    public float BreathSpeed => breathSpeed;
    /// <summary>律动透明度下限，供层管理器读取。</summary>
    public float BreathMinAlpha => breathMinAlpha;
    /// <summary>律动透明度上限，供层管理器读取。</summary>
    public float BreathMaxAlpha => breathMaxAlpha;
    /// <summary>呼吸目标红色，供层管理器读取。</summary>
    public Color BreathColor => breathColor;
    /// <summary>获得音效资源键，供层管理器读取。</summary>
    public string BuffSoundKey => buffSoundKey;
    /// <summary>获得音效音量，供层管理器读取。</summary>
    public float BuffSoundVolume => buffSoundVolume;
    /// <summary>获得音效优先级，供层管理器读取。</summary>
    public int BuffSoundPriority => buffSoundPriority;

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层狂暴；不设层数上限，每层独立计时与快照加成比例。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        RageState state = prop.GetComponent<RageState>();
        if (state == null)
            state = prop.gameObject.AddComponent<RageState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层狂暴。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        RageState state = prop.GetComponent<RageState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 计算单层加成比例：基础 6% + 局外攻击魔法等级 × 0.6%。
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
/// 目标身上的狂暴层管理器：无限叠加、每层独立到期，总加成按各层比例相加，
/// 同时驱动目标图像的红色垂直渐变呼吸表现（从上往下透明→显示，任意层激活即律动）。
/// </summary>
internal class RageState : MonoBehaviour
{
    /// <summary>单层狂暴快照，施加瞬间锁定加成比例。</summary>
    private class Layer
    {
        public RageBuff source;    // 施加该层的实例，用于取消时匹配。
        public float percent;      // 本层快照的加成比例。
        public float expireTime;   // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private float baseAtkRate;                 // 首层施加时快照的基础攻速。
    private float baseMoveSpeed;               // 首层施加时快照的基础移速。
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private float breathSpeed = 1.2f;
    private float breathMinAlpha = 0.15f;
    private float breathMaxAlpha = 0.4f;
    private Color breathColor = new Color(1f, 0.2f, 0.15f, 1f);
    private AudioSource soundAudio;            // 获得音效音频源（首层施加时解析）。
    private string soundKey = "Violent";
    private float soundVolume = 1f;
    private int soundPriority = 32;
    private bool warnedMissingSound;           // 是否已输出过获得音效缺失警告（一次性）。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        ResolveSoundAudio();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    /// <summary>
    /// 解析获得音效音频源：优先复用对象上的 AudioSource，没有则新建一个
    /// （仅首次生成时创建一次，不属于热路径）。
    /// </summary>
    private void ResolveSoundAudio()
    {
        if (soundAudio != null)
            return;

        soundAudio = GetComponent<AudioSource>();
        if (soundAudio == null)
        {
            soundAudio = gameObject.AddComponent<AudioSource>();
            soundAudio.playOnAwake = false;
            soundAudio.spatialBlend = 0f;
        }
    }

    /// <summary>
    /// 无限叠加一层狂暴；首个层加入时快照基础攻速/移速与呼吸、音效参数。
    /// 获得音效仅在首层施加时播放，后续叠加不触发。
    /// </summary>
    public bool AddLayer(RageBuff source)
    {
        if (source == null || prop == null)
            return false;

        bool isFirstLayer = layers.Count == 0;
        if (isFirstLayer)
        {
            baseAtkRate = prop.atkRate;
            baseMoveSpeed = prop.moveSpeed;
            breathSpeed = source.BreathSpeed;
            breathMinAlpha = source.BreathMinAlpha;
            breathMaxAlpha = source.BreathMaxAlpha;
            breathColor = source.BreathColor;
            soundKey = source.BuffSoundKey;
            soundVolume = source.BuffSoundVolume;
            soundPriority = source.BuffSoundPriority;
            ResolveSoundAudio();
        }

        layers.Add(new Layer
        {
            source = source,
            percent = source.GetTotalPercent(),
            expireTime = Time.time + source.Duration,
        });

        ApplyEffect();
        if (isFirstLayer)
            PlayBuffSound();
        return true;
    }

    /// <summary>
    /// 播放首次获得狂暴音效；资源键或片段缺失时输出一次性警告，避免静默失败。
    /// </summary>
    private void PlayBuffSound()
    {
        if (soundAudio == null || prop == null ||
            AudioManager.Instance == null || ResourceManager.Instance == null ||
            string.IsNullOrEmpty(soundKey))
            return;

        AudioClip clip = ResourceManager.Instance.GetAudio(soundKey);
        if (clip == null)
        {
            if (!warnedMissingSound)
            {
                warnedMissingSound = true;
                Debug.LogWarning($"[RageBuff] 音频资源 {soundKey} 未加载，获得音效无法播放。", this);
            }
            return;
        }

        soundAudio.clip = clip;
        soundAudio.volume = soundVolume;
        soundAudio.priority = soundPriority;
        AudioManager.Instance.PlayEffectAt(
            soundAudio,
            (uint)soundPriority,
            prop.transform);
    }

    /// <summary>
    /// 移除由指定减益实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(RageBuff source)
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

    private void RemoveAt(int index)
    {
        layers.RemoveAt(index);
        ApplyEffect();

        if (layers.Count == 0)
        {
            prop.atkRate = baseAtkRate;
            prop.moveSpeed = baseMoveSpeed;
            Destroy(this);
        }
    }

    /// <summary>
    /// 按当前全部层的比例求和后重算目标的攻速与移速。
    /// </summary>
    private void ApplyEffect()
    {
        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].percent;

        prop.atkRate = baseAtkRate * (1f + total);
        prop.moveSpeed = baseMoveSpeed * (1f + total);
    }

    /// <summary>
    /// 驱动红色渐变呼吸：以正弦波在原始颜色与红色之间往复混合，层数多少不影响表现。
    /// </summary>
    private void UpdateBreathing()
    {
        if (layers.Count == 0 || renderers == null || renderers.Length == 0)
            return;

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * breathSpeed * Mathf.PI * 2f);
        float strength = Mathf.Lerp(breathMinAlpha, breathMaxAlpha, wave);

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
    /// 还原基础攻速/移速，清空层并恢复所有渲染器原始颜色。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
        {
            prop.atkRate = baseAtkRate;
            prop.moveSpeed = baseMoveSpeed;
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
