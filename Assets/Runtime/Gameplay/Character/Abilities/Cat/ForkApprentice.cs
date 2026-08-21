using UnityEngine;

/// <summary>
/// Fork apprentice（叉子学徒）机制：
/// 1. 每秒对自身施加一层“创伤”（TraumaDebuff），自身创伤至高 maxTraumaLayers 层（默认 5）；
/// 2. 攻击时按自身当前创伤层数增伤：每层创伤 +damagePerLayer（默认 35%）伤害
///    （写入 damageMultiplier，采用“除旧乘新”方式，与愤怒等其他增伤共存）。
/// 创伤是持续直伤减益：每层每秒对自身造成一次伤害，层数越高压血越快，
/// 形成“以持续流血换取高增伤”的攻强机制。
/// 通过 Update 计时自叠创伤、OnAtt 事件接入增伤，仅新增本脚本即可生效。
/// </summary>
public class ForkApprentice : BehaviourBase
{
    [Header("自叠创伤")]
    [SerializeField, Min(0.1f)]
    private float selfApplyInterval = 1f;   // 自叠创伤间隔秒（每秒）。
    [SerializeField, Min(0.1f)]
    private float traumaDuration = 5f;      // 每层创伤持续秒（5 秒到期，稳态约 5 层）。
    [SerializeField, Min(1)]
    private int maxTraumaLayers = 5;        // 自身创伤层数上限（至高 5 层）。

    [Header("创伤增伤")]
    [SerializeField, Min(0f)]
    private float damagePerLayer = 0.35f;   // 攻击时每层创伤的增伤比例（35%）。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private TraumaDebuff _trauma;
    private float timer;
    private int _lastLayers;                // 上次攻击时参与增伤的层数（用于除旧乘新还原）。

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
        _trauma = gameObject.AddComponent<TraumaDebuff>();
        _trauma.SetDuration(traumaDuration);
    }

    private void OnEnable()
    {
        timer = 0f;
        if (_prop != null)
            _prop.OnAtt += HandleAttack;
    }

    private void OnDisable()
    {
        if (_prop != null)
        {
            _prop.OnAtt -= HandleAttack;

            // 还原残留的创伤增伤倍率，避免池化复用后污染其他系统。
            if (_prop.damageMultiplier != 0f)
                _prop.damageMultiplier /= (1f + _lastLayers * damagePerLayer);
            _lastLayers = 0;
        }
    }

    /// <summary>经 CharacterAI 调度初始化：依赖已在 Awake 缓存，此处仅兜底补齐。</summary>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null)
            _prop = prop;
        if (_health == null)
            _health = health;
    }

    /// <summary>按间隔对自身叠加一层创伤；被动不阻止后续 AI 行为。</summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (_prop == null || _prop.isDead || _health == null)
            return false;

        timer += Time.deltaTime;
        if (timer < selfApplyInterval)
            return false;

        timer = 0f;

        // 至高 5 层：达到上限不再叠加。
        if (_trauma.GetLayerCount(_prop) >= maxTraumaLayers)
            return false;

        // 统一状态入口施加并登记。
        _health.ApplyBuff(_trauma);
        return false;
    }

    /// <summary>
    /// 攻击时：按自身当前创伤层数设置增伤（除旧乘新，与其他增伤共存）。
    /// </summary>
    private void HandleAttack()
    {
        if (_prop == null)
            return;

        int layers = Mathf.Min(_trauma.GetLayerCount(_prop), maxTraumaLayers);

        if (_prop.damageMultiplier != 0f)
            _prop.damageMultiplier /= (1f + _lastLayers * damagePerLayer);

        _lastLayers = layers;
        _prop.damageMultiplier *= (1f + _lastLayers * damagePerLayer);
    }
}
