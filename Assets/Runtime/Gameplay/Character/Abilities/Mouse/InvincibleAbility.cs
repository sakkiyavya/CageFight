using UnityEngine;

/// <summary>
/// Invincible 机制：受到的减益（debuff）全部转化为自身的愤怒（AngerBuff）3 秒。
/// 经 CharacterHealth 统一状态过滤器扩展点登记（OnEnable/OnDisable 对称）：
/// 原减益不再生效，改为自身叠加一层愤怒（增伤/受伤/暴击 + 橙红呼吸）。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class InvincibleAbility : BehaviourBase
{
    [Header("转化配置")]
    [SerializeField, Min(0.1f)]
    private float angerDuration = 3f;        // 每个被转化的减益提供的愤怒持续秒数。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private AngerBuff _anger;                // 运行时创建的愤怒实例（仅作配置载体）。

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>转化由受击流程触发，无每帧行为；返回 false 放行后续 AI。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();

        // 运行时创建并配置愤怒实例，避免预制体额外挂载组件。
        _anger = gameObject.AddComponent<AngerBuff>();
        _anger.SetDuration(angerDuration);
    }

    private void OnEnable()
    {
        // 经生命框架统一扩展点登记减益转化过滤器（OnDisable 对称注销）。
        if (_health != null)
            _health.RegisterBuffFilter(ConvertDebuff);
    }

    private void OnDisable()
    {
        if (_health != null)
            _health.UnregisterBuffFilter(ConvertDebuff);
    }
    #endregion

    #region 减益转化
    /// <summary>
    /// 接管一个即将施加的减益：不再施加原减益，改为对自身叠加一层愤怒。
    /// </summary>
    /// <param name="debuff">被转化的减益实例（本实现不区分类型，统一转化为愤怒）。</param>
    /// <returns>转化成功时返回 <see langword="true"/>。</returns>
    public bool ConvertDebuff(BuffBase debuff)
    {
        if (debuff == null || !debuff.isDeBuff)
            return false;

        if (_prop == null || _anger == null || _prop.isDead)
            return false;

        _anger.ApplyBuff(_prop);
        return true;
    }
    #endregion
}
