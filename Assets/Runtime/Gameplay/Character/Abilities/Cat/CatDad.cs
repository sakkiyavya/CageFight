using UnityEngine;

/// <summary>
/// Cat dad（猫爸）：基础拥有 50% 未命中率（baseMissChance，可调），
/// 每次攻击被伤害结算判定未命中时获得一层“狂暴”（默认 4 秒）。
/// 未命中由伤害结算（DamageComputor 的 missChance 掷骰）统一判定，
/// 通过 IMissHandler 回调通知本脚本，保证“未命中得狂暴”与 miss 跳字严格同步。
/// 狂暴（RageBuff）：攻速/移速提升、紫色呼吸光效，层独立计时、可无限叠加。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class CatDad : BehaviourBase, IMissHandler
{
    [Header("未命中率")]
    [SerializeField, Range(0f, 1f)]
    private float baseMissChance = 0.5f;    // 基础未命中率（50%）。

    [Header("未命中奖励")]
    [SerializeField, Min(0.1f)]
    private float rageDuration = 4f;        // 每次未命中获得的狂暴持续秒（4 秒）。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private RageBuff _rage;

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>未命中奖励由伤害结算回调驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
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

    /// <summary>
    /// 伤害结算判定本次攻击未命中：获得一层狂暴。
    /// </summary>
    public void OnAttackMissed()
    {
        if (_prop == null || _prop.isDead)
            return;

        if (_health != null)
            _health.ApplyBuff(_rage);
    }
}
