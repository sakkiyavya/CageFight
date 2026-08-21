using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 巨化增益：体型（根物体缩放）与最大生命按比例增大，差值补齐为临时生命值
/// （层消失时临时生命同步扣除），并获得抗击退加成。
/// 基础：体型/最大生命 +7%、抗击退 +0.5；受局外“防御魔法等级”
/// （UserGlobalInfo.DefenseMagicLevel）影响——每级额外 +0.7% 体型/生命、+0.1 抗击退。
/// 可无限叠加：每层独立计时、独立快照加成，总效果为各层比例相加（如 2 层 = 7% + 7% = 14%）。
/// 每获得一层巨化时，目标弹动一下并播放配置的获得音效（默认 Huge buff）。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class GiantBuff : BuffBase
{
    [Header("巨化数值")]
    [SerializeField, Min(0f)]
    private float basePercent = 0.07f;      // 基础体型/最大生命加成比例（7%）。
    [SerializeField, Min(0f)]
    private float levelPercent = 0.007f;    // 每级局外防御魔法等级额外加成比例（0.7%）。
    [SerializeField, Min(0f)]
    private float baseAntiRepel = 0.5f;     // 基础抗击退加成。
    [SerializeField, Min(0f)]
    private float levelAntiRepel = 0.1f;    // 每级局外防御魔法等级额外抗击退加成。
    [SerializeField, Min(0.1f)]
    private float duration = 10f;           // 每层持续时间秒。

    [Header("获得表现：弹动")]
    [SerializeField, Min(0.01f)]
    private float bounceDuration = 0.3f;    // 弹动持续秒。
    [SerializeField, Min(0f)]
    private float bounceAmount = 0.2f;      // 弹动最大缩放增幅。

    [Header("获得音效")]
    [SerializeField, ResourceKey(typeof(AudioClip))]
    private string buffSoundKey = "Huge buff"; // 获得巨化时播放的音频资源键。
    [SerializeField, Range(0f, 1f)]
    private float buffSoundVolume = 1f;     // 获得音效音量。
    [SerializeField, Range(0, 256)]
    private int buffSoundPriority = 32;     // 获得音效优先级（越小越高）。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => false;

    /// <summary>每层持续时间，供层管理器读取。</summary>
    public float Duration => duration;
    /// <summary>弹动持续秒，供层管理器读取。</summary>
    public float BounceDuration => bounceDuration;
    /// <summary>弹动最大缩放增幅，供层管理器读取。</summary>
    public float BounceAmount => bounceAmount;
    /// <summary>获得音效资源键，供层管理器读取。</summary>
    public string BuffSoundKey => buffSoundKey;
    /// <summary>获得音效音量，供层管理器读取。</summary>
    public float BuffSoundVolume => buffSoundVolume;
    /// <summary>获得音效优先级，供层管理器读取。</summary>
    public int BuffSoundPriority => buffSoundPriority;

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层巨化；不设层数上限，每层独立计时与快照加成。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        GiantState state = prop.GetComponent<GiantState>();
        if (state == null)
            state = prop.gameObject.AddComponent<GiantState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层巨化。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        GiantState state = prop.GetComponent<GiantState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 计算单层体型/最大生命加成比例：基础 7% + 局外防御魔法等级 × 0.7%。
    /// </summary>
    public float GetTotalPercent()
    {
        return basePercent + GetDefenseMagicLevel() * levelPercent;
    }

    /// <summary>
    /// 计算单层抗击退加成：基础 0.5 + 局外防御魔法等级 × 0.1。
    /// </summary>
    public float GetTotalAntiRepel()
    {
        return baseAntiRepel + GetDefenseMagicLevel() * levelAntiRepel;
    }

    private int GetDefenseMagicLevel()
    {
        return UserGlobalInfo.Instance != null
            ? UserGlobalInfo.Instance.DefenseMagicLevel
            : 0;
    }
    #endregion
}

/// <summary>
/// 目标身上的巨化层管理器：无限叠加、每层独立到期，总加成按各层比例相加；
/// 每层加入时补齐的临时生命值在对应层消失时同步扣除；
/// 每获得一层时执行一次弹动表现并播放获得音效。
/// </summary>
internal class GiantState : MonoBehaviour
{
    /// <summary>单层巨化快照，施加瞬间锁定加成与临时生命。</summary>
    private class Layer
    {
        public GiantBuff source;    // 施加该层的实例，用于取消时匹配。
        public float percent;       // 本层快照的体型/生命加成比例。
        public float antiBonus;     // 本层快照的抗击退加成。
        public int tempHp;          // 本层加入时补齐的临时生命值，本层消失时扣除。
        public float expireTime;    // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private CharacterHealth health;
    private int baseMaxHp;                    // 首层施加时快照的基础最大生命。
    private float baseAntiRepel;              // 首层施加时快照的基础抗击退。
    private Vector3 baseScale;                // 首层施加时快照的基础体型。

    private AudioSource soundAudio;           // 获得音效音频源（首层施加时解析）。
    private Coroutine bounceRoutine;          // 当前正在播放的弹动协程。
    private float bounceDuration = 0.3f;
    private float bounceAmount = 0.2f;
    private string soundKey = "Huge buff";
    private float soundVolume = 1f;
    private int soundPriority = 32;
    private bool warnedMissingSound;          // 是否已输出过获得音效缺失警告（一次性）。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
        ResolveSoundAudio();
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
    /// 无限叠加一层巨化：重算最大生命并补齐差值临时生命，累加抗击退与体型，
    /// 然后执行一次弹动；获得音效仅在首层施加时播放，后续叠加不触发。
    /// </summary>
    public bool AddLayer(GiantBuff source)
    {
        if (source == null || prop == null)
            return false;

        bool isFirstLayer = layers.Count == 0;
        if (isFirstLayer)
        {
            baseMaxHp = prop.maxHp;
            baseAntiRepel = prop.antiRepel;
            baseScale = prop.transform.localScale;
            bounceDuration = source.BounceDuration;
            bounceAmount = source.BounceAmount;
            soundKey = source.BuffSoundKey;
            soundVolume = source.BuffSoundVolume;
            soundPriority = source.BuffSoundPriority;
            ResolveSoundAudio();
        }

        int prevMax = prop.maxHp;
        float percent = source.GetTotalPercent();

        layers.Add(new Layer
        {
            source = source,
            percent = percent,
            antiBonus = source.GetTotalAntiRepel(),
            tempHp = 0,
            expireTime = Time.time + source.Duration,
        });

        int newMax = Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * (1f + TotalPercent())));
        int gain = Mathf.Max(0, newMax - prevMax);
        layers[layers.Count - 1].tempHp = gain;

        ApplyToProp(newMax, gain);
        if (isFirstLayer)
            PlayBuffSound();
        StartBounce();
        return true;
    }

    /// <summary>
    /// 移除由指定实例施加的一层巨化，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(GiantBuff source)
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
        Layer layer = layers[index];
        layers.RemoveAt(index);

        int newMax = Mathf.Max(1, Mathf.RoundToInt(baseMaxHp * (1f + TotalPercent())));
        // 本层消失：扣除其补齐的临时生命。
        ApplyToProp(newMax, -layer.tempHp);

        if (layers.Count == 0)
        {
            health.SetMaxHp(baseMaxHp);
            prop.antiRepel = baseAntiRepel;
            prop.transform.localScale = baseScale;
            Destroy(this);
        }
    }

    /// <summary>
    /// 将当前全部层的比例求和后应用到最大生命、临时生命、抗击退与体型。
    /// </summary>
    private void ApplyToProp(int newMax, int hpDelta)
    {
        health.SetMaxHp(newMax);
        health.SetHpKeepDeadState(Mathf.Clamp(prop.currentHp + hpDelta, 0, newMax));
        prop.antiRepel = Mathf.Max(0f, baseAntiRepel + TotalAnti());
        prop.transform.localScale = baseScale * (1f + TotalPercent());
    }

    /// <summary>
    /// 播放获得巨化音效；资源键或片段缺失时输出一次性警告，避免静默失败。
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
                Debug.LogWarning($"[GiantBuff] 音频资源 {soundKey} 未加载，获得音效无法播放。", this);
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
    /// 启动一次弹动：以衰减正弦在巨化体型基础上做缩放脉冲，结束后精确回到巨化体型。
    /// 弹动期间若层数变化，每帧按当前层重算，保证结束值正确。
    /// </summary>
    private void StartBounce()
    {
        if (bounceRoutine != null)
            StopCoroutine(bounceRoutine);

        bounceRoutine = StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / bounceDuration;
            float wave = Mathf.Sin(t * Mathf.PI * 2.5f) * (1f - t);
            prop.transform.localScale =
                baseScale * (1f + TotalPercent()) * (1f + wave * bounceAmount);
            yield return null;
        }

        prop.transform.localScale = baseScale * (1f + TotalPercent());
        bounceRoutine = null;
    }

    private float TotalPercent()
    {
        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].percent;
        return total;
    }

    private float TotalAnti()
    {
        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].antiBonus;
        return total;
    }

    private void OnDisable()
    {
        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
            bounceRoutine = null;
        }

        RestoreEverything();
    }

    /// <summary>
    /// 还原基础最大生命、抗击退与体型，并清空层列表。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
        {
            health.SetMaxHp(baseMaxHp);
            prop.antiRepel = baseAntiRepel;
            prop.transform.localScale = baseScale;
        }

        layers.Clear();
    }
}
