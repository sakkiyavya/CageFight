using UnityEngine;

/// <summary>
/// Cat son（猫子）机制：
/// 自带 baseMissChance（默认 30%）未命中率——由伤害结算（DamageComputor 的
/// missChance 掷骰）统一判定，经 IMissHandler 回调通知；
/// 每次攻击未命中：获得一层“颓废”（DecadentDebuff，默认 3 秒，攻击削弱 + 变暗）；
/// 每次攻击命中：获得一层“狂暴”（RageBuff，默认 5 秒，攻速/移速提升）。
/// 两层状态均经生命框架统一入口 ApplyBuff 施加，同实例重复施加按层管理，
/// 层独立计时、可无限叠加。
/// </summary>
public class CatSon : BehaviourBase, IMissHandler, IHitHandler
{
    [Header("未命中率")]
    [SerializeField, Range(0f, 1f)]
    private float baseMissChance = 0.3f;        // 自带未命中率（30%）。

    [Header("未命中惩罚")]
    [SerializeField, Min(0.1f)]
    private float decadentDuration = 3f;        // 每次未命中获得的颓废持续秒（3 秒）。

    [Header("命中奖励")]
    [SerializeField, Min(0.1f)]
    private float rageDuration = 5f;            // 每次命中获得的狂暴持续秒（5 秒）。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private DecadentDebuff _decadent;
    private RageBuff _rage;

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>命中/未命中奖励由伤害结算回调驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();

        _decadent = gameObject.AddComponent<DecadentDebuff>();
        _decadent.duration = decadentDuration;

        _rage = gameObject.AddComponent<RageBuff>();
        _rage.SetDuration(rageDuration);
    }

    private void OnEnable()
    {
        // 基础未命中率：写入属性基础值并立即生效（对象池回收再启用时重新应用）。
        if (_prop == null)
            return;

        _prop.baseMissChance = baseMissChance;
        _prop.missChance = baseMissChance;
    }

    /// <summary>伤害结算判定本次攻击未命中：获得一层颓废。</summary>
    public void OnAttackMissed()
    {
        if (_prop == null || _prop.isDead)
            return;

        if (_health != null)
            _health.ApplyBuff(_decadent);
    }

    /// <summary>本次攻击命中（未被未命中判定拦下）：获得一层狂暴。</summary>
    public void OnAttackHit()
    {
        if (_prop == null || _prop.isDead)
            return;

        if (_health != null)
            _health.ApplyBuff(_rage);
    }
}
