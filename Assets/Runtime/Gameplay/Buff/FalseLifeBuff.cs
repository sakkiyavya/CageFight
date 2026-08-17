using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 妄业之力：死亡诅咒类 Buff（防御魔法体系）。
/// 施加后目标叠加一层“妄业之力”，层数无限叠加，且对已持有该 Buff 的单位依旧生效
/// （不免疫、不唯一，叠多少层都可以）。
/// 当目标生命归零时结算诅咒：不立即死亡，而是恢复一部分最大生命值
/// （基础 80%，防御魔法等级每级额外 +8%，等级取 UserGlobalInfo.DefenseMagicLevel），
/// 并在 n × 每层秒数内持续扣除生命值直至归零（n = 结算时的层数，
/// 因此叠层越多，“假死”持续越久），扣血结束后进入常规死亡流程。
/// 持有期间（有活跃层或正在扣血结算）目标图像变黑 20%；
/// 触发结算（目标死亡）时播放音效“False life”。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class FalseLifeBuff : BuffBase
{
    [Header("妄业之力数值")]
    [SerializeField, Min(0.1f)]
    private float perLayerSeconds = 5f;             // 每层持续秒；死亡结算扣血时长 = 层数 × 本值。
    [SerializeField, Range(0f, 1f)]
    private float baseRestorePercent = 0.8f;        // 死亡后恢复的最大生命比例（80%）。
    [SerializeField, Range(0f, 0.2f)]
    private float levelRestorePercent = 0.08f;      // 防御魔法每级额外恢复比例（8%）。

    public override float buffSustainTime => perLayerSeconds;
    public override bool isDeBuff => false;

    /// <summary>每层持续秒，供层管理器读取。</summary>
    public float Duration => perLayerSeconds;
    /// <summary>基础恢复比例，供层管理器读取。</summary>
    public float BaseRestorePercent => baseRestorePercent;
    /// <summary>每级防御魔法恢复比例，供层管理器读取。</summary>
    public float LevelRestorePercent => levelRestorePercent;

    /// <summary>供运行时创建/配置实例时设置每层持续秒。</summary>
    public void SetDuration(float seconds)
    {
        perLayerSeconds = Mathf.Max(0.1f, seconds);
    }

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层妄业之力；不设层数上限，对已持有者依旧生效，每层独立计时。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        FalseLifeState state = prop.GetComponent<FalseLifeState>();
        if (state == null)
            state = prop.gameObject.AddComponent<FalseLifeState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层妄业之力。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        FalseLifeState state = prop.GetComponent<FalseLifeState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion
}

/// <summary>
/// 目标身上的妄业之力层管理器：无限叠加、每层独立到期；
/// 实现 IDeathReviver，在目标生命归零时结算诅咒（假死恢复 + 持续扣血），
/// 同时驱动目标图像变黑 20% 的视觉表现。
/// </summary>
internal class FalseLifeState : MonoBehaviour, IDeathReviver
{
    /// <summary>单层妄业之力快照。</summary>
    private class Layer
    {
        public FalseLifeBuff source;    // 施加该层的实例，用于取消时匹配与读取配置。
        public float expireTime;        // 该层到期时间。
    }

    private const float DarkenFactor = 0.8f;            // 变黑 20%（颜色 RGB 乘 0.8，保持透明度）。
    private const string FalseLifeSoundKey = "False life"; // 触发结算时播放的音频资源键。
    private const float SoundVolume = 1f;
    private const int SoundPriority = 32;

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private CharacterHealth health;
    private SpriteRenderer[] renderers;
    private Color[] originalColors;
    private bool draining;                              // 是否正在扣血结算（假死期间）。
    private Coroutine drainRoutine;
    private AudioSource soundAudio;                     // 触发音效音频源。
    private bool warnedMissingSound;                    // 是否已输出过音效缺失警告（一次性）。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
        ResolveSoundAudio();
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;
    }

    /// <summary>
    /// 解析触发音效音频源：优先复用对象上的 AudioSource，没有则新建一个。
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
    /// 无限叠加一层妄业之力；对已持有者依旧生效，不设层数上限。
    /// </summary>
    public bool AddLayer(FalseLifeBuff source)
    {
        if (source == null || prop == null)
            return false;

        layers.Add(new Layer
        {
            source = source,
            expireTime = Time.time + source.Duration,
        });

        ApplyDarken();
        return true;
    }

    /// <summary>
    /// 移除由指定 Buff 实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(FalseLifeBuff source)
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

        ApplyDarken();
    }

    /// <summary>
    /// 目标生命归零时接管死亡：结算妄业之力诅咒（恢复 + 持续扣血）。
    /// </summary>
    /// <param name="unit">即将死亡的单位。</param>
    /// <param name="lethalDamage">导致生命归零的伤害数据。</param>
    /// <returns>有活跃层且未在结算中时返回 <see langword="true"/>（跳过常规死亡）。</returns>
    public bool TryRevive(GameObject unit, Damage lethalDamage)
    {
        if (prop == null || draining || layers.Count == 0)
            return false;

        FalseLifeBuff source = layers[0].source;
        if (source == null)
            return false;

        // 恢复比例 = 基础 80% + 防御魔法等级 × 每级 8%（等级取全局玩家防御魔法等级）。
        int level = UserGlobalInfo.Instance != null
            ? UserGlobalInfo.Instance.DefenseMagicLevel
            : 0;
        float restorePercent = Mathf.Clamp01(
            source.BaseRestorePercent + level * source.LevelRestorePercent);
        int restoreHp = Mathf.Max(0, Mathf.RoundToInt(prop.maxHp * restorePercent));
        // 扣血总时长 = 结算时层数 × 每层秒数（层越多假死越久）。
        float totalSeconds = layers.Count * source.Duration;

        // 一次性结算：消耗当前全部层。
        ClearLayersForTrigger();

        PlayFalseLifeSound();

        // 恢复生命并刷新血条，随后进入持续扣血。
        prop.currentHp = Mathf.Min(prop.maxHp, restoreHp);
        if (health != null)
            health.SetHpbar();

        draining = true;
        if (drainRoutine != null)
            StopCoroutine(drainRoutine);
        drainRoutine = StartCoroutine(DrainRoutine(restoreHp, totalSeconds));
        return true;
    }

    /// <summary>
    /// 在 totalSeconds 秒内把生命值从 startHp 线性扣到 0（无视期间治疗，强制扣光），
    /// 结束后进入常规死亡流程；期间被真正击杀则提前停止结算。
    /// </summary>
    private IEnumerator DrainRoutine(int startHp, float totalSeconds)
    {
        float elapsed = 0f;
        while (elapsed < totalSeconds)
        {
            elapsed += Time.deltaTime;
            if (prop == null || prop.isDead)
                break; // 结算期间被真正击杀：停止扣血，交由常规死亡流程。

            float t = Mathf.Clamp01(elapsed / totalSeconds);
            prop.currentHp = Mathf.Max(0, Mathf.RoundToInt(startHp * (1f - t)));
            if (health != null)
                health.SetHpbar();

            yield return null;
        }

        drainRoutine = null;
        draining = false;
        ApplyDarken(); // 层已消耗且结算结束，恢复原色。

        if (prop == null || prop.isDead)
            yield break;

        // 扣血结束：生命归零，进入常规死亡流程（抛飞 + 重生）。
        prop.currentHp = 0;
        if (health != null && health.isActiveAndEnabled)
            health.Die();
    }

    /// <summary>
    /// 触发结算时消耗当前全部层，并从目标的 Buff 列表移除引用。
    /// </summary>
    private void ClearLayersForTrigger()
    {
        if (prop != null)
        {
            for (int i = 0; i < layers.Count; i++)
            {
                if (layers[i].source != null)
                    prop.currentBuff.Remove(layers[i].source);
            }
        }

        layers.Clear();
    }

    /// <summary>
    /// 播放触发结算音效“False life”；资源键或片段缺失时输出一次性警告，避免静默失败。
    /// </summary>
    private void PlayFalseLifeSound()
    {
        if (soundAudio == null || prop == null ||
            AudioManager.Instance == null || ResourceManager.Instance == null)
            return;

        AudioClip clip = ResourceManager.Instance.GetAudio(FalseLifeSoundKey);
        if (clip == null)
        {
            if (!warnedMissingSound)
            {
                warnedMissingSound = true;
                Debug.LogWarning($"[FalseLifeBuff] 音频资源 {FalseLifeSoundKey} 未加载，触发音效无法播放。", this);
            }
            return;
        }

        soundAudio.clip = clip;
        soundAudio.volume = SoundVolume;
        soundAudio.priority = SoundPriority;
        Camera cam = Camera.main;
        AudioManager.Instance.PlayEffect(
            soundAudio,
            (uint)SoundPriority,
            cam != null
                ? Vector3.Distance(prop.transform.position, cam.transform.position)
                : 0f,
            prop.transform);
    }

    private void RemoveAt(int index)
    {
        Layer layer = layers[index];
        layers.RemoveAt(index);

        if (prop != null && layer.source != null)
            prop.currentBuff.Remove(layer.source);

        if (layers.Count == 0 && !draining)
            ApplyDarken();

        if (layers.Count == 0)
            Destroy(this);
    }

    private void OnDisable()
    {
        if (drainRoutine != null)
        {
            StopCoroutine(drainRoutine);
            drainRoutine = null;
        }

        draining = false;
        RestoreColors();
        layers.Clear();
    }

    /// <summary>
    /// 有活跃层或正在扣血结算时图像变黑 20%，否则恢复原色。
    /// </summary>
    private void ApplyDarken()
    {
        if (renderers == null)
            return;

        bool dark = layers.Count > 0 || draining;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
                continue;

            Color color = originalColors[i];
            if (dark)
            {
                color.r *= DarkenFactor;
                color.g *= DarkenFactor;
                color.b *= DarkenFactor;
            }

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
}
