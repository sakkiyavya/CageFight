using UnityEngine;

/// <summary>
/// 建筑穿透弹幕（Fortress 用）：弹幕穿过非建筑单位（角色等），
/// 只在命中敌方建筑时造成伤害并回收。
/// 需在同一对象的 DamageSource 上启用 hasSubProjectile（关闭其自带命中逻辑），
/// 由本脚本接管命中；存活计时仍由 DamageSource 按 sustainTime 兜底回收。
/// 伤害配置沿用 DamageSource.damage（发射方已写入攻击力、来源与阵营）。
/// 仅新增本脚本即可生效。
/// </summary>
[RequireComponent(typeof(DamageSource))]
public class BuildingPierceProjectile : MonoBehaviour
{
    private DamageSource damageSource;

    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
    }

    /// <summary>
    /// 处理触发器命中：非敌方建筑直接穿过；命中敌方建筑时结算伤害并回收弹幕。
    /// </summary>
    /// <param name="collision">进入触发器的二维碰撞体。</param>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (damageSource == null || collision == null)
            return;

        Damage damage = damageSource.damage;

        GameObjectProperty targetProp = collision.GetComponent<GameObjectProperty>();
        if (targetProp == null || targetProp.isDead || targetProp.isUntargetable ||
            targetProp.side == damage.side ||
            (targetProp.objectType & GameObjectType.Building) == 0)
            return;

        ICollide collide = collision.GetComponent<ICollide>();
        if (collide == null)
            return;

        damage.collideDir = transform.position.x < collision.transform.position.x ? 1 : -1;
        damage.target = collision.gameObject;
        collide.OnCollide(damage);

        if (GameObjectPool.Instance != null)
            GameObjectPool.Instance.Release(gameObject);
    }
}
