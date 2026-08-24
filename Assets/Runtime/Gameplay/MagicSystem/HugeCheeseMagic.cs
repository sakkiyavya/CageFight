using UnityEngine;

/// <summary>
/// Huge cheese Magic 释放型法术执行器（原地生成，增益类）：
/// - 与冰冻法术同款拖动瞄准释放（DirectSpawn）：落点原地生成，
///   立即播放 "Huge cheese" 释放动画（奶酪弹出帧动画）；
/// - 动画开始时对范围内友方单位施加一层“巨化”（GiantBuff，
///   经 CharacterHealth.ApplyBuff 统一入口施加并登记）；
/// - 动画播放完成后回收；兜底超时（动画事件缺失/卡死时安全回收）。
/// 实现 IEngineerDirectSpellInstance，由 EngineerSpellCaster 以对象池方式生成。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class HugeCheeseMagic : MonoBehaviour, IEngineerDirectSpellInstance
{
    [Header("巨化")]
    [SerializeField] private GiantBuff giantBuff;       // 施加的巨化实例（预制体上预配置）。
    [SerializeField, Min(0.1f)] private float radius = 3f;      // 巨化施加半径。
    [SerializeField] private LayerMask targetLayers = ~0;       // 参与判定的层。

    [Header("兜底")]
    [SerializeField, Min(0.5f)] private float fallbackTimeout = 2f; // 动画结束兜底回收秒。

    private Animator animator;
    private int casterSide;
    private float startedAt;
    private bool applied;
    private bool finished;

    private static readonly Collider2D[] hitBuffer = new Collider2D[32];

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (giantBuff == null)
            giantBuff = GetComponent<GiantBuff>();
    }

    private void OnEnable()
    {
        // 池化复用入口：重置本轮状态并停用动画（等待 Initialize 重新播放）。
        finished = false;
        applied = false;
        startedAt = Time.time;
        if (animator != null)
            animator.enabled = false;
    }

    /// <summary>
    /// 由 EngineerSpellCaster 调用：落点原地生成，立即播放奶酪释放动画，
    /// 并对范围内友方单位施加一层巨化。
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
        startedAt = Time.time;
        applied = false;
        finished = false;

        // 立即播放释放动画（奶酪弹出帧动画）。
        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Play("Huge cheese", 0, 0f);
        }

        ApplyGiant();
    }

    private void Update()
    {
        if (finished)
            return;

        // 兜底：动画事件缺失/动画卡死时安全回收。
        if (Time.time - startedAt >= fallbackTimeout)
            Release();
    }

    /// <summary>
    /// 对范围内友方单位施加一层巨化（经生命框架统一入口 ApplyBuff + 登记 currentBuff）。
    /// </summary>
    private void ApplyGiant()
    {
        if (applied || giantBuff == null)
            return;

        applied = true;

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, hitBuffer, targetLayers);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null)
                continue;

            GameObjectProperty prop = hit.GetComponentInParent<GameObjectProperty>();
            if (prop == null || prop.isDead || prop.side != casterSide)
                continue;

            CharacterHealth targetHealth = prop.GetComponent<CharacterHealth>();
            if (targetHealth == null)
                continue;

            // 经生命框架统一入口施加一层巨化。
            targetHealth.ApplyBuff(giantBuff);
        }
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
        Debug.LogError("[HugeCheeseMagic] 必须由 GameObjectPool 生成。", this);
        gameObject.SetActive(false);
    }
}
