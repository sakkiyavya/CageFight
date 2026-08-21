using UnityEngine;

/// <summary>
/// Iron Wall Guard（铁壁卫兵）：每 applyInterval 秒（默认 1.5）对自身施加一层
/// “坚毅”（ResoluteBuff）。坚毅实例由预制体提供（把 RemoteResource/Buff/ResoluteBuff
/// 预制体拖入 resoluteBuff 字段），每层持续时长、护盾贴图等全部配置在坚毅预制体上，
/// 本脚本不参与贴图/时长的运行时配置，避免配置链路漂移。
/// 通过 CharacterHealth.ApplyBuff 统一入口施加，仅新增本脚本即可生效。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
[RequireComponent(typeof(CharacterHealth))]
public class IronWallGuard : BehaviourBase
{
    [Header("坚毅施放")]
    [SerializeField, Min(0.1f)]
    private float applyInterval = 1.5f;      // 施放间隔秒（每 1.5 秒获得一层）。

    [Header("坚毅 Buff")]
    [SerializeField, Tooltip("坚毅 Buff 预制体实例（RemoteResource/Buff/ResoluteBuff）")]
    private ResoluteBuff resoluteBuff;

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private float timer;

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        timer = 0f;
    }

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>按间隔对自身施加一层坚毅；被动不阻止后续 AI 行为。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null || _prop.isDead || _health == null || resoluteBuff == null)
            return false;

        timer += Time.deltaTime;
        if (timer < applyInterval)
            return false;

        timer = 0f;
        _health.ApplyBuff(resoluteBuff);
        return false;
    }
}
