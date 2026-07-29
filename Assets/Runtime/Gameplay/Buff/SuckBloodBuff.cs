using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 数值类buff实例，此buff为增加10%的吸血率
/// </summary>
public class SuckBloodBuff : BuffBase
{
    public override bool isDeBuff => false;          // 增益Buff
    public override float buffSustainTime => 10f;    // 持续时间10s
    float value = 0.1f;                              // 吸血率
    #region 公开接口
    /// <summary>
    /// 将配置的吸血比例累加到目标属性，使其后续攻击能够获得额外吸血。
    /// </summary>
    /// <param name="prop">需要增加吸血比例的目标属性组件。</param>
    /// <returns>效果应用完成后返回 <see langword="true"/>。</returns>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        prop.suckBlood += value;
        return true;
    }


    /// <summary>
    /// 从目标属性中扣除本 Buff 增加的吸血比例，恢复应用前的数值。
    /// </summary>
    /// <param name="prop">需要移除吸血加成的目标属性组件。</param>
    /// <returns>效果撤销完成后返回 <see langword="true"/>。</returns>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        prop.suckBlood -= value;
        return true;
    }
    #endregion
}
