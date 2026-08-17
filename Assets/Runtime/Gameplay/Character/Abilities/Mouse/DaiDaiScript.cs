using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class DaiDaiScript : MonoBehaviour
{
    [Header("免死后图片")]
    [SerializeField] private Sprite replacementSprite;
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
    private Vector3 originalScale;

    private bool triggered;

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
        prop.OnHitted += OnHitted;
    }

    private void OnDisable()
    {
        prop.OnHitted -= OnHitted;
        StopAllCoroutines();

        if (animator != null)
            animator.enabled = true;

        if (characterAI != null)
            characterAI.enabled = true;

        RestoreAppearance();
    }

    private void OnHitted(Damage damage)
    {
        if (triggered || prop.isDead)
            return;

        int incomingDamage =
            Mathf.Max(
                0,
                DamageComputor
                    .DamageCompute(damage)
                    .finalDamage
            );

        if (prop.currentHp - incomingDamage > 0)
            return;

        triggered = true;

        int restoredHp =
            Mathf.Max(
                1,
                Mathf.RoundToInt(
                    prop.maxHp * restoredHpPercent
                )
            );

        /*
         * OnHitted在正式扣血前触发，
         * 所以提前补上即将受到的伤害。
         */
        prop.currentHp =
            restoredHp + incomingDamage;

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
    }

    private void ApplyReplacementAppearance()
    {
        if (targetRenderer == null ||
            replacementSprite == null)
        {
            return;
        }

        targetRenderer.sprite = replacementSprite;

        /*
         * 根据原图片和替换图片的实际尺寸，
         * 自动计算接近原角色的显示大小。
         */
        if (originalSprite != null)
        {
            Vector2 originalBounds =
                originalSprite.bounds.size;

            Vector2 replacementBounds =
                replacementSprite.bounds.size;

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
