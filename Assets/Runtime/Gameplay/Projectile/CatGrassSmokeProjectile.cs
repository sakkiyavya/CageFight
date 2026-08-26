using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cat addict 的弹幕（预制体 "Cat grass smoke"）：
/// - 飞行：正常直线弹道——从发射点直线飞向“预计攻击地点”（发射瞬间锁定的目标位置，
///   目标移动/死亡不影响落点），到达后消失。
/// - 无限穿透：命中目标后不消失，继续直线飞行，沿途可命中任意数量的敌方/友方；
///   同一目标只结算一次（已命中集合去重）。
/// - 命中敌方：施加 1 层“浓缩”（默认 4 秒），并照常结算弹幕伤害；
/// - 命中友方：不造成伤害，为友方恢复施法者（Cat addict）最大生命 7% 的 HP，
///   并施加 1 层“狂暴”（默认 4 秒）。
/// 采用子投射物模式（hasSubProjectile = true 并禁用 DamageSource），自行处理命中与回收。
/// </summary>
[RequireComponent(typeof(DamageSource))]
[RequireComponent(typeof(Collider2D))]
public class CatGrassSmokeProjectile : MonoBehaviour
{
    [Header("飞行")]
    [SerializeField, Min(0.1f)]
    private float moveSpeed = 6f;           // 直线飞行速度。

    [Header("命中效果")]
    [SerializeField, Min(0.1f)]
    private float concentrateDuration = 4f; // 敌方获得的浓缩持续秒。
    [SerializeField, Min(0.1f)]
    private float rageDuration = 4f;        // 友方获得的狂暴持续秒。
    [SerializeField, Min(0f)]
    private float healHpPercent = 0.07f;    // 友方恢复施法者最大生命的比例（7%）。

    private DamageSource damageSource;
    private ConcentratedBuff concentrate;
    private RageBuff rage;

    private Vector3 startPos;               // 本次飞行的发射点。
    private Vector3 targetPoint;            // 预计攻击地点（发射瞬间锁定的目标位置）。
    private float travelTime;               // 本次飞行总时长。
    private float elapsed;                  // 本次飞行已耗时。
    private bool flying;                    // 是否正在飞行。
    private bool initialized;               // 首帧是否已完成起点/落点锁定。
    private readonly HashSet<GameObject> hitTargets = new HashSet<GameObject>(); // 已结算的目标（无限穿透去重）。

    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
        concentrate = gameObject.AddComponent<ConcentratedBuff>();
        concentrate.SetDuration(concentrateDuration);
        rage = gameObject.AddComponent<RageBuff>();
        rage.SetDuration(rageDuration);
    }

    private void OnEnable()
    {
        // 子投射物模式：自行处理飞行、命中与回收，跳过 DamageSource 的触发器与计时逻辑。
        damageSource.hasSubProjectile = true;
        damageSource.enabled = false;

        flying = true;
        elapsed = 0f;
        initialized = false; // 首帧再锁定起点与落点（射手在对象池 Get 之后才设置朝向与 target）。
        hitTargets.Clear();  // 穿透命中记录随每次发射重置。
    }

    private void LateUpdate()
    {
        if (!flying)
            return;

        // 首帧初始化：此时 CharacterAI.ShootProjectile 已完成位置、朝向与 target 的设置。
        if (!initialized)
        {
            initialized = true;
            startPos = transform.position;

            // 预计攻击地点：发射瞬间锁定的目标位置（此后不追踪）。
            GameObject target = damageSource.target;
            if (target != null)
                targetPoint = target.transform.position;
            else
                targetPoint = startPos + transform.right * 10f;

            float dist = Vector3.Distance(startPos, targetPoint);
            travelTime = Mathf.Max(0.01f, dist / moveSpeed);
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / travelTime);

        // 正常直线弹道：从发射点直线插值到预计攻击地点（无晃动）。
        transform.position = Vector3.Lerp(startPos, targetPoint, t);

        if (t >= 1f)
            Release();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!flying || !initialized)
            return;

        // 排除发射者本体：弹幕在发射点生成时会与发射者的碰撞体重叠。
        if (damageSource.damage.source != null &&
            collision.gameObject == damageSource.damage.source)
            return;

        // 无限穿透：同一目标只结算一次；命中后弹幕不消失，继续飞行穿透后续目标。
        if (!hitTargets.Add(collision.gameObject))
            return;

        GameObjectProperty targetProp = collision.GetComponent<GameObjectProperty>();
        if (targetProp == null || targetProp.isDead)
            return;

        bool friendly = damageSource.damage.side == targetProp.side;
        if (friendly)
        {
            HandleFriendlyHit(collision, targetProp);
        }
        else
        {
            HandleEnemyHit(collision, targetProp);
        }
        // 穿透：不回收，继续飞行，直到到达预计攻击地点。
    }

    /// <summary>
    /// 命中敌方：施加浓缩并照常结算弹幕伤害。
    /// </summary>
    private void HandleEnemyHit(Collider2D collision, GameObjectProperty targetProp)
    {
        concentrate.ApplyBuff(targetProp);

        ICollide c = collision.GetComponent<ICollide>();
        if (c == null)
            return;

        damageSource.damage.collideDir = transform.position.x < collision.transform.position.x ? 1 : -1;
        damageSource.damage.target = collision.gameObject;
        c.OnCollide(damageSource.damage);

        // 弹幕命中通知：被命中的目标实现 IProjectileImpactHandler 时回调
        // （如 General Cat 每次受到弹幕攻击获得一层护甲）。
        IProjectileImpactHandler impact = collision.GetComponent<IProjectileImpactHandler>();
        if (impact != null)
            impact.OnProjectileDamageTriggered(collision.transform.position);
    }

    /// <summary>
    /// 命中友方：不造成伤害，恢复施法者最大生命 7% 的 HP 并施加一层狂暴。
    /// </summary>
    private void HandleFriendlyHit(Collider2D collision, GameObjectProperty targetProp)
    {
        GameObject caster = damageSource.damage.source;
        GameObjectProperty casterProp = caster != null ? caster.GetComponent<GameObjectProperty>() : null;

        // 恢复比例优先读施法者（Cat addict）上可配置的 healHpPercent，弹幕自身字段仅作兜底。
        float percent = healHpPercent;
        if (caster != null)
        {
            CatAddict addict = caster.GetComponent<CatAddict>();
            if (addict != null)
                percent = addict.HealHpPercent;
        }

        if (casterProp != null && casterProp.maxHp > 0)
        {
            CharacterHealth health = collision.GetComponent<CharacterHealth>();
            if (health != null)
                health.Heal(Mathf.Max(1, Mathf.RoundToInt(casterProp.maxHp * percent)));
        }

        rage.ApplyBuff(targetProp);
    }

    /// <summary>
    /// 结束本次飞行并回收弹幕。
    /// </summary>
    private void Release()
    {
        if (!flying)
            return;

        flying = false;
        GameObjectPool pool = GameObjectPool.Instance;
        if (pool != null)
            pool.Release(gameObject);
    }
}
