using UnityEngine;

/// <summary>
/// Dice hand（骰子手）机制：每次攻击的伤害在 1~6 倍之间随机（骰子点数）。
/// 点数 1-2：自身获得颓废（DecadentDebuff）5 秒；点数 5-6：自身获得狂暴（RageBuff）4 秒。
/// 通过订阅既有接口 GameObjectProperty.OnAtt 在 ShootProjectile 发射后接入：
/// 以“除旧乘新”的方式把点数写入 damageMultiplier（不覆盖其他系统如愤怒的修正），
/// 弹幕命中时由 DamageComputor 结算倍率，下一次攻击前自动还原。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class DiceHandAbility : BehaviourBase
{
    [Header("骰子机制")]
    [SerializeField, Range(1, 6)]
    private int minRoll = 1;                 // 最小骰子点数。
    [SerializeField, Range(1, 6)]
    private int maxRoll = 6;                 // 最大骰子点数。

    [Header("自身 Buff")]
    [SerializeField, Min(0.1f)]
    private float decadentDuration = 5f;     // 点数 1-2 时自身颓废时长。
    [SerializeField, Min(0.1f)]
    private float rageDuration = 4f;         // 点数 5-6 时自身狂暴时长。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private DecadentDebuff _decadent;        // 运行时创建的颓废实例（仅作配置载体，状态在目标层管理器）。
    private RageBuff _rage;                  // 运行时创建的狂暴实例。
    private int _lastRoll = 1;               // 上一次骰子点数，用于还原倍率。

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>纯攻击被动：无每帧行为，返回 false 放行后续 AI 行为。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();

        // 运行时创建并配置 buff 实例，避免预制体额外挂载组件。
        _decadent = gameObject.AddComponent<DecadentDebuff>();
        _decadent.duration = decadentDuration;
        _rage = gameObject.AddComponent<RageBuff>();
        _rage.SetDuration(rageDuration);
    }

    private void OnEnable()
    {
        if (_prop != null)
            _prop.OnAtt += HandleAttacked;
    }

    private void OnDisable()
    {
        if (_prop != null)
        {
            _prop.OnAtt -= HandleAttacked;

            // 还原残留的骰子倍率，避免池化复用后污染其他系统。
            if (_prop.damageMultiplier != 0f)
                _prop.damageMultiplier /= _lastRoll;
            _lastRoll = 1;
        }
    }
    #endregion

    #region 内部实现
    /// <summary>
    /// 响应攻击事件：掷骰 1-6，将点数乘入伤害倍率（弹幕命中时结算），
    /// 并按点数对自身施加颓废（1-2）或狂暴（5-6）。
    /// </summary>
    private void HandleAttacked()
    {
        if (_prop == null)
            return;

        // 除旧乘新：先还原上一次点数，再应用本次点数，
        // 保证不覆盖其他系统（如愤怒）写入的倍率。
        _prop.damageMultiplier /= _lastRoll;
        _lastRoll = Random.Range(minRoll, maxRoll + 1);
        _prop.damageMultiplier *= _lastRoll;

        if (_lastRoll <= 2)
            _health.ApplyBuff(_decadent);
        else if (_lastRoll >= 5)
            _health.ApplyBuff(_rage);
    }
    #endregion
}
