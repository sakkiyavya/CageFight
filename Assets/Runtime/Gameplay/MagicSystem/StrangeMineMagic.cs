using UnityEngine;

/// <summary>
/// Strange mine Magic 释放型法术执行器（原地生成，控制类）：
/// - 与冰冻法术同款拖动瞄准释放（DirectSpawn，带施法前动画与警示圈）；
/// - 三阶段动画：入场播放 "Strange mine"，释放阶段循环 "Strange mine2"，
///   退场播放 "Strange mine3" 后回收；
/// - 释放阶段每 tickInterval（默认 0.5 秒）对范围内敌方施加一层“创伤”
///   （TraumaDebuff，经 CharacterHealth.ApplyBuff 统一入口施加并登记）；
/// - 入场结束隐藏警示圈；兜底超时防动画卡死。
/// 实现 IEngineerDirectSpellInstance，由 EngineerSpellCaster 以对象池方式生成。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class StrangeMineMagic : MonoBehaviour, IEngineerDirectSpellInstance
{
    [SerializeField] private TraumaDebuff traumaBuff;       // 施加的创伤实例（预制体上预配置）。
    [SerializeField] private SpriteRenderer warningCircle;  // 警示圈渲染器（子物体，入场阶段显示）。
    [SerializeField, Min(0.1f)] private float radius = 4.5f; // 创伤范围。
    [SerializeField, Min(0.1f)] private float duration = 5f; // 释放阶段持续秒（入场/退场不计入）。
    [SerializeField, Min(0.05f)] private float tickInterval = 0.5f; // 每 0.5 秒施加一层创伤。
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField, Min(0.5f)] private float fallbackTimeout = 10f; // 动画卡死兜底回收秒。

    private static readonly Collider2D[] hitBuffer = new Collider2D[32];

    private enum Phase { Idle, Enter, Active, Exit }

    private Animator animator;
    private int side;
    private Phase phase;
    private float phaseEndsAt;
    private float nextTick;
    private float startedAt;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (traumaBuff == null)
            traumaBuff = GetComponent<TraumaDebuff>();
        if (warningCircle == null)
        {
            Transform circle = transform.Find("WarningCircle");
            if (circle != null)
                warningCircle = circle.GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        // 池化复用入口：重置阶段与表现。
        phase = Phase.Idle;
        if (animator != null)
            animator.enabled = false;
        if (warningCircle != null)
            warningCircle.enabled = false;
    }

    /// <summary>
    /// 由 EngineerSpellCaster 调用：落点原地生成，显示警示圈并进入入场动画。
    /// </summary>
    public void Initialize(EngineerController caster, SpellDefinition definition, Vector3 target)
    {
        if (!caster || !traumaBuff || !IsPooled())
        {
            gameObject.SetActive(false);
            return;
        }

        transform.position = target;
        side = caster.Side;
        startedAt = Time.time;

        // 警示圈：与冰冻法术一致（按定义取贴图并按范围缩放）。
        if (warningCircle != null)
        {
            Sprite sprite = definition.ShowWarningCircle && ResourceManager.Instance
                ? ResourceManager.Instance.GetSprite(definition.WarningCircleKey) : null;
            if (sprite != null)
                warningCircle.sprite = sprite;

            float spriteRadius = warningCircle.sprite != null
                ? warningCircle.sprite.bounds.extents.x : 0.5f;
            float scale = spriteRadius > 0.001f ? radius / spriteRadius : 1f;
            warningCircle.transform.localScale = new Vector3(scale, scale, 1f);
            warningCircle.enabled = definition.ShowWarningCircle && warningCircle.sprite != null;
        }

        EnterPhase();
    }

    /// <summary>入场阶段：播放 "Strange mine"（时长按片段实际长度）。</summary>
    private void EnterPhase()
    {
        phase = Phase.Enter;
        float clipLength = 0.68f;
        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Play("Strange mine", 0, 0f);
            clipLength = Mathf.Max(0.05f, animator.GetCurrentAnimatorStateInfo(0).length);
        }
        phaseEndsAt = Time.time + clipLength;
    }

    /// <summary>释放阶段：控制器入场过渡已进入 "Strange mine2"（循环播放），开始按间隔施加创伤。</summary>
    private void StartActivePhase()
    {
        phase = Phase.Active;
        // 不再重新 Play：mine → mine2 的控制器过渡自动完成，mine2 状态循环播放。
        phaseEndsAt = Time.time + duration;
        nextTick = Time.time + tickInterval;
    }

    /// <summary>退场阶段：生效结束，隐藏警示圈并播放 "Strange mine3"（时长按片段实际长度）。</summary>
    private void StartExitPhase()
    {
        phase = Phase.Exit;

        // 生效结束：隐藏警示圈。
        if (warningCircle != null)
            warningCircle.enabled = false;

        float clipLength = 1.02f;
        if (animator != null)
        {
            animator.Rebind();
            animator.Play("Strange mine3", 0, 0f);
            clipLength = Mathf.Max(0.05f, animator.GetCurrentAnimatorStateInfo(0).length);
        }
        phaseEndsAt = Time.time + clipLength;
    }

    private void Update()
    {
        if (phase == Phase.Idle)
            return;

        // 兜底：动画卡死时安全回收。
        if (Time.time - startedAt >= fallbackTimeout)
        {
            Release();
            return;
        }

        if (Time.time < phaseEndsAt)
        {
            if (phase == Phase.Active && Time.time >= nextTick)
            {
                nextTick += tickInterval;
                ApplyTrauma();
            }
            return;
        }

        switch (phase)
        {
            case Phase.Enter:
                StartActivePhase();
                break;
            case Phase.Active:
                StartExitPhase();
                break;
            case Phase.Exit:
                Release();
                break;
        }
    }

    /// <summary>
    /// 对范围内敌方施加一层创伤（经生命框架统一入口 ApplyBuff + 登记 currentDebuff）。
    /// </summary>
    private void ApplyTrauma()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, radius, hitBuffer, targetLayers);

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];
            if (hit == null)
                continue;

            GameObjectProperty target = hit.GetComponentInParent<GameObjectProperty>();
            if (target == null || target.isDead || target.side == side)
                continue;

            CharacterHealth targetHealth = target.GetComponent<CharacterHealth>();
            if (targetHealth == null)
                continue;

            // 经生命框架统一入口施加一层创伤。
            targetHealth.ApplyBuff(traumaBuff);
        }
    }

    private void Release()
    {
        phase = Phase.Idle;
        GameObjectPool pool = GameObjectPool.Instance;
        if (pool != null)
            pool.Release(gameObject);
        else
            gameObject.SetActive(false);
    }

    private bool IsPooled()
    {
        GameObjectPool pool = GameObjectPool.Instance;
        return pool != null && pool.GetPrefab(gameObject) != null;
    }
}
