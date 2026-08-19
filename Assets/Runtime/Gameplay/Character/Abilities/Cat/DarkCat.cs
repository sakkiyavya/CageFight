using UnityEngine;

public class DarkCat : MonoBehaviour
{
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private Transform iconTarget;
    [SerializeField] private float range = 6f;
    [SerializeField] private float iconTime = .5f;
    [SerializeField] private LayerMask targetLayer = ~0;

    [SerializeField, Min(1)]
    private int falseLifeLayersPerKill = 1;    // 击杀敌人获得的妄业之力层数（默认 1）。

    static readonly Collider2D[] hits = new Collider2D[64];
    readonly GameObjectProperty[] targets = new GameObjectProperty[3];

    GameObjectProperty prop;
    CharacterHealth health;                    // 自身生命组件，用于回复等量生命。
    FalseLifeBuff falseLife;                   // 击杀后叠给自己使用的妄业之力实例。

    void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<CharacterHealth>();
        falseLife = gameObject.AddComponent<FalseLifeBuff>();
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

        // 同时攻击至多 3 个目标，累计本次实际造成的伤害用于回复等量生命。
        int dealt = 0;
        for (int i = 0; i < count; i++)
            dealt += Attack(targets[i]);

        if (dealt > 0)
            HealSelf(dealt);

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

    int Attack(GameObjectProperty target)
    {
        // 目标在索敌与本击之间死亡：跳过，避免冒领击杀。
        if (target.isDead)
            return 0;

        Damage damage = Damage.DefaultDamage;
        damage.side = prop.side;
        damage.source = gameObject;
        damage.target = target.gameObject;
        damage.initialDamage = prop.atk;
        damage.repel = prop.repel;

        Damage result = target.GetComponent<ICollide>().OnCollide(damage);

        // 击杀敌人（含建筑）：给自己叠加一层妄业之力。
        if (target.isDead)
        {
            for (int i = 0; i < falseLifeLayersPerKill; i++)
                prop.ApplyStatus(falseLife);
        }

        if (!iconPrefab) return result.missed ? 0 : result.finalDamage;

        GameObject icon =
            GameObjectPool.Instance.Get(iconPrefab);

        icon.transform.position = target.transform.position;

        DarkCatIcon fly = icon.GetComponent<DarkCatIcon>();
        if (!fly) fly = icon.AddComponent<DarkCatIcon>();

        fly.Play(iconTarget, iconTime);
        return result.missed ? 0 : result.finalDamage;
    }

    /// <summary>
    /// 回复等量生命：按 suckBlood 比例（Dark cat 为 100）回复本次攻击造成的总伤害，
    /// 经 CharacterHealth.Heal 结算（受重伤等治疗倍率影响、上限为最大生命）。
    /// </summary>
    void HealSelf(int dealtDamage)
    {
        if (dealtDamage <= 0 || health == null || prop.isDead)
            return;

        int heal = Mathf.RoundToInt(dealtDamage * prop.suckBlood / 100f);
        if (heal > 0)
            health.Heal(heal);
    }
}
