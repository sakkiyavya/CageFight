using UnityEngine;

/// <summary>
/// Cat Bonze（猫僧）：攻击时对自身获得 layersPerAttack 层“护甲”（每层 layerDuration 秒），
/// 被攻击时失去 losePerHit 层“护甲”。
/// 护甲（ArmorBuff）：每层提供免伤点数（默认 10 点，随创造者单位等级成长），
/// 可无限叠加、蓝色呼吸光效；施加时以自身为创造者（等级接缝，当前等级系统未建成时为 10 点/层）。
/// 通过 GameObjectProperty 的 OnAtt（攻击）/OnHitted（被击）事件挂接，不侵入 AI 流程。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class CatBonze : BehaviourBase
{
    [Header("护甲施放")]
    [SerializeField, Min(1)]
    private int layersPerAttack = 3;        // 每次攻击获得的护甲层数（3 层）。
    [SerializeField, Min(1)]
    private int losePerHit = 1;             // 每次被攻击失去的护甲层数（1 层）。
    [SerializeField, Min(0.1f)]
    private float layerDuration = 5f;       // 每层持续秒（5 秒）。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private ArmorBuff _armor;
    private Damage _selfDamage;             // 以自身为创造者的施放伤害数据（复用，避免每帧构造）。

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>攻击/受击被动由事件驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
        _armor = gameObject.AddComponent<ArmorBuff>();
        _armor.SetDuration(layerDuration);
        _selfDamage = Damage.DefaultDamage;
    }

    private void OnEnable()
    {
        if (_prop != null)
        {
            _prop.OnAtt += HandleAttack;
            _prop.OnHitted += HandleHitted;
        }
    }

    private void OnDisable()
    {
        if (_prop != null)
        {
            _prop.OnAtt -= HandleAttack;
            _prop.OnHitted -= HandleHitted;
        }
    }

    /// <summary>
    /// 每次攻击：对自身施加 layersPerAttack 层护甲（以自身为创造者，供等级接缝读取）。
    /// </summary>
    private void HandleAttack()
    {
        if (_prop == null || _prop.isDead)
            return;

        _selfDamage.source = gameObject;
        for (int i = 0; i < layersPerAttack; i++)
            _health.ApplyBuff(_armor, _selfDamage);
    }

    /// <summary>
    /// 每次被攻击：移除 losePerHit 层护甲。
    /// </summary>
    private void HandleHitted(Damage damage)
    {
        if (_prop == null || _prop.isDead)
            return;

        for (int i = 0; i < losePerHit; i++)
            _armor.CancelBuff(_prop);
    }
}
