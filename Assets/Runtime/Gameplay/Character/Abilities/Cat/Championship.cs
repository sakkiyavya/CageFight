using UnityEngine;

/// <summary>
/// Championship（锦标赛）机制：每次攻击命中敌方单位时判定——
/// 若目标“质量”（Rigidbody2D.mass）低于 massThreshold（默认 4）
/// 且当前血量低于 hpPercentThreshold（默认 15%），
/// 则先赋予 target 层“妄业之力”（FalseLifeBuff，默认 2 层），
/// 随后对其造成致命伤害直接斩杀（伤害经 OnCollide 完整结算，
/// 生命归零时由妄业之力的 IDeathReviver 接管“假死”流程）。
/// 通过 GameObjectProperty.OnAtt 接入，仅新增本脚本即可生效。
/// </summary>
public class Championship : MonoBehaviour
{
    [Header("斩杀判定")]
    [SerializeField]
    private float massThreshold = 4f;          // 质量阈值：目标 Rigidbody2D.mass 低于该值才触发。
    [SerializeField, Range(0f, 1f)]
    private float hpPercentThreshold = 0.15f;  // 血量阈值：目标当前血量比例低于该值才触发。

    [Header("斩杀效果")]
    [SerializeField, Min(1)]
    private int falseLifeLayers = 2;           // 赋予的妄业之力层数。
    [SerializeField, Min(1f)]
    private float executeDamageMultiplier = 10f; // 斩杀伤害 = 目标最大生命 × 该倍数（保证致命）。

    private GameObjectProperty _prop;
    private FalseLifeBuff _falseLife;
    private Damage _execDamage;

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _falseLife = gameObject.AddComponent<FalseLifeBuff>();
        _execDamage = Damage.DefaultDamage;
    }

    private void OnEnable()
    {
        if (_prop != null)
            _prop.OnAtt += HandleAttack;
    }

    private void OnDisable()
    {
        if (_prop != null)
            _prop.OnAtt -= HandleAttack;
    }

    /// <summary>
    /// 响应攻击事件：命中目标满足“质量低 + 血量低”时，赋予妄业之力并斩杀。
    /// </summary>
    private void HandleAttack()
    {
        if (_prop == null || _prop.target == null)
            return;

        GameObjectProperty targetProp = _prop.target.GetComponent<GameObjectProperty>();
        if (targetProp == null || targetProp.isDead)
            return;

        // 质量判定：目标 Rigidbody2D 的质量需低于阈值（无刚体视为不满足）。
        Rigidbody2D targetBody = targetProp.GetComponent<Rigidbody2D>();
        if (targetBody == null || targetBody.mass >= massThreshold)
            return;

        // 血量判定：当前血量比例需低于阈值。
        if (targetProp.maxHp <= 0 ||
            (float)targetProp.currentHp / targetProp.maxHp >= hpPercentThreshold)
            return;

        ICollide collide = targetProp.GetComponent<ICollide>();
        if (collide == null)
            return;

        // 先赋予妄业之力（死亡后假死接管），再造成致命伤害斩杀。
        for (int i = 0; i < falseLifeLayers; i++)
            _falseLife.ApplyBuff(targetProp);

        _execDamage.side = _prop.side;
        _execDamage.source = gameObject;
        _execDamage.target = _prop.target;
        _execDamage.initialDamage = Mathf.Max(1, Mathf.RoundToInt(targetProp.maxHp * executeDamageMultiplier));
        _execDamage.finalDamage = 0;
        _execDamage.type = DamageType.normal;
        _execDamage.repel = 0f;
        _execDamage.collideDir = transform.position.x < targetProp.transform.position.x ? 1 : -1;
        collide.OnCollide(_execDamage);
    }
}
