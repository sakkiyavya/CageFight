using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 愤怒增益：目标获得增伤、受伤增加与暴击概率。
/// 每层：增伤 +6%、受伤增加 +6%、造成 200% 伤害的概率 +6%；
/// 受局外“攻击魔法等级”（UserGlobalInfo.AttackMagicLevel）影响——每一级额外 +0.6%。
/// 可无限叠加：每层独立计时、独立快照加成，总效果为各层比例相加。
/// 首次施加播放 Violent 音效，叠加不触发；拥有期间目标图像显示红色渐变呼吸。
/// 增伤/受伤/暴击经 GameObjectProperty 新增的战斗修正字段接入 DamageComputor。
/// </summary>
public class AngerBuff : BuffBase
{
    [Header("愤怒数值")]
    [SerializeField, Min(0f)]
    private float basePercent = 0.06f;      // 每层基础增伤/受伤/暴击概率（6%）。
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
    private Color breathColor = new Color(1f, 0.18f, 0.1f, 1f); // 呼吸目标红色（从上往下透明→显示的渐变律动）。

    [Header("获得音效（仅首次施加触发，叠加不触发）")]
    [SerializeField, ResourceKey(typeof(AudioClip))]
    private string buffSoundKey = "Violent";  // 首次获得愤怒时播放的音频资源键。
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
    /// 叠加一层愤怒；不设层数上限，每层独立计时与快照加成比例。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        AngerState state = prop.GetComponent<AngerState>();
        if (state == null)
            state = prop.gameObject.AddComponent<AngerState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层愤怒。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        AngerState state = prop.GetComponent<AngerState>();
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
/// 目标身上的愤怒层管理器：无限叠加、每层独立到期，总加成按各层比例相加，
/// 同时写入增伤/受伤/暴击修正字段，并驱动橙红渐变呼吸；获得音效仅在首层播放。
/// </summary>
internal class AngerState : MonoBehaviour
{
    /// <summary>单层愤怒快照，施加瞬间锁定加成比例。</summary>
    private class Layer
    {
        public AngerBuff source;    // 施加该层的实例，用于取消时匹配。
        public float percent;       // 本层快照的加成比例。
        public float expireTime;    // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private float baseDamageMultiplier = 1f;      // 首层施加时快照的基础增伤倍率。
    private float baseDamageTakenMultiplier = 1f; // 首层施加时快照的基础受伤倍率。
    private float baseCritChance = 0f;            // 首层施加时快照的基础暴击概率。
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private float breathSpeed = 1.2f;
    private float breathMinAlpha = 0.15f;
    private float breathMaxAlpha = 0.4f;
    private Color breathColor = new Color(1f, 0.18f, 0.1f, 1f);
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
    /// 无限叠加一层愤怒；首个层加入时快照基础修正字段与呼吸、音效参数。
    /// 获得音效仅在首层施加时播放，后续叠加不触发。
    /// </summary>
    public bool AddLayer(AngerBuff source)
    {
        if (source == null || prop == null)
            return false;

        bool isFirstLayer = layers.Count == 0;
        if (isFirstLayer)
        {
            baseDamageMultiplier = prop.damageMultiplier;
            baseDamageTakenMultiplier = prop.damageTakenMultiplier;
            baseCritChance = prop.critChance;
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
    /// 移除由指定实例施加的一层愤怒，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(AngerBuff source)
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

    private void RemoveAt(int index)
    {
        layers.RemoveAt(index);
        ApplyEffect();

        if (layers.Count == 0)
        {
            prop.damageMultiplier = baseDamageMultiplier;
            prop.damageTakenMultiplier = baseDamageTakenMultiplier;
            prop.critChance = baseCritChance;
            Destroy(this);
        }
    }

    /// <summary>
    /// 按当前全部层的比例求和后写入增伤、受伤与暴击概率修正。
    /// </summary>
    private void ApplyEffect()
    {
        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].percent;

        prop.damageMultiplier = baseDamageMultiplier * (1f + total);
        prop.damageTakenMultiplier = baseDamageTakenMultiplier * (1f + total);
        prop.critChance = Mathf.Clamp01(baseCritChance + total);
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

    /// <summary>
    /// 播放首次获得愤怒音效；资源键或片段缺失时输出一次性警告，避免静默失败。
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
                Debug.LogWarning($"[AngerBuff] 音频资源 {soundKey} 未加载，获得音效无法播放。", this);
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

    private void OnDisable()
    {
        RestoreEverything();
    }

    /// <summary>
    /// 还原基础增伤/受伤/暴击修正与渲染器颜色，并清空层列表。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
        {
            prop.damageMultiplier = baseDamageMultiplier;
            prop.damageTakenMultiplier = baseDamageTakenMultiplier;
            prop.critChance = baseCritChance;
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
