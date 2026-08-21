using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class BOOMBro : BehaviourBase, IProjectileImpactHandler
{
    [Header("攻击设置")]
    [Range(0f, 90f)]
    [SerializeField] private float spreadAngle = 30f;

    [SerializeField] private Transform shootPoint;

    [Header("自身击退")]
    [Min(0.1f)]
    [SerializeField] private float selfRepelRadius = 2f;

    private GameObjectProperty prop;
    private Collider2D ownCollider;

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
    }

    /// <summary>纯攻击被动：无每帧行为，返回 false 放行后续 AI 行为。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        ownCollider = GetComponent<Collider2D>();

        if (shootPoint == null)
            shootPoint = transform.Find("ShootPoint");
    }

    // 攻击动画调用
    public void BOOMShoot()
    {
        if (prop.target == null ||
            string.IsNullOrEmpty(prop.atkObj))
        {
            return;
        }

        GameObject prefab =
            ResourceManager.Instance.GetGameObject(
                prop.atkObj
            );

        if (prefab == null)
            return;

        GameObject projectile =
            GameObjectPool.Instance.Get(prefab);

        if (projectile == null)
            return;

        Vector3 start =
            shootPoint != null
                ? shootPoint.position
                : transform.position;

        projectile.transform.position = start;

        Vector2 direction =
            (prop.target.transform.position - start)
            .normalized;

        float angle =
            Random.Range(
                -spreadAngle,
                spreadAngle
            );

        direction =
            Quaternion.Euler(0f, 0f, angle) *
            direction;

        projectile.transform.right = direction;

        DamageSource source =
            projectile.GetComponent<DamageSource>();

        if (source != null)
        {
            Damage damage = source.damage;

            damage.initialDamage = prop.atk;
            damage.source = gameObject;
            damage.side = prop.side;
            damage.repel = prop.repel;
            damage.type = DamageType.normal;

            source.damage = damage;
            source.target = prop.target;
        }

        prop.OnAtt?.Invoke();
    }

    // 弹幕爆炸伤害帧自动调用
    public void OnProjectileDamageTriggered(
        Vector3 explosionPosition)
    {
        Vector2 nearestPoint =
            ownCollider != null
                ? ownCollider.ClosestPoint(
                    explosionPosition
                )
                : (Vector2)transform.position;

        // 不在爆炸范围内，不触发自身击退
        if (Vector2.Distance(
                nearestPoint,
                explosionPosition
            ) > selfRepelRadius)
        {
            return;
        }

        float direction =
            transform.position.x <
            explosionPosition.x
                ? -1f
                : 1f;

        prop.repelDistance =
            direction *
            Mathf.Abs(prop.repel);

        prop.isRepel = true;
    }
}