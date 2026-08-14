using UnityEngine;

public class DarkCat : MonoBehaviour
{
    public GameObject iconPrefab;
    public Transform iconTarget;
    public float range = 6f;
    public float iconTime = .5f;
    public LayerMask targetLayer = ~0;

    static readonly Collider2D[] hits = new Collider2D[64];
    readonly GameObjectProperty[] targets = new GameObjectProperty[3];

    GameObjectProperty prop;

    void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        if (!iconTarget) iconTarget = transform;
    }

    // 攻击动画事件调用
    public void DarkCatAttack()
    {
        if (prop.isDead) return;

        int count = 0;
        AddTarget(prop.target, ref count);

        int found = Physics2D.OverlapCircleNonAlloc(
            transform.position, range, hits, targetLayer);

        for (int i = 0; i < found && count < 3; i++)
            AddTarget(hits[i].gameObject, ref count);

        for (int i = 0; i < count; i++)
            Attack(targets[i]);

        if (count > 0)
            prop.OnAtt?.Invoke();
    }

    void AddTarget(GameObject obj, ref int count)
    {
        if (!obj || count >= 3) return;

        GameObjectProperty target =
            obj.GetComponentInParent<GameObjectProperty>();

        if (!target || target == prop ||
            target.isDead || target.side == prop.side ||
            target.GetComponent<ICollide>() == null)
            return;

        for (int i = 0; i < count; i++)
            if (targets[i] == target) return;

        targets[count++] = target;
    }

    void Attack(GameObjectProperty target)
    {
        Damage damage = Damage.DefaultDamage;
        damage.side = prop.side;
        damage.source = gameObject;
        damage.target = target.gameObject;
        damage.initialDamage = prop.atk;
        damage.repel = prop.repel;

        target.GetComponent<ICollide>().OnCollide(damage);

        if (!iconPrefab) return;

        GameObject icon =
            GameObjectPool.Instance.Get(iconPrefab);

        icon.transform.position = target.transform.position;

        DarkCatIcon fly = icon.GetComponent<DarkCatIcon>();
        if (!fly) fly = icon.AddComponent<DarkCatIcon>();

        fly.Play(iconTarget, iconTime);
    }
}

public class DarkCatIcon : MonoBehaviour
{
    Transform target;
    Vector3 startPosition, startScale;
    float duration, timer;

    public void Play(Transform destination, float time)
    {
        target = destination;
        startPosition = transform.position;
        startScale = transform.localScale;
        duration = Mathf.Max(.01f, time);
        timer = 0;
    }

    void Update()
    {
        if (!target) return;

        timer += Time.deltaTime;
        float t = Mathf.Clamp01(timer / duration);

        transform.position =
            Vector3.Lerp(startPosition, target.position, t);

        transform.localScale =
            Vector3.Lerp(startScale, Vector3.zero, t);

        if (t >= 1)
        {
            transform.localScale = startScale;
            GameObjectPool.Instance.Release(gameObject);
        }
    }
}
