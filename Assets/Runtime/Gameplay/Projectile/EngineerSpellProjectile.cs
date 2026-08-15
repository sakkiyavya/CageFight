using UnityEngine;

[RequireComponent(typeof(DamageSource))]
/// <summary>由对象池复用的工程师抛射法术实例，命中数据继承施法工程师阵营。</summary>
public sealed class EngineerSpellProjectile : MonoBehaviour, IEngineerAimedSpellInstance
{
    [SerializeField, Min(0)] private int damage = 20;
    [SerializeField] private float spinSpeed = 720f;
    [SerializeField] private float repel;
    [SerializeField] private DamageType damageType = DamageType.magic;

    Vector3 start, target;
    Quaternion launchRotation;
    float elapsed, flightTime, arcHeight;
    bool finished, initialized;
    DamageSource source;

    void Awake() => source = GetComponent<DamageSource>();

    void OnEnable()
    {
        elapsed = 0f;
        finished = false;
        initialized = false;
    }

    /// <summary>重置池化状态，并以施法者阵营和落点初始化飞行数据。</summary>
    public void Initialize(EngineerController caster, SpellDefinition definition, Vector3 landingPoint)
    {
        if (!caster || !definition || !source || !TryGetPool(out _))
        {
            DisableInvalidInstance();
            return;
        }

        start = transform.position;
        target = landingPoint;
        launchRotation = transform.rotation;
        flightTime = Mathf.Max(0.01f, definition.FlightTime);
        arcHeight = definition.ArcHeight;
        elapsed = 0f;

        source.damage.side = caster.Side;
        source.damage.source = caster.gameObject;
        source.damage.initialDamage = damage;
        source.damage.finalDamage = 0;
        source.damage.target = null;
        source.damage.type = damageType;
        source.damage.repel = repel;
        source.target = null;
        source.Init();
        initialized = true;
    }

    void Update()
    {
        if (finished || !initialized) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / flightTime);
        transform.position = Vector3.Lerp(start, target, t) +
            Vector3.up * (4f * arcHeight * t * (1f - t));
        transform.rotation = launchRotation * Quaternion.Euler(0f, 0f, spinSpeed * elapsed);

        if (t < 1f) return;
        ReturnToPool();
    }

    bool TryGetPool(out GameObjectPool pool)
    {
        pool = GameObjectPool.Instance;
        return pool && pool.GetPrefab(gameObject);
    }

    void ReturnToPool()
    {
        if (finished) return;

        finished = true;
        initialized = false;
        if (TryGetPool(out GameObjectPool pool))
            pool.Release(gameObject);
        else
            DisableInvalidInstance();
    }

    void DisableInvalidInstance()
    {
        finished = true;
        initialized = false;
        Debug.LogError("[EngineerSpellProjectile] 必须由 GameObjectPool 生成。", this);
        gameObject.SetActive(false);
    }
}
