using UnityEngine;

/// <summary>
/// Water breath cat（水息猫）机制（后半段）：每次攻击后对自身施加一层“护甲”
/// （ArmorBuff），每层持续 layerDuration 秒（默认 5 秒）。
/// 护甲：每层提供免伤点数（默认 10，随创造者单位等级成长接缝）、可无限叠加、
/// 蓝色呼吸光效；施加时以自身为创造者。
/// 命中敌人施加寒冷的机制已由既有实现负责，本脚本只负责攻击后自叠护甲。
/// 通过 GameObjectProperty.OnAtt 事件接入，仅新增本脚本即可生效。
/// </summary>
public class WaterBreathCat : BehaviourBase
{
    [Header("护甲")]
    [SerializeField, Min(0.1f)]
    private float layerDuration = 5f;       // 每层护甲持续秒（5 秒）。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private ArmorBuff _armor;
    private Damage _selfDamage;             // 以自身为创造者的施加伤害数据（复用）。

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>攻击被动由 OnAtt 事件驱动，无每帧行为；返回 false 放行后续 AI。</summary>
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
        _selfDamage.source = gameObject;
        if (_prop != null)
            _prop.OnAtt += HandleAttack;
    }

    private void OnDisable()
    {
        if (_prop != null)
            _prop.OnAtt -= HandleAttack;
    }

    /// <summary>
    /// 每次攻击后：对自身施加一层护甲（以自身为创造者，供等级接缝读取）。
    /// </summary>
    private void HandleAttack()
    {
        if (_prop == null || _prop.isDead || _health == null)
            return;

        _health.ApplyBuff(_armor, _selfDamage);
    }
}
