using UnityEngine;

/// <summary>
/// Board friend（牌友）机制：每次攻击对命中的目标随机施加一种持续 4 秒的减益
/// ——寒冷（ColdDebuff）/ 颓废（DecadentDebuff）/ 黏黏（StickyDebuff）。
/// 连续三次施加同一种减益时，获得配置的金币奖励（默认 100）。
/// 通过订阅既有接口 GameObjectProperty.OnAtt 接入；对目标直调 ApplyBuff（与 BabaDoctorC1Script 同款先例）。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class BoardFriendAbility : MonoBehaviour
{
    [Header("减益配置")]
    [SerializeField, Min(0.1f)]
    private float debuffDuration = 4f;       // 三种减益的持续秒数。
    [Header("连续奖励")]
    [SerializeField, Min(1)]
    private int consecutiveCount = 3;        // 连续同一种减益达到的次数。
    [SerializeField, Min(0)]
    private int coinReward = 100;            // 达成时获得的金币。

    private GameObjectProperty _prop;
    private ColdDebuff _cold;                // 运行时创建的寒冷实例（仅作配置载体）。
    private DecadentDebuff _decadent;        // 运行时创建的颓废实例。
    private StickyDebuff _sticky;            // 运行时创建的黏黏实例。

    private int _lastIndex = -1;             // 上一次施加的减益序号（0=寒冷 1=颓废 2=黏黏）。
    private int _streak;                     // 当前连续同一种减益的次数。

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();

        // 运行时创建并配置减益实例，避免预制体额外挂载组件。
        _cold = gameObject.AddComponent<ColdDebuff>();
        _cold.duration = debuffDuration;
        _decadent = gameObject.AddComponent<DecadentDebuff>();
        _decadent.duration = debuffDuration;
        _sticky = gameObject.AddComponent<StickyDebuff>();
        _sticky.duration = debuffDuration;
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
    /// 响应攻击事件：随机选择一种减益施加到当前目标，并统计连续同种次数，
    /// 连续达到三次时发放金币奖励并重置计数。
    /// </summary>
    private void HandleAttacked()
    {
        if (_prop == null)
            return;

        int index = Random.Range(0, 3);
        ApplyToTarget(GetDebuff(index));

        // 连续同一种减益计数。
        if (index == _lastIndex)
            _streak++;
        else
        {
            _lastIndex = index;
            _streak = 1;
        }

        if (_streak >= consecutiveCount)
        {
            _streak = 0;
            if (Coins.Instance != null)
                Coins.Instance.GainCoins(coinReward);
        }
    }

    /// <summary>
    /// 将指定减益施加到 AI 当前锁定目标的属性组件；目标缺失或已死亡时忽略。
    /// </summary>
    private void ApplyToTarget(BuffBase debuff)
    {
        if (debuff == null || _prop.target == null)
            return;

        GameObjectProperty targetProp = _prop.target.GetComponent<GameObjectProperty>();
        if (targetProp == null || targetProp.isDead)
            return;

        debuff.ApplyBuff(targetProp);
    }

    /// <summary>
    /// 按序号返回对应的减益实例。
    /// </summary>
    private BuffBase GetDebuff(int index)
    {
        switch (index)
        {
            case 0: return _cold;
            case 1: return _decadent;
            default: return _sticky;
        }
    }
    #endregion
}
