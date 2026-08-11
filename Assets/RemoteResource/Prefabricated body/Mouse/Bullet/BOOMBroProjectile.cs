using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(DamageSource))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class BOOMBroProjectile : MonoBehaviour
{
    [Header("飞行")]
    [SerializeField] private Sprite flightSprite;
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float arriveDistance = 0.1f;

    [Header("爆炸")]
    [SerializeField] private bool canBeIntercepted = true;
    [SerializeField] private float explosionRadius = 2f;
    [SerializeField] private LayerMask targetLayers = ~0;
    [SerializeField] private Animator animator;
    [SerializeField] private string explosionStateName;

    [Header("音效")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip explosionAudio;

    private DamageSource damageSource;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private Collider2D[] projectileColliders;

    private readonly Collider2D[] hitBuffer =
        new Collider2D[32];

    private readonly HashSet<Component> damagedTargets =
        new HashSet<Component>();

    private Vector3 targetPoint;
    private bool initialized;
    private bool exploding;
    private bool damageDealt;

    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();

        projectileColliders =
            GetComponents<Collider2D>();

        if (animator == null)
            animator = GetComponent<Animator>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        foreach (Collider2D col in projectileColliders)
            col.isTrigger = true;
    }

    private void OnEnable()
    {
        initialized = false;
        exploding = false;
        damageDealt = false;

        body.velocity = Vector2.zero;

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.Stop();
        }

        if (spriteRenderer != null &&
            flightSprite != null)
        {
            spriteRenderer.sprite = flightSprite;
        }

        if (animator != null)
            animator.enabled = false;

        foreach (Collider2D col in projectileColliders)
            col.enabled = true;

        damageSource.hasSubProjectile = true;
        damageSource.enabled = false;
    }

    private void LateUpdate()
    {
        if (exploding)
            return;

        if (!initialized)
        {
            InitializeFlight();
            return;
        }

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetPoint,
                moveSpeed * Time.deltaTime
            );

        if (Vector2.Distance(
                transform.position,
                targetPoint
            ) <= arriveDistance)
        {
            BeginExplosion();
        }
    }

    private void InitializeFlight()
    {
        Vector3 direction =
            transform.right.normalized;

        if (direction.sqrMagnitude < 0.01f)
            direction = Vector3.right;

        float distance = 5f;

        if (damageSource.target != null)
        {
            distance =
                Vector2.Distance(
                    transform.position,
                    damageSource.target
                        .transform.position
                );
        }

        targetPoint =
            transform.position +
            direction * distance;

        initialized = true;
    }

    private void OnTriggerEnter2D(
        Collider2D other)
    {
        if (!canBeIntercepted || exploding)
            return;

        ICollide target =
            other.GetComponent<ICollide>();

        if (target == null)
            return;

        if (target.IsFriendly(
                damageSource.damage))
        {
            return;
        }

        BeginExplosion();
    }

    private void BeginExplosion()
    {
        if (exploding)
            return;

        exploding = true;
        body.velocity = Vector2.zero;

        foreach (Collider2D col in projectileColliders)
            col.enabled = false;

        if (animator == null)
            return;

        animator.enabled = true;

        if (!string.IsNullOrEmpty(
                explosionStateName))
        {
            animator.Play(
                explosionStateName,
                0,
                0f
            );
        }
    }

    // 爆炸动画伤害帧调用
    public void DealExplosionDamage()
    {
        if (!exploding || damageDealt)
            return;

        damageDealt = true;

        /*
         * 先通知攻击来源施加专属效果。
         * BabaDoctorC1会先让敌我全部晶化，
         * 然后才结算敌方伤害。
         */
        NotifyAttackSource();

        damagedTargets.Clear();

        int count =
            Physics2D.OverlapCircleNonAlloc(
                transform.position,
                explosionRadius,
                hitBuffer,
                targetLayers
            );

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = hitBuffer[i];

            ICollide target =
                hit.GetComponent<ICollide>();

            Component component =
                target as Component;

            if (target == null ||
                component == null)
            {
                continue;
            }

            if (!damagedTargets.Add(component))
                continue;

            // 友方只获得晶化，不受到伤害
            if (target.IsFriendly(
                    damageSource.damage))
            {
                continue;
            }

            Damage damage =
                damageSource.damage;

            damage.target = hit.gameObject;

            damage.collideDir =
                transform.position.x <
                hit.transform.position.x
                    ? 1
                    : -1;

            target.OnCollide(damage);
        }
    }

    private void NotifyAttackSource()
    {
        GameObject source =
            damageSource.damage.source;

        if (source == null)
            return;

        source.SendMessage(
            "OnProjectileDamageTriggered",
            transform.position,
            SendMessageOptions.DontRequireReceiver
        );
    }

    // 爆炸动画音效帧调用
    public void PlayExplosionAudio()
    {
        if (audioSource == null ||
            explosionAudio == null)
        {
            return;
        }

        audioSource.clip = explosionAudio;

        if (AudioManager.Instance != null &&
            Camera.main != null)
        {
            float distance =
                Vector3.Distance(
                    transform.position,
                    Camera.main.transform.position
                );

            AudioManager.Instance.PlayEffect(
                audioSource,
                (uint)audioSource.priority,
                distance,
                transform
            );
        }
        else
        {
            audioSource.PlayOneShot(
                explosionAudio
            );
        }
    }

    // 爆炸动画最后一帧调用
    public void FinishExplosion()
    {
        GameObjectPool.Instance.Release(
            gameObject
        );
    }
}