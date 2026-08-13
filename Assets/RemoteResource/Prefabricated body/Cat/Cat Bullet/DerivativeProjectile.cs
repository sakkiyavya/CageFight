using UnityEngine;

public class DerivativeProjectile : MonoBehaviour
{
    public float speed = 1, lifeTime = 5;
    [ResourceKey(typeof(GameObject))] public string projectileKey;
    public float searchRange = 5, fireInterval = 1, breatheSpeed = 2;
    public LayerMask targetLayer = ~0;
    public Color colorA = Color.white;
    public Color colorB = new Color(1, 1, 1, .25f);

    static readonly Collider2D[] hits = new Collider2D[64];
    SpriteRenderer sr;
    DamageSource carrier;
    Damage damage;
    Vector3 direction;
    float life, fire;
    int ownerSide;
    bool ready;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        carrier = GetComponent<DamageSource>();
    }

    void OnEnable()
    {
        life = fire = 0;
        ready = false;
    }

    void Update()
    {
        if (!ready)
        {
            if (!carrier || !carrier.damage.source) return;

            GameObjectProperty owner =
                carrier.damage.source.GetComponent<GameObjectProperty>();

            if (!owner) return;

            ownerSide = owner.side;
            damage = carrier.damage;
            damage.side = ownerSide;
            damage.initialDamage = owner.atk;
            damage.source = owner.gameObject;

            direction = transform.right.normalized;
            carrier.hasSubProjectile = true;
            carrier.sustainTime = lifeTime + 1;
            carrier.Init();
            ready = true;
        }

        life += Time.deltaTime;
        fire += Time.deltaTime;
        transform.position += direction * speed * Time.deltaTime;

        if (sr)
            sr.color = Color.Lerp(
                colorA, colorB,
                Mathf.PingPong(life * breatheSpeed, 1));

        if (fire >= fireInterval)
        {
            fire = 0;
            Shoot();
        }

        if (life >= lifeTime)
            GameObjectPool.Instance.Release(gameObject);
    }

    void Shoot()
    {
        GameObject target = FindTarget();
        GameObject prefab =
            ResourceManager.Instance.GetGameObject(projectileKey);

        if (!target || !prefab) return;

        GameObject bullet = GameObjectPool.Instance.Get(prefab);
        bullet.transform.position = transform.position;
        bullet.transform.right =
            target.transform.position - transform.position;

        DamageSource ds = bullet.GetComponent<DamageSource>();
        if (!ds) return;

        ds.damage = damage;
        ds.damage.target = target;
        ds.target = target;
        ds.Init();
    }

    GameObject FindTarget()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, searchRange, hits, targetLayer);

        GameObject target = null;
        float nearest = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            GameObjectProperty prop =
                hits[i].GetComponentInParent<GameObjectProperty>();

            if (!prop || prop.isDead || prop.side == ownerSide)
                continue;

            float distance =
                (prop.transform.position - transform.position).sqrMagnitude;

            if (distance < nearest)
            {
                nearest = distance;
                target = prop.gameObject;
            }
        }

        return target;
    }
}