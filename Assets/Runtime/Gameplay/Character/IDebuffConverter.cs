using UnityEngine;

/// <summary>
/// 减益转化器接口：目标收到减益时，可由实现方把减益转化为其他效果
/// （例如 Invincible 把受到的减益转化为自身的愤怒增益）。
/// CharacterHealth.OnCollide 施加 Buff 前会询问目标是否实现本接口。
/// </summary>
public interface IDebuffConverter
{
    /// <summary>
    /// 尝试转化一个减益。
    /// </summary>
    /// <param name="debuff">即将施加到目标的减益实例。</param>
    /// <returns>返回 <see langword="true"/> 表示已接管该减益（原减益不再施加）。</returns>
    bool ConvertDebuff(BuffBase debuff);
}
