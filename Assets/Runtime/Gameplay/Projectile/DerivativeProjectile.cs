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

        // 弹幕预载：projectileKey 是本组件独立字段、不在关卡预载清单内，
        // 缓存未命中时 Shoot 会静默跳过（表现为"不攻击"）。启用时补发预载（编辑器直取路径同步缓存）。
        if (ResourceManager.Instance != null && !string.IsNullOrEmpty(projectileKey) &&
            ResourceManager.Instance.GetGameObject(projectileKey) == null)
        {
            ResourceManager.Instance.LoadExtraResourceAsync<GameObject>(projectileKey);
        }
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

        if (prefab == null)
        {
            // 弹幕仍未缓存：补发预载，后续齐射可用（本轮不发射）。
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.LoadExtraResourceAsync<GameObject>(projectileKey);
            return;
        }

        if (!target) return;

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