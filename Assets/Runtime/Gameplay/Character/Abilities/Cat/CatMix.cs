using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class CatMix : BehaviourBase
{
    [SerializeField] private float range = 2f;
    [SerializeField, Range(0f, 1f)]
    private float hpLoss = 0.2f;

    private static readonly Collider2D[] hits =
        new Collider2D[32];

    private GameObjectProperty prop;
    private CharacterHealth health;
    private bool triggered;

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (this.prop == null)
            this.prop = prop;
        if (this.health == null)
            this.health = health;
    }

    /// <summary>死亡连锁由生命框架 Died 事件驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        triggered = false;
        if (health != null)
            health.Died += HandleDied;
    }

    private void OnDisable()
    {
        if (health != null)
            health.Died -= HandleDied;
    }

    /// <summary>
    /// 死亡时触发一次周边连锁扣血（经生命框架 Died 事件接入，不再轮询死亡状态）。
    /// </summary>
    private void HandleDied(GameObject unit)
    {
        if (triggered)
            return;

        triggered = true;
        TriggerNearby();
    }

    private void TriggerNearby()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            range,
            hits
        );

        for (int i = 0; i < count; i++)
        {
            CatMix target =
                hits[i].GetComponentInParent<CatMix>();

            if (target == null ||
                target == this ||
                target.prop.isDead)
            {
                continue;
            }

            // 防止同一单位的多个碰撞体重复扣血
            bool repeated = false;

            for (int j = 0; j < i; j++)
            {
                if (hits[j] != null &&
                    hits[j].GetComponentInParent<CatMix>()
                    == target)
                {
                    repeated = true;
                    break;
                }
            }

            if (!repeated)
                target.LoseHp();
        }
    }

    private void LoseHp()
    {
        int damageValue =
            Mathf.RoundToInt(prop.maxHp * hpLoss);

        Damage damage = Damage.DefaultDamage;
        damage.initialDamage = damageValue;
        damage.source = gameObject;
        damage.target = gameObject;
        damage.side = prop.side;
        damage.repel = 0f;

        if (health != null)
            health.TakeDamage(damage);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            range
        );
    }
}