using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICollide
{
    #region 碰撞回调
    /// <summary>
    /// 判断传入伤害是否来自友方阵营，供碰撞源决定是否忽略本次命中。
    /// </summary>
    /// <param name="damage">包含来源阵营信息的伤害数据。</param>
    /// <returns>伤害来源与当前对象是否属于友方。</returns>
    bool IsFriendly(Damage damage);
    /// <summary>
    /// 处理敌方伤害碰撞，并返回经过当前对象处理后的伤害数据。
    /// </summary>
    /// <param name="damage">碰撞源携带的原始伤害数据。</param>
    /// <returns>应用防御、状态或其他规则后的伤害数据。</returns>
    Damage OnCollide(Damage damage);
    #endregion
}
