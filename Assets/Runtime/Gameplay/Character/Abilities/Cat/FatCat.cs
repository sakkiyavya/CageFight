using UnityEngine;

/// <summary>
/// Fat Cat（肥猫）机制：每次攻击对自身施加一层“虚假力量”（FalsePowerBuff），
/// 每层持续 layerDuration 秒（默认 4 秒）。
/// 虚假力量：每层 +1 击退（随攻击魔法等级成长）、可无限叠加、层独立计时、
/// 无视觉表现。
/// 通过 GameObjectProperty.OnAtt 事件接入，仅新增本脚本即可生效。
/// </summary>
public class FatCat : BehaviourBase
{
    [Header("虚假力量")]
    [SerializeField, Min(0.1f)]
    private float layerDuration = 4f;       // 每层持续秒（4 秒）。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private FalsePowerBuff _falsePower;

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
        _falsePower = gameObject.AddComponent<FalsePowerBuff>();
        _falsePower.SetDuration(layerDuration);
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
    /// 每次攻击：对自身施加一层虚假力量（统一状态入口登记）。
    /// </summary>
    private void HandleAttack()
    {
        if (_prop == null || _prop.isDead || _health == null)
            return;

        _health.ApplyBuff(_falsePower);
    }
}
