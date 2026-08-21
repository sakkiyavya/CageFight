using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class BabaDoctorC1Script : BehaviourBase, IProjectileImpactHandler
{
    [SerializeField] private Transform shootPoint;
    [SerializeField] private CrystallizationDebuff crystallization;
    [SerializeField] private float crystallizationRadius = 2f;
    [SerializeField] private LayerMask targetLayers = ~0;

    private GameObjectProperty prop;

    // 固定缓存，避免爆炸时创建新数组
    private readonly Collider2D[] hitBuffer =
        new Collider2D[32];

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
    }

    /// <summary>弹幕触发为事件驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();

        if (shootPoint == null)
            shootPoint = transform.Find("ShootPoint");
    }

    // 攻击动画调用
    public void BabaShoot()
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

        projectile.transform.right =
            (prop.target.transform.position -
             start).normalized;

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
        if (crystallization == null)
            return;

        int count =
            Physics2D.OverlapCircleNonAlloc(
                explosionPosition,
                crystallizationRadius,
                hitBuffer,
                targetLayers
            );

        for (int i = 0; i < count; i++)
        {
            GameObjectProperty target =
                hitBuffer[i].GetComponentInParent<
                    GameObjectProperty
                >();

            if (target == null)
                continue;

            // 已晶化的单位只刷新时间（经生命框架统一入口施加并登记）。
            CharacterHealth targetHealth = target.GetComponent<CharacterHealth>();
            if (targetHealth == null || !targetHealth.ApplyBuff(crystallization))
                continue;
        }
    }
}