using UnityEngine;

/// <summary>
/// Dancing statue Magic 释放型法术执行器（原地生成，控制类）：
/// - 与冰冻法术同款拖动瞄准释放（DirectSpawn，带施法前动画）：
///   落点原地生成跳舞雕像，循环播放 "Dancing statue" 舞蹈动画；
/// - 每 tickInterval（默认 1 秒）对范围内敌方施加一层“麻痹”（ParalysisDebuff，
///   经 CharacterHealth.ApplyBuff 统一入口施加并登记）；
/// - 范围比冰冻法术更广（默认半径 3.5）；持续 duration 秒后淡出回收。
/// 实现 IEngineerDirectSpellInstance，由 EngineerSpellCaster 以对象池方式生成。
/// </summary>
[RequireComponent(typeof(Animator))]
public sealed class DancingStatueMagic : MonoBehaviour, IEngineerDirectSpellInstance
{
    [SerializeField] private ParalysisDebuff paralysisBuff;  // 施加的麻痹实例（预制体上预配置）。
    [SerializeField] private SpriteRenderer body;            // 雕像本体渲染器（结束淡出用）。
    [SerializeField] private SpriteRenderer warningCircle;   // 警示圈渲染器（子物体，入场阶段显示）。
    [SerializeField, Min(0.1f)] private float radius = 3.5f; // 麻痹范围（比冰冻法术更广）。
    [SerializeField, Min(0.1f)] private float duration = 5f; // 雕像持续秒。
    [SerializeField, Min(0.05f)] private float tickInterval = 1f; // 每 1 秒施加一层麻痹。
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField, Min(0.05f)] private float fadeOutTime = 0.45f; // 结束淡出秒。

    private static readonly Collider2D[] hitBuffer = new Collider2D[32];

    private Animator animator;
    private Color originalColor;
    private int side;
    private float startedAt;
    private float nextTick;
    private float endingAt;
    private bool active;
    private bool ending;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        if (paralysisBuff == null)
            paralysisBuff = GetComponent<ParalysisDebuff>();
        if (body == null)
            body = GetComponent<SpriteRenderer>();
        if (body != null)
            originalColor = body.color;
        if (warningCircle == null)
        {
            Transform circle = transform.Find("WarningCircle");
            if (circle != null)
                warningCircle = circle.GetComponent<SpriteRenderer>();
        }
    }

    private void OnEnable()
    {
        // 池化复用入口：重置本轮状态与表现。
        active = false;
        ending = false;
        if (body != null)
            body.color = originalColor;
        if (animator != null)
            animator.enabled = false;
        if (warningCircle != null)
            warningCircle.enabled = false;
    }

    /// <summary>
    /// 由 EngineerSpellCaster 调用：落点原地生成雕像，循环播放舞蹈动画，
    /// 显示警示圈，并按间隔对范围内敌方施加麻痹。
    /// </summary>
    public void Initialize(EngineerController caster, SpellDefinition definition, Vector3 target)
    {
        if (!caster || !paralysisBuff || !IsPooled())
        {
            gameObject.SetActive(false);
            return;
        }

        transform.position = target;
        side = caster.Side;
        startedAt = Time.time;
        nextTick = startedAt + tickInterval;

        // 循环播放跳舞动画。
        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Play("Dancing statue", 0, 0f);
        }

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

        active = true;
    }

    private void Update()
    {
        if (!active)
            return;

        float elapsed = Time.time - startedAt;
        if (!ending && elapsed >= duration)
        {
            ending = true;
            endingAt = Time.time;

            // 生效结束：隐藏警示圈（生效期间一直显示）。
            if (warningCircle != null)
                warningCircle.enabled = false;
        }

        if (ending)
        {
            EndVisual();
            return;
        }

        if (Time.time >= nextTick)
        {
            nextTick += tickInterval;
            ApplyParalysis();
        }
    }

    /// <summary>
    /// 对范围内敌方施加一层麻痹（经生命框架统一入口 ApplyBuff + 登记 currentDebuff）。
    /// </summary>
    private void ApplyParalysis()
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

            // 经生命框架统一入口施加一层麻痹。
            targetHealth.ApplyBuff(paralysisBuff);
        }
    }

    /// <summary>结束淡出：透明度线性降低，结束后回收对象池。</summary>
    private void EndVisual()
    {
        float t = Mathf.Clamp01((Time.time - endingAt) / fadeOutTime);
        if (body != null)
        {
            Color color = originalColor;
            color.a *= 1f - t;
            body.color = color;
        }

        if (t >= 1f)
            GameObjectPool.Instance.Release(gameObject);
    }

    private bool IsPooled()
    {
        GameObjectPool pool = GameObjectPool.Instance;
        return pool != null && pool.GetPrefab(gameObject) != null;
    }
}
