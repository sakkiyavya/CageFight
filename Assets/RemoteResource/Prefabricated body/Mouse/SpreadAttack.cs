using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class SpreadAttack : MonoBehaviour
{
    [Range(0f, 180f)]
    public float spreadAngle = 15f;

    public Transform shootPoint;

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

        GameObject projectile =
            GameObjectPool.Instance.Get(prefab);

        if (projectile == null)
            return;

        Vector3 start = shootPoint != null
            ? shootPoint.position
            : transform.position;

        projectile.transform.position = start;

        Vector3 direction =
            (prop.target.transform.position - start).normalized;

        float offset =
            Random.Range(-spreadAngle, spreadAngle);

        direction =
            Quaternion.Euler(0f, 0f, offset) * direction;

        projectile.transform.right = direction;

        DamageSource source =
            projectile.GetComponent<DamageSource>();

        if (source != null)
        {
            source.damage.initialDamage = prop.atk;
            source.damage.source = gameObject;
            source.damage.side = prop.side;
            source.damage.repel = prop.repel;
            source.damage.type = DamageType.normal;

            GameObject aimPoint = new GameObject("SpreadAimPoint");
            aimPoint.transform.position = start + direction * 10f;
            source.target = aimPoint;

            StartCoroutine(DestroyAimPoint(aimPoint));
        }

        prop.OnAtt?.Invoke();
    }

    private IEnumerator DestroyAimPoint(GameObject aimPoint)
    {
        yield return null;

        if (aimPoint != null)
            Destroy(aimPoint);
    }
}
