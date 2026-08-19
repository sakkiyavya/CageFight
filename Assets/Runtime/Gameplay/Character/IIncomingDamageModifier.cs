using UnityEngine;

/// <summary>
/// 入伤修正接口：在 CharacterHealth 正式扣血前修正本次已结算伤害
/// （如护盾吸收、无敌豁免），返回修正后的最终伤害。
/// 伤害计算、暴击、防御、未命中在同一结算链路中只执行一次；
/// 实现方只修改伤害值并内部维护吸收量与状态刷新，不得通过预先回血抵消伤害。
/// </summary>
public interface IIncomingDamageModifier
{
    /// <summary>
    /// 修正本次伤害。
    /// </summary>
    /// <param name="damage">已完成全部伤害计算的伤害数据（finalDamage 为实际伤害）。</param>
    /// <returns>修正后实际扣除的生命值。</returns>
    int ModifyIncomingDamage(Damage damage);
}
