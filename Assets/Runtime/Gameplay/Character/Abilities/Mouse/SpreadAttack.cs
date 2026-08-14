using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class SpreadAttack : MonoBehaviour
{
    [Range(0f, 180f)]
    [SerializeField] private float spreadAngle = 15f;
    [SerializeField] private Transform shootPoint;

    private GameObjectProperty prop;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();

        if (shootPoint == null)
            shootPoint = transform.Find("ShootPoint");
    }

    public void ShootSpreadProjectile()
    {
        if (prop.target == null || string.IsNullOrEmpty(prop.atkObj))
            return;

        GameObject prefab =
            ResourceManager.Instance.GetGameObject(prop.atkObj);
        GameObjectPool pool = GameObjectPool.Instance;

        if (prefab == null || pool == null)
            return;

        GameObject projectile = pool.Get(prefab);
        if (projectile == null)
            return;

        Vector3 start = shootPoint != null
            ? shootPoint.position
            : transform.position;
        Vector3 direction =
            (prop.target.transform.position - start).normalized;

        float offset = Random.Range(-spreadAngle, spreadAngle);
        direction = Quaternion.Euler(0f, 0f, offset) * direction;

        projectile.transform.position = start;
        projectile.transform.right = direction;

        DamageSource source = projectile.GetComponent<DamageSource>();
        if (source == null)
        {
            pool.Release(projectile);
            return;
        }

        Damage damage = source.damage;
        damage.initialDamage = prop.atk;
        damage.source = gameObject;
        damage.side = prop.side;
        damage.repel = prop.repel;
        damage.type = DamageType.normal;

        source.damage = damage;
        source.target = null;

        prop.OnAtt?.Invoke();
    }
}