using UnityEngine;

/// <summary>
/// Magic energy sing Magic 释放型法术执行器（原地生成，直伤类）：
/// - 原地生成（DirectSpawn）后立即播放释放动画（"Magic energy sing" 帧动画）；
/// - 伤害由动画帧事件触发（DealDamage，动画中段）——对范围内敌方结算大量直伤与击退；
/// - 动画帧事件 Finish（动画末尾）触发回收，动画播放完成后才结束；
/// - 兜底超时（动画事件丢失/动画卡死时安全回收）。
/// 等级跟随预留：伤害 = baseDamage + GetLevelBonus()（接缝当前返回 0）。
/// 实现 IEngineerDirectSpellInstance，由 EngineerSpellCaster 以对象池方式生成。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class MagicEnergySing : MonoBehaviour, IEngineerDirectSpellInstance
{
    [Header("直伤")]
    [SerializeField, Min(0)]
    private int baseDamage = 150;           // 基础大量伤害（等级跟随的基准）。
    [SerializeField]
    private float repel = 10f;              // 释放击退强度。
    [SerializeField]
    private DamageType damageType = DamageType.magic;

    [Header("伤害范围")]
    [SerializeField, Min(0.1f)]
    private float radius = 3f;              // 伤害半径。
    [SerializeField]
    private LayerMask targetLayers = ~0;    // 参与判定的层。

    [Header("兜底")]
    [SerializeField, Min(0.5f)]
    private float fallbackTimeout = 5f;     // 动画事件缺失时的兜底回收秒。

    [Header("音效")]
    [SerializeField]
    private AudioSource audioSource;        // 伤害帧音效源（未配置时自动获取/创建）。
    [SerializeField, ResourceKey(typeof(AudioClip))]
    private string damageAudioKey = "BOOM.LV.3"; // 伤害帧音效资源键。

    [Header("警示圈")]
    [SerializeField]
    private SpriteRenderer warningCircle;   // 释放警示圈渲染器（子物体，显示伤害范围）。

    private Animator animator;
    private SpriteRenderer body;

    private int casterSide;
    private GameObject casterObject;
    private float startedAt;
    private bool damageDealt;
    private bool finished;

    private static readonly Collider2D[] hitBuffer = new Collider2D[32];

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<SpriteRenderer>();
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }
        }
    }

    private void OnEnable()
    {
        finished = false;
        damageDealt = false;
        startedAt = Time.time;
        if (animator != null)
            animator.enabled = false;
        if (body != null)
            body.enabled = true;
        if (warningCircle != null)
            warningCircle.enabled = false;
    }

    /// <summary>
    /// 由 EngineerSpellCaster 调用：落点原地生成，并立即开始释放动画。
    /// </summary>
    public void Initialize(EngineerController caster, SpellDefinition definition, Vector3 target)
    {
        if (!caster || !IsPooled())
        {
            DisableInvalidInstance();
            return;
        }

        transform.position = target;
        casterSide = caster.Side;
        casterObject = caster.gameObject;
        startedAt = Time.time;
        damageDealt = false;
        finished = false;

        // 显示警示圈并按其视觉范围缩放：警示圈半径与伤害半径一致。
        if (warningCircle != null && warningCircle.sprite != null)
        {
            float spriteRadius = warningCircle.sprite.bounds.extents.x;
            float scale = spriteRadius > 0.001f ? radius / spriteRadius : 1f;
            warningCircle.transform.localScale = new Vector3(scale, scale, 1f);
            warningCircle.enabled = true;
        }

        // 立即播放释放动画（伤害与结束由动画帧事件驱动）。
        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Play("Magic energy sing", 0, 0f);
        }
        else
        {
            // 无动画：直接结算并结束。
            DealDamage();
            Finish();
        }
    }

    private void Update()
    {
        if (finished)
            return;

        // 兜底：动画事件丢失/动画卡死时安全回收。
        if (Time.time - startedAt >= fallbackTimeout)
            Release();
    }

    /// <summary>
    /// 动画帧事件（中段）调用：对范围内敌方结算大量直伤与击退。
    /// </summary>
    public void DealDamage()
    {
        if (damageDealt)
            return;

        damageDealt = true;

        // 伤害帧：隐藏警示圈。
        if (warningCircle != null)
            warningCircle.enabled = false;

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, hitBuffer, targetLayers);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null)
                continue;

            GameObjectProperty prop = hit.GetComponent<GameObjectProperty>();
            if (prop == null || prop.isDead || prop.isUntargetable || prop.side == casterSide)
                continue;

            ICollide collide = hit.GetComponent<ICollide>();
            if (collide == null)
                continue;

            Damage d = Damage.DefaultDamage;
            d.side = casterSide;
            d.source = casterObject;
            d.target = hit.gameObject;
            d.initialDamage = Mathf.Max(1, baseDamage + GetLevelBonus());
            d.finalDamage = 0;
            d.type = damageType;
            d.repel = repel;
            d.collideDir = transform.position.x < hit.transform.position.x ? 1 : -1;
            collide.OnCollide(d);
        }

        PlayDamageAudio();
    }

    /// <summary>
    /// 伤害帧音效（BOOM.LV.3）：按资源键经 ResourceManager 取得，在单位位置经 AudioManager 播放。
    /// </summary>
    private void PlayDamageAudio()
    {
        if (audioSource == null || AudioManager.Instance == null ||
            ResourceManager.Instance == null || string.IsNullOrEmpty(damageAudioKey))
            return;

        AudioClip clip = ResourceManager.Instance.GetAudio(damageAudioKey);
        if (clip == null)
            return;

        audioSource.clip = clip;
        audioSource.volume = 1f;
        audioSource.priority = 32;
        AudioManager.Instance.PlayEffectAt(audioSource, 32, transform);
    }

    /// <summary>
    /// 动画帧事件（末尾）调用：动画播放完成后回收。
    /// </summary>
    public void Finish()
    {
        Release();
    }

    /// <summary>
    /// 等级跟随接缝：未来在此读取“总等级”并换算加成伤害；当前返回 0。
    /// </summary>
    private int GetLevelBonus()
    {
        return 0;
    }

    private bool IsPooled()
    {
        GameObjectPool pool = GameObjectPool.Instance;
        return pool != null && pool.GetPrefab(gameObject) != null;
    }

    private void Release()
    {
        if (finished)
            return;

        finished = true;
        GameObjectPool pool = GameObjectPool.Instance;
        if (pool != null)
            pool.Release(gameObject);
        else
            DisableInvalidInstance();
    }

    private void DisableInvalidInstance()
    {
        finished = true;
        Debug.LogError("[MagicEnergySing] 必须由 GameObjectPool 生成。", this);
        gameObject.SetActive(false);
    }
}
