using UnityEngine;

/// <summary>
/// 创伤覆盖云（Skunk 绿雾用）：每隔固定间隔（默认 0.5 秒）对当前覆盖区域内
/// 的敌方单位施加一层创伤（TraumaDebuff）。
/// 覆盖区域取自身触发碰撞体的包围盒；阵营沿用同一对象 DamageSource 的伤害阵营。
/// 使用 NonAlloc 复用缓冲，无热路径分配。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(DamageSource))]
[RequireComponent(typeof(Collider2D))]
public class TraumaCloud : MonoBehaviour
{
    [Header("创伤施加")]
    [SerializeField, Min(0.05f)]
    private float interval = 0.5f;           // 每次施加创伤的间隔秒。
    [SerializeField, Min(0.1f)]
    private float traumaDuration = 5f;       // 每层创伤的持续秒数。
    [SerializeField]
    private LayerMask enemyMask = ~0;        // 参与覆盖判定的层。

    private static readonly Collider2D[] hits = new Collider2D[64];    // 复用的覆盖扫描缓冲。

    private DamageSource damageSource;
    private Collider2D trigger;
    private TraumaDebuff trauma;
    private float elapsed;

    #region 生命周期与回调
    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
        trigger = GetComponent<Collider2D>();

        // 运行时创建并配置创伤实例，避免预制体额外挂载组件。
        trauma = gameObject.AddComponent<TraumaDebuff>();
        trauma.SetDuration(traumaDuration);
    }

    private void OnEnable()
    {
        elapsed = 0f;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed < interval)
            return;

        elapsed = 0f;
        ApplyTraumaToCovered();
    }
    #endregion

    #region 内部实现
    /// <summary>
    /// 扫描当前覆盖区域内的敌方单位，对其施加一层创伤。
    /// 阵营沿用 DamageSource 的伤害阵营，来源沿用其伤害来源（施法者）。
    /// </summary>
    private void ApplyTraumaToCovered()
    {
        if (damageSource == null || trigger == null || trauma == null)
            return;

        Bounds bounds = trigger.bounds;
        int count = Physics2D.OverlapBoxNonAlloc(
            bounds.center, bounds.size, 0f, hits, enemyMask);

        for (int i = 0; i < count; i++)
        {
            if (hits[i] == null)
                continue;

            GameObjectProperty prop = hits[i].GetComponent<GameObjectProperty>();
            if (prop == null || prop.isDead || prop.isUntargetable ||
                prop.side == damageSource.damage.side ||
                prop.GetComponent<ICollide>() == null)
                continue;

            // 携带来源与阵营施加创伤，使其快照正确的施法者归属。
            trauma.ApplyBuff(prop, damageSource.damage);
        }
    }
    #endregion
}
