using UnityEngine;

/// <summary>
/// Mouse eyes（鼠目）机制：每次攻击对自身施加一层精准（PreciseBuff），
/// 提升攻速 5%（随攻击魔法等级成长），每层独立 10 秒，可无限叠加。
/// 通过订阅既有接口 GameObjectProperty.OnAtt 在 ShootProjectile 发射后接入。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class MouseEyesAbility : BehaviourBase
{
    [Header("自身 Buff")]
    [SerializeField, Min(0.1f)]
    private float preciseDuration = 10f;     // 每次攻击获得一层精准的持续秒数。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private PreciseBuff _precise;            // 运行时创建的精准实例（仅作配置载体，状态在目标层管理器）。

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
        _precise = gameObject.AddComponent<PreciseBuff>();
        _precise.SetDuration(preciseDuration);
    }

    private void OnEnable()
    {
        if (_prop != null)
            _prop.OnAtt += HandleAttacked;
    }

    private void OnDisable()
    {
        if (_prop != null)
            _prop.OnAtt -= HandleAttacked;
    }
    #endregion

    #region 内部实现
    /// <summary>
    /// 响应攻击事件：对自身叠加一层精准。
    /// </summary>
    private void HandleAttacked()
    {
        if (_precise != null && _health != null)
            _health.ApplyBuff(_precise);
    }
    #endregion
}
