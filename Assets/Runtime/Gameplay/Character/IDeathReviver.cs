using UnityEngine;

/// <summary>
/// 死亡复活器接口：单位生命归零时，由实现方决定是否接管死亡流程
/// （例如 Crystallized giant beast 生命归零后扣减 HP 上限并晶化复活）。
/// CharacterHealth.TakeDamage 在触发常规死亡前会询问目标是否实现本接口。
/// </summary>
public interface IDeathReviver
{
    /// <summary>
    /// 尝试接管一次致命伤害。
    /// </summary>
    /// <param name="unit">即将死亡的单位。</param>
    /// <param name="lethalDamage">导致生命归零的伤害数据。</param>
    /// <returns>返回 <see langword="true"/> 表示已接管（跳过常规死亡流程）。</returns>
    bool TryRevive(GameObject unit, Damage lethalDamage);
}
