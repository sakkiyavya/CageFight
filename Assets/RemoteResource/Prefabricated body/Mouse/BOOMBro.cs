using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class BOOMBro : MonoBehaviour
{
    [Header("散射设置")]
    [Range(0f, 90f)]
    [SerializeField] private float spreadAngle = 30f;

    [SerializeField] private Transform shootPoint;

    private GameObjectProperty prop;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();

        if (shootPoint == null)
            shootPoint = transform.Find("ShootPoint");
    }

    // 攻击动画调用
    public void BOOMShoot()
    {
        if (prop == null ||
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

        Vector3 firePosition =
            shootPoint != null
                ? shootPoint.position
                : transform.position;

        projectile.transform.position = firePosition;

        Vector2 direction;

        if (prop.target != null)
        {
            direction =
                (prop.target.transform.position -
                 firePosition).normalized;
        }
        else
        {
            direction =
                prop.isFacingLeft
                    ? Vector2.left
                    : Vector2.right;
        }

        float randomAngle =
            Random.Range(
                -spreadAngle,
                spreadAngle
            );

        direction =
            Quaternion.Euler(
                0f,
                0f,
                randomAngle
            ) * direction;

        projectile.transform.right = direction;

        DamageSource damageSource =
            projectile.GetComponent<DamageSource>();

        if (damageSource != null)
        {
            /*
             * 复制原Damage，保留ProjectileBuffCarrier
             * 已经添加的Buff。
             */
            Damage damage = damageSource.damage;

            damage.initialDamage = prop.atk;
            damage.source = gameObject;
            damage.side = prop.side;
            damage.repel = prop.repel;
            damage.type = DamageType.normal;

            damageSource.damage = damage;
            damageSource.target = prop.target;
        }

        prop.OnAtt?.Invoke();
    }

    /*
     * 通用爆炸弹幕在伤害帧通知攻击来源。
     * 这是BOOM Bro自己的专属后坐力。
     */
    public void OnProjectileDamageTriggered(
        Vector3 explosionPosition)
    {
        float direction =
            transform.position.x < explosionPosition.x
                ? -1f
                : 1f;

        prop.repelDistance =
            direction * Mathf.Abs(prop.repel);

        prop.isRepel = true;
    }
}