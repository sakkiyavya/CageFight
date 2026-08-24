using UnityEngine;

/// <summary>
/// General Cat（猫将军）：每次受击（CharacterHealth.TakeDamage 在正式扣血前发布
/// GameObjectProperty.OnHitted）获得一层“勇气”。每层持续 7 秒、至多 10 层，
/// 持续时长与层数上限全部配置在勇气 Buff 预制体（RemoteResource/Buff/CourageBuff）上，
/// 本脚本不做任何运行时数值配置，避免配置链路漂移。
/// 勇气（CourageBuff）：每层提供 3% 最终伤害减免（局外防御魔法等级每级额外 +0.3%），
/// 各层独立计时到期，总减免上限 100%；叠加期间目标呈淡黄色呼吸光效。
/// 勇气实例由预制体提供（把 RemoteResource/Buff/CourageBuff 预制体拖入 courageBuff 字段），
/// 通过 CharacterHealth.ApplyBuff 统一入口施加，无运行时 AddComponent。
/// 原“受弹幕命中获得护甲（ArmorBuff）”机制已移除。
/// 通过 GameObjectProperty 的 OnHitted 事件挂接，不侵入 AI 流程。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class GeneralCat : BehaviourBase
{
    [Header("勇气 Buff")]
    [SerializeField, Tooltip("勇气 Buff 预制体实例（RemoteResource/Buff/CourageBuff）")]
    private CourageBuff courageBuff;

    private GameObjectProperty _prop;
    private CharacterHealth _health;

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>受击被动由事件驱动，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        if (_prop != null)
            _prop.OnHitted += HandleHitted;
    }

    private void OnDisable()
    {
        if (_prop != null)
            _prop.OnHitted -= HandleHitted;
    }

    /// <summary>
    /// 每次受击：经统一入口施加一层勇气；层数上限、每层时长与减免比例
    /// 全部由勇气 Buff 预制体配置，到达上限后本次叠加自动不生效。
    /// </summary>
    private void HandleHitted(Damage damage)
    {
        if (_prop == null || _prop.isDead || _health == null || courageBuff == null)
            return;

        _health.ApplyBuff(courageBuff);
    }
}
