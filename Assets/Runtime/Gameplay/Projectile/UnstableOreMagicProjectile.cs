using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unstable ore Magic 抛射法术执行器（直伤类）：
/// - 抛射：从施法点沿弧线（ArcHeight）飞向落点，抛射期间显示弹幕素材（矿石贴图）；
/// - 落地：隐藏弹幕素材，播放爆炸动画（复用 "Unstable ore" 动画，事件链：
///   PlayExplosionAudio → DealExplosionDamage → FinishExplosion），
///   对落点半径内敌方造成直伤与击退（经 DamageComputor 完整结算）。
/// - 等级跟随预留：伤害 = baseDamage + GetLevelBonus()（接缝当前返回 0，
///   未来在此接入“总等级”换算的加成伤害）。
/// 实现 IEngineerAimedSpellInstance，由 EngineerSpellCaster 以对象池方式生成并初始化。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class UnstableOreMagicProjectile : MonoBehaviour, IEngineerAimedSpellInstance
{
    [Header("直伤")]
    [SerializeField, Min(0)]
    private int baseDamage = 30;            // 基础伤害（等级跟随的基准）。
    [SerializeField]
    private float repel = 6f;               // 爆炸击退强度。
    [SerializeField]
    private DamageType damageType = DamageType.magic;

    [Header("抛射")]
    [SerializeField, Min(0f)]
    private float spinSpeed = 720f;         // 抛射期间自旋角速度（度/秒），与 Crowbar Magic 一致。

    [Header("爆炸")]
    [SerializeField, Min(0.1f)]
    private float explosionRadius = 2f;     // 爆炸伤害半径。
    [SerializeField]
    private LayerMask targetLayers = ~0;    // 参与爆炸判定的层。
    [SerializeField]
    private string explosionStateName = "Unstable ore"; // 爆炸动画状态名（与控制器一致）。
    [SerializeField, Min(0.01f)]
    private float fallbackExplosionDuration = 2f;       // 无动画/动画卡死时的兜底回收秒。

    [Header("音效")]
    [SerializeField]
    private AudioSource audioSource;        // 爆炸音效源（未配置时自动获取/创建）。
    [SerializeField]
    private AudioClip explosionAudio;       // 爆炸音效片段。

    private Animator animator;
    private SpriteRenderer body;
    private Sprite flightSprite;            // 抛射飞行贴图（对象池复用后还原，防止爆炸帧残留）。

    private Vector3 start;                  // 抛射起点（施法点）。
    private Vector3 target;                 // 落点。
    private Quaternion launchRotation;      // 发射时的初始旋转（自旋基准）。
    private float elapsed;
    private float flightTime;
    private float arcHeight;
    private bool initialized;
    private bool exploding;
    private bool finished;
    private bool damageDealt;

    private int casterSide;                 // 施法者阵营（爆炸敌我判定）。
    private GameObject casterObject;        // 施法者对象（伤害归属）。

    private static readonly Collider2D[] hitBuffer = new Collider2D[32];   // 复用的爆炸扫描缓冲。
    private readonly HashSet<Component> damagedTargets = new HashSet<Component>(); // 本次爆炸已结算目标。
    private Coroutine fallbackRoutine;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        body = GetComponent<SpriteRenderer>();
        if (body != null)
            flightSprite = body.sprite;
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
        initialized = false;
        exploding = false;
        finished = false;
        damageDealt = false;
        damagedTargets.Clear();
        CancelFallback();
        if (body != null)
        {
            // 对象池复用：还原抛射飞行贴图（上次爆炸动画会残留爆炸帧）并恢复显示。
            body.sprite = flightSprite;
            body.enabled = true;
        }
        if (animator != null)
            animator.enabled = false;
    }

    /// <summary>
    /// 由 EngineerSpellCaster 调用：以施法者阵营与落点初始化抛射数据。
    /// </summary>
    public void Initialize(EngineerController caster, SpellDefinition definition, Vector3 landingPoint)
    {
        if (!caster || !definition || !IsPooled())
        {
            DisableInvalidInstance();
            return;
        }

        start = transform.position;
        target = landingPoint;
        launchRotation = transform.rotation;
        flightTime = Mathf.Max(0.01f, definition.FlightTime);
        arcHeight = definition.ArcHeight;
        elapsed = 0f;
        casterSide = caster.Side;
        casterObject = caster.gameObject;
        initialized = true;
    }

    private void Update()
    {
        if (finished || !initialized || exploding)
            return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightTime);

        // 抛射：直线插值 + 抛物线高度，并沿发射方向自旋（与 Crowbar Magic 一致）。
        transform.position = Vector3.Lerp(start, target, t) +
            Vector3.up * (4f * arcHeight * t * (1f - t));
        transform.rotation = launchRotation * Quaternion.Euler(0f, 0f, spinSpeed * elapsed);

        if (t < 1f)
            return;

        BeginExplosion();
    }

    /// <summary>
    /// 落地：播放爆炸动画（动画帧会把同一 SpriteRenderer 的贴图切换为爆炸帧，
    /// 因此不隐藏主体渲染器），无动画时直接结算并回收。
    /// </summary>
    private void BeginExplosion()
    {
        if (exploding)
            return;

        exploding = true;

        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            if (!string.IsNullOrEmpty(explosionStateName))
                animator.Play(explosionStateName, 0, 0f);
            StartFallback(fallbackExplosionDuration);
        }
        else
        {
            if (body != null)
                body.enabled = false;
            DealExplosionDamage();
            Release();
        }
    }

    /// <summary>
    /// 爆炸动画伤害帧调用（动画事件）：对落点半径内敌方结算直伤与击退，并播放爆炸音效。
    /// </summary>
    public void DealExplosionDamage()
    {
        if (!exploding || damageDealt)
            return;

        damageDealt = true;

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, explosionRadius, hitBuffer, targetLayers);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null)
                continue;

            ICollide collide = hit.GetComponent<ICollide>();
            Component component = collide as Component;
            if (collide == null || component == null)
                continue;

            if (!damagedTargets.Add(component))
                continue;

            GameObjectProperty prop = hit.GetComponent<GameObjectProperty>();
            if (prop == null || prop.isDead || prop.isUntargetable ||
                prop.side == casterSide)
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

        PlayExplosionAudio();
    }

    /// <summary>
    /// 爆炸动画结束帧调用（动画事件）：回收本实例。
    /// </summary>
    public void FinishExplosion()
    {
        Release();
    }

    /// <summary>
    /// 爆炸动画音效帧调用（动画事件）。
    /// </summary>
    public void PlayExplosionAudio()
    {
        if (audioSource == null || explosionAudio == null ||
            AudioManager.Instance == null)
            return;

        audioSource.clip = explosionAudio;
        audioSource.volume = 1f;
        audioSource.priority = 32;
        Camera cam = Camera.main;
        AudioManager.Instance.PlayEffect(
            audioSource,
            32,
            cam != null
                ? Vector3.Distance(transform.position, cam.transform.position)
                : 0f,
            transform);
    }

    #region 内部辅助
    /// <summary>
    /// 等级跟随接缝：未来在此读取“总等级”并换算加成伤害；
    /// 当前等级系统未接入，返回 0（即造成 baseDamage 点伤害）。
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
        initialized = false;
        CancelFallback();
        GameObjectPool pool = GameObjectPool.Instance;
        if (pool != null)
            pool.Release(gameObject);
        else
            DisableInvalidInstance();
    }

    private void StartFallback(float seconds)
    {
        CancelFallback();
        fallbackRoutine = StartCoroutine(FallbackRoutine(seconds));
    }

    private IEnumerator FallbackRoutine(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        fallbackRoutine = null;
        Release();
    }

    private void CancelFallback()
    {
        if (fallbackRoutine != null)
        {
            StopCoroutine(fallbackRoutine);
            fallbackRoutine = null;
        }
    }

    private void DisableInvalidInstance()
    {
        finished = true;
        initialized = false;
        Debug.LogError("[UnstableOreMagicProjectile] 必须由 GameObjectPool 生成。", this);
        gameObject.SetActive(false);
    }
    #endregion
}
