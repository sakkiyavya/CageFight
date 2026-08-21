using UnityEngine;

/// <summary>
/// Fortress 碰撞伤害：与敌方建筑保持接触时，每秒对其造成自身最大生命一定比例
/// （默认 1%）的碰撞伤害。
/// 通过计时器限制结算频率（默认每 1 秒一次），避免触发器逐帧持续刷伤；
/// 伤害经既有 Damage + ICollide.OnCollide 链路（BuildingHealth.TakeDamage 已能扣血）。
/// 仅新增本脚本即可生效。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class FortressAbility : BehaviourBase
{
    [Header("碰撞伤害")]
    [SerializeField, Range(0f, 1f)]
    private float collisionDamagePercent = 0.01f;   // 每次碰撞伤害 = 自身最大生命 × 该比例（1%）。
    [SerializeField, Min(0.1f)]
    private float collisionInterval = 1f;           // 两次碰撞伤害之间的最小间隔（防刷伤）。

    private GameObjectProperty _prop;
    private float elapsed;                          // 距上次碰撞伤害的计时。

    private Damage applyDamage = Damage.DefaultDamage;   // 复用的伤害结构。

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
    }

    /// <summary>碰撞伤害由触发器事件驱动，无每帧行为；返回 false 放行后续 AI 行为。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }

    private void OnEnable()
    {
        elapsed = collisionInterval;
    }

    /// <summary>
    /// 与敌方建筑保持接触时，按间隔结算一次碰撞伤害。
    /// </summary>
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (_prop == null || _prop.isDead)
            return;

        elapsed += Time.deltaTime;
        if (elapsed < collisionInterval)
            return;

        GameObjectProperty targetProp = collision.GetComponent<GameObjectProperty>();
        if (targetProp == null || targetProp.isDead || targetProp.isUntargetable ||
            targetProp.side == _prop.side ||
            (targetProp.objectType & GameObjectType.Building) == 0)
            return;

        ICollide collide = collision.GetComponent<ICollide>();
        if (collide == null)
            return;

        elapsed = 0f;

        applyDamage.side = _prop.side;
        applyDamage.initialDamage = Mathf.Max(1, Mathf.RoundToInt(_prop.maxHp * collisionDamagePercent));
        applyDamage.finalDamage = 0;
        applyDamage.type = DamageType.normal;
        applyDamage.source = gameObject;
        applyDamage.target = collision.gameObject;
        applyDamage.collideDir = 1;
        applyDamage.repel = 0f;
        applyDamage.buffs = null;

        collide.OnCollide(applyDamage);
    }
    #endregion
}
