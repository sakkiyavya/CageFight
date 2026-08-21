using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class DaiDaiScript : BehaviourBase
{
    [Header("免死后图片")]
    [SerializeField, ResourceKey(typeof(Sprite))]
    private string replacementSpriteKey = "Derivative-Two fool";
    [SerializeField] private SpriteRenderer targetRenderer;

    [Tooltip("(1,1)匹配原图片大小，(0.5,0.5)缩小一半")]
    [SerializeField] private Vector2 replacementSize = Vector2.one;

    [Header("设置")]
    [SerializeField, Min(0.1f)] private float deathDelay = 10f;

    [SerializeField, Range(0f, 1f)]
    private float restoredHpPercent = 0.5f;

    private GameObjectProperty prop;
    private CharacterHealth health;
    private CharacterAI characterAI;
    private Animator animator;
    private Rigidbody2D body;

    private Sprite originalSprite;
    private Sprite _replacementSprite;      // 经 ResourceManager 解析的替换贴图缓存。
    private Vector3 originalScale;

    private bool triggered;

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
        if (this.health == null)
            this.health = health;
    }

    /// <summary>免死由死亡复活器驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
        characterAI = GetComponent<CharacterAI>();
        animator = GetComponent<Animator>();
        body = GetComponent<Rigidbody2D>();

        if (targetRenderer == null)
        {
            targetRenderer =
                GetComponentInChildren<SpriteRenderer>(true);
        }
    }

    private void Start()
    {
        if (targetRenderer == null)
            return;

        originalSprite = targetRenderer.sprite;
        originalScale = targetRenderer.transform.localScale;
    }

    private void OnEnable()
    {
        triggered = false;
        // 经生命框架统一扩展点登记免死复活器（OnDisable 对称注销）；
        // 不再监听 OnHitted 预判伤害，避免重复调用伤害结算。
        if (health != null)
            health.RegisterDeathReviver(TryRevive);
    }

    private void OnDisable()
    {
        if (health != null)
            health.UnregisterDeathReviver(TryRevive);

        StopAllCoroutines();

        if (animator != null)
            animator.enabled = true;

        if (characterAI != null)
            characterAI.enabled = true;

        RestoreAppearance();
    }

    /// <summary>
    /// 免死接管（经 CharacterHealth 死亡复活器登记）：致命伤害后按比例恢复生命，
    /// 停止 AI 与攻击、替换贴图，并进入延迟死亡状态；本次伤害已由框架完成唯一结算，
    /// 不再自行调用伤害计算。
    /// </summary>
    /// <param name="lethalDamage">导致生命归零的伤害数据。</param>
    /// <returns>接管成功时返回 <see langword="true"/>（跳过常规死亡流程）。</returns>
    private bool TryRevive(Damage lethalDamage)
    {
        if (triggered)
            return false;

        triggered = true;

        int restoredHp =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    prop.maxHp * restoredHpPercent
                )
            );

        // 经生命框架受控 API 恢复生命（框架已结算完本次致命伤害）。
        if (health != null)
            health.SetHpKeepDeadState(restoredHp);

        prop.isAttack = false;
        prop.target = null;
        prop.path?.Clear();

        if (characterAI != null)
            characterAI.enabled = false;

        if (animator != null)
            animator.enabled = false;

        if (body != null)
        {
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        ApplyReplacementAppearance();
        StartCoroutine(DelayedDeath());
        return true;
    }

    private void ApplyReplacementAppearance()
    {
        if (targetRenderer == null)
            return;

        // 替换贴图按资源键经 ResourceManager 解析（延迟补齐）。
        if (_replacementSprite == null && ResourceManager.Instance != null &&
            !string.IsNullOrEmpty(replacementSpriteKey))
            _replacementSprite = ResourceManager.Instance.GetSprite(replacementSpriteKey);

        if (_replacementSprite == null)
            return;

        targetRenderer.sprite = _replacementSprite;

        /*
         * 根据原图片和替换图片的实际尺寸，
         * 自动计算接近原角色的显示大小。
         */
        if (originalSprite != null)
        {
            Vector2 originalBounds =
                originalSprite.bounds.size;

            Vector2 replacementBounds =
                _replacementSprite.bounds.size;

            float scaleX =
                replacementBounds.x > 0f
                    ? originalBounds.x /
                      replacementBounds.x
                    : 1f;

            float scaleY =
                replacementBounds.y > 0f
                    ? originalBounds.y /
                      replacementBounds.y
                    : 1f;

            targetRenderer.transform.localScale =
                new Vector3(
                    originalScale.x *
                    scaleX *
                    replacementSize.x,

                    originalScale.y *
                    scaleY *
                    replacementSize.y,

                    originalScale.z
                );
        }
    }

    private IEnumerator DelayedDeath()
    {
        yield return new WaitForSeconds(deathDelay);

        if (prop.isDead)
            yield break;

        if (animator != null)
            animator.enabled = true;

        Damage damage = Damage.DefaultDamage;

        damage.source = gameObject;
        damage.target = gameObject;
        damage.side = prop.side;
        damage.initialDamage = prop.maxHp;
        damage.repel = 0f;

        health.TakeDamage(damage);
    }

    private void RestoreAppearance()
    {
        if (targetRenderer == null)
            return;

        if (originalSprite != null)
            targetRenderer.sprite = originalSprite;

        targetRenderer.transform.localScale =
            originalScale;
    }
}
