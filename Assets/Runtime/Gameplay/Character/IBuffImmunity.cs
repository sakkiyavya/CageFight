using UnityEngine;

/// <summary>
/// Buff 免疫接口：目标收到指定类型的 Buff 时直接忽略，不施加也不生效
/// （例如 Crystallized giant beast 免疫“妄业之力”）。
/// CharacterHealth.OnCollide 施加任意 Buff 前会询问目标是否实现本接口。
/// </summary>
public interface IBuffImmunity
{
    /// <summary>
    /// 判断是否免疫指定 Buff。
    /// </summary>
    /// <param name="buff">即将施加的 Buff 实例。</param>
    /// <returns>免疫时返回 <see langword="true"/>（该 Buff 不会施加）。</returns>
    bool IsImmuneTo(BuffBase buff);
}
