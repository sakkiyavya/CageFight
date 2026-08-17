using UnityEngine;

/// <summary>
/// Three hand（三只手）机制：对同一目标的连续命中第 3 次造成 3 倍伤害，
/// 并偷取目标 5% 最大生命恢复自身。
/// 通过订阅既有接口 GameObjectProperty.OnAtt 在 ShootProjectile 发射后接入：
/// 以“除旧乘新”方式把倍率写入 damageMultiplier（不覆盖其他系统如愤怒的修正），
/// 弹幕命中时由 DamageComputor 结算；偷取经 CharacterHealth.Heal 恢复自身。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class ThreeHandAbility : MonoBehaviour
{
    [Header("连击机制")]
    [SerializeField, Min(2)]
    private int comboCount = 3;              // 第几次连续命中触发（默认第三次）。
    [SerializeField, Min(1f)]
    private float comboMultiplier = 3f;      // 触发时的伤害倍率。

    [Header("偷取")]
    [SerializeField, Range(0f, 1f)]
    private float stealHpPercent = 0.05f;    // 偷取目标最大生命的比例（5%）。

    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private GameObject _lastTarget;          // 上一次攻击的目标，用于连续判定。
    private int _streak;                     // 对当前目标的连续命中计数。
    private float _lastMultiplier = 1f;      // 上一次写入的倍率，用于还原。

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
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

            // 还原残留的倍率，避免池化复用后污染其他系统。
            if (_prop.damageMultiplier != 0f)
                _prop.damageMultiplier /= _lastMultiplier;
            _lastMultiplier = 1f;
            _streak = 0;
            _lastTarget = null;
        }
    }
    #endregion

    #region 内部实现
    /// <summary>
    /// 响应攻击事件：按“是否连续命中同一目标”推进计数，
    /// 第 comboCount 次连续命中时写入组合倍率并偷取目标生命。
    /// </summary>
    private void HandleAttacked()
    {
        if (_prop == null)
            return;

        GameObject target = _prop.target;

        // 目标切换或缺失时重新计数。
        if (target == null || target != _lastTarget)
        {
            _lastTarget = target;
            _streak = 1;
        }
        else
        {
            _streak++;
        }

        bool isComboHit = _streak >= comboCount;
        if (isComboHit)
        {
            // 每 comboCount 次连续命中触发一次，触发后重新计数。
            _streak = 0;
            TryStealHp(target);
        }

        // 除旧乘新：先还原上一次倍率，再应用本次（触发时 ×comboMultiplier，否则 ×1），
        // 保证不覆盖其他系统（如愤怒）写入的倍率。
        _prop.damageMultiplier /= _lastMultiplier;
        _lastMultiplier = isComboHit ? comboMultiplier : 1f;
        _prop.damageMultiplier *= _lastMultiplier;
    }

    /// <summary>
    /// 按目标最大生命的配置比例偷取生命，用于恢复自身；目标缺失或已死亡时忽略。
    /// </summary>
    private void TryStealHp(GameObject target)
    {
        if (target == null || _health == null)
            return;

        GameObjectProperty targetProp = target.GetComponent<GameObjectProperty>();
        if (targetProp == null || targetProp.isDead)
            return;

        int steal = Mathf.Max(1, Mathf.RoundToInt(targetProp.maxHp * stealHpPercent));
        _health.Heal(steal);
    }
    #endregion
}
