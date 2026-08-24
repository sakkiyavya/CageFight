using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 浓缩增益：使目标随从（被施加的单位）体型缩小、攻击力提升。
/// 基础：体型 -7%、攻击力 +7%；受局外“防御魔法等级”
/// （UserGlobalInfo.DefenseMagicLevel，即守护魔法等级）影响——每级额外 +0.7%。
/// 可无限叠加：每层独立计时、独立快照加成，总效果为各层比例相加（如 2 层 = 7% + 7% = 14%）。
/// 获得音效（默认 Zip buff）仅在首次施加时触发，叠加不触发。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
///
/// 视觉-only 缩小（2026-08-22 修订）：根 Transform 缩放仅用于显示。
/// 碰撞体与发射点按 1/缩放 反向补偿，世界受击框与攻击发射位置保持不变，
/// 消除“缩小后互相打不中”（发射点内缩导致近战挥空、受击框变小导致子弹隧穿）。
/// </summary>
public class ConcentratedBuff : BuffBase
{
    [Header("浓缩数值")]
    [SerializeField, Min(0f)]
    private float basePercent = 0.07f;      // 基础体型缩小/攻击力提升比例（7%）。
    [SerializeField, Min(0f)]
    private float levelPercent = 0.007f;    // 每级局外防御魔法等级额外加成比例（0.7%）。
    [SerializeField, Min(0.1f)]
    private float duration = 10f;           // 每层持续时间秒。
    [SerializeField, Range(0.05f, 1f)]
    private float minScaleFactor = 0.2f;    // 体型缩小下限（不低于基础体型的 20%）。

    [Header("获得音效（仅首次施加触发，叠加不触发）")]
    [SerializeField, ResourceKey(typeof(AudioClip))]
    private string buffSoundKey = "Zip buff"; // 首次获得浓缩时播放的音频资源键。
    [SerializeField, Range(0f, 1f)]
    private float buffSoundVolume = 1f;       // 获得音效音量。
    [SerializeField, Range(0, 256)]
    private int buffSoundPriority = 32;       // 获得音效优先级（越小越高）。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => false;

    /// <summary>每层持续时间，供层管理器读取。</summary>
    public float Duration => duration;
    /// <summary>体型缩小下限，供层管理器读取。</summary>
    public float MinScaleFactor => minScaleFactor;
    /// <summary>获得音效资源键，供层管理器读取。</summary>
    public string BuffSoundKey => buffSoundKey;
    /// <summary>获得音效音量，供层管理器读取。</summary>
    public float BuffSoundVolume => buffSoundVolume;
    /// <summary>获得音效优先级，供层管理器读取。</summary>
    public int BuffSoundPriority => buffSoundPriority;

    /// <summary>供运行时创建/配置实例时设置每层持续时间。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层浓缩；不设层数上限，每层独立计时与快照加成比例。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        ConcentratedState state = prop.GetComponent<ConcentratedState>();
        if (state == null)
            state = prop.gameObject.AddComponent<ConcentratedState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层浓缩。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        ConcentratedState state = prop.GetComponent<ConcentratedState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 计算单层加成比例：基础 7% + 局外防御魔法等级（守护魔法等级）× 0.7%。
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
/// 目标身上的浓缩层管理器：无限叠加、每层独立到期，总加成按各层比例相加；
/// 体型按 (1 - 总比例) 缩小并保留下限（仅视觉），攻击力按 (1 + 总比例) 提升；
/// 获得音效仅在首层施加时播放。
/// </summary>
internal class ConcentratedState : MonoBehaviour
{
    /// <summary>单层浓缩快照，施加瞬间锁定加成比例。</summary>
    private class Layer
    {
        public ConcentratedBuff source;    // 施加该层的实例，用于取消时匹配。
        public float percent;              // 本层快照的加成比例。
        public float expireTime;           // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private int baseAtk;                       // 首层施加时快照的基础攻击力。
    private Vector3 baseScale;                 // 首层施加时快照的基础体型（含朝向翻转）。
    private float minScaleFactor = 0.2f;       // 体型缩小下限。
    private float currentScaleFactor = 1f;     // 当前生效的体型缩放（每帧重应用用）。

    // 视觉-only 补偿：碰撞体与发射点的世界占位保持原始大小。
    private Collider2D[] bodyColliders;        // 根物体上的碰撞体（受击框）。
    private Vector2[] colliderBaseSizes;       // Box: size；Circle: (半径, 0)；不支持类型为 0（跳过）。
    private Vector2[] colliderBaseOffsets;     // 各碰撞体基准偏移。
    private bool[] colliderIsCircle;           // 是否为圆形碰撞体。
    private Transform shootPoint;              // 发射点子物体。
    private Vector3 shootPointBaseLocal;       // 发射点基准本地坐标。

    private AudioSource soundAudio;            // 获得音效音频源（首层施加时解析）。
    private string soundKey = "Zip buff";
    private float soundVolume = 1f;
    private int soundPriority = 32;
    private bool warnedMissingSound;           // 是否已输出过获得音效缺失警告（一次性）。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
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
    /// 无限叠加一层浓缩；首个层加入时快照基础攻击力/体型/碰撞体/发射点与音效参数。
    /// 获得音效仅在首层施加时播放，后续叠加不触发。
    /// </summary>
    public bool AddLayer(ConcentratedBuff source)
    {
        if (source == null || prop == null)
            return false;

        bool isFirstLayer = layers.Count == 0;
        if (isFirstLayer)
        {
            baseAtk = prop.atk;
            baseScale = prop.transform.localScale;
            minScaleFactor = source.MinScaleFactor;
            soundKey = source.BuffSoundKey;
            soundVolume = source.BuffSoundVolume;
            soundPriority = source.BuffSoundPriority;
            ResolveSoundAudio();
            CaptureBodyFootprint();
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
    /// 快照根物体碰撞体与发射点的基准占位（视觉-only 补偿换算用）。
    /// </summary>
    private void CaptureBodyFootprint()
    {
        bodyColliders = GetComponents<Collider2D>();
        colliderIsCircle = new bool[bodyColliders.Length];
        colliderBaseSizes = new Vector2[bodyColliders.Length];
        colliderBaseOffsets = new Vector2[bodyColliders.Length];

        for (int i = 0; i < bodyColliders.Length; i++)
        {
            Collider2D col = bodyColliders[i];
            if (col == null)
                continue;

            colliderBaseOffsets[i] = col.offset;
            if (col is CircleCollider2D circle)
            {
                colliderIsCircle[i] = true;
                colliderBaseSizes[i] = new Vector2(circle.radius, 0f);
            }
            else if (col is BoxCollider2D box)
            {
                colliderIsCircle[i] = false;
                colliderBaseSizes[i] = box.size;
            }
            else
            {
                // 不支持的类型（Polygon/Capsule 等）跳过补偿。
                colliderIsCircle[i] = false;
                colliderBaseSizes[i] = Vector2.zero;
            }
        }

        shootPoint = transform.Find("ShootPoint");
        if (shootPoint != null)
            shootPointBaseLocal = shootPoint.localPosition;
    }

    /// <summary>
    /// 移除由指定实例施加的一层浓缩，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(ConcentratedBuff source)
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

    /// <summary>
    /// 每帧重应用视觉缩放（覆盖 Move 等行为对 localScale 的朝向翻转写入），
    /// 保证缩小显示与补偿占位始终一致。
    /// </summary>
    private void LateUpdate()
    {
        if (prop == null || layers.Count == 0)
            return;

        ApplyVisualScale(currentScaleFactor);
    }

    private void RemoveAt(int index)
    {
        layers.RemoveAt(index);
        ApplyEffect();

        if (layers.Count == 0)
        {
            prop.atk = baseAtk;
            prop.transform.localScale = baseScale;
            RestoreBodyFootprint();
            Destroy(this);
        }
    }

    /// <summary>
    /// 按当前全部层的比例求和：体型按 (1 - 总比例) 视觉缩小（不低于下限），
    /// 攻击力按 (1 + 总比例) 提升；碰撞体与发射点反向补偿保持世界占位不变。
    /// </summary>
    private void ApplyEffect()
    {
        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].percent;

        float scaleFactor = Mathf.Max(minScaleFactor, 1f - total);
        currentScaleFactor = scaleFactor;

        ApplyVisualScale(scaleFactor);
        CompensateBodyFootprint(scaleFactor);

        prop.atk = Mathf.Max(0, Mathf.RoundToInt(baseAtk * (1f + total)));
    }

    /// <summary>应用视觉缩放（保留朝向翻转）。</summary>
    private void ApplyVisualScale(float scaleFactor)
    {
        float flipX = prop.isFacingLeft ? -1f : 1f;
        prop.transform.localScale = new Vector3(
            Mathf.Abs(baseScale.x) * flipX * scaleFactor,
            Mathf.Abs(baseScale.y) * scaleFactor,
            Mathf.Abs(baseScale.z) * scaleFactor);
    }

    /// <summary>
    /// 视觉-only 补偿：碰撞体尺寸/偏移与发射点本地坐标乘以 1/缩放，
    /// 使它们在世界空间中的占位与未缩小时完全一致。
    /// </summary>
    private void CompensateBodyFootprint(float scaleFactor)
    {
        if (scaleFactor <= 0.001f)
            return;

        float inv = 1f / scaleFactor;

        if (bodyColliders != null)
        {
            for (int i = 0; i < bodyColliders.Length; i++)
            {
                Collider2D col = bodyColliders[i];
                if (col == null)
                    continue;

                col.offset = colliderBaseOffsets[i] * inv;

                if (colliderIsCircle[i] && col is CircleCollider2D circle)
                {
                    circle.radius = colliderBaseSizes[i].x * inv;
                }
                else if (colliderBaseSizes[i].x > 0.001f && col is BoxCollider2D box)
                {
                    box.size = colliderBaseSizes[i] * inv;
                }
            }
        }

        if (shootPoint != null)
            shootPoint.localPosition = shootPointBaseLocal * inv;
    }

    /// <summary>还原碰撞体与发射点的基准占位。</summary>
    private void RestoreBodyFootprint()
    {
        if (bodyColliders != null)
        {
            for (int i = 0; i < bodyColliders.Length; i++)
            {
                Collider2D col = bodyColliders[i];
                if (col == null)
                    continue;

                col.offset = colliderBaseOffsets[i];

                if (colliderIsCircle[i] && col is CircleCollider2D circle)
                {
                    circle.radius = colliderBaseSizes[i].x;
                }
                else if (colliderBaseSizes[i].x > 0.001f && col is BoxCollider2D box)
                {
                    box.size = colliderBaseSizes[i];
                }
            }
        }

        if (shootPoint != null)
            shootPoint.localPosition = shootPointBaseLocal;
    }

    /// <summary>
    /// 播放首次获得浓缩音效；资源键或片段缺失时输出一次性警告，避免静默失败。
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
                Debug.LogWarning($"[ConcentratedBuff] 音频资源 {soundKey} 未加载，获得音效无法播放。", this);
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
    /// 还原基础攻击力、体型、碰撞体与发射点，并清空层列表。
    /// </summary>
    private void RestoreEverything()
    {
        if (prop != null && layers.Count > 0)
        {
            prop.atk = baseAtk;
            prop.transform.localScale = baseScale;
            RestoreBodyFootprint();
        }

        layers.Clear();
        currentScaleFactor = 1f;
    }
}
