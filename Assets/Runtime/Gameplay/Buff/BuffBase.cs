using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BuffBase : MonoBehaviour
{
    public float buffApplyTime = 0;    // Buff 成功施加时的游戏时间。
    public abstract float buffSustainTime{get;}
    public abstract bool isDeBuff{get;}

    #region Buff 生命周期
    /// <summary>
    /// 将 Buff 效果施加到目标属性组件。
    /// </summary>
    /// <param name="prop">需要应用 Buff 的目标属性组件。</param>
    /// <returns>目标满足条件且效果成功应用时返回 <see langword="true"/>。</returns>
    public abstract bool ApplyBuff(GameObjectProperty prop);
    /// <summary>
    /// 携带来源伤害数据施加 Buff，供需要读取施法者或伤害参数的 Buff 重写。
    /// 默认实现转发到无伤害版本，现有 Buff 无需任何改动即可继续工作。
    /// </summary>
    /// <param name="prop">需要应用 Buff 的目标属性组件。</param>
    /// <param name="damage">本次施加 Buff 的来源伤害数据，包含施法者对象与阵营。</param>
    /// <returns>目标满足条件且效果成功应用时返回 <see langword="true"/>。</returns>
    public virtual bool ApplyBuff(GameObjectProperty prop, Damage damage)
    {
        return ApplyBuff(prop);
    }
    /// <summary>
    /// 从目标属性组件移除此前施加的 Buff 效果。
    /// </summary>
    /// <param name="prop">需要移除 Buff 的目标属性组件。</param>
    /// <returns>效果成功撤销时返回 <see langword="true"/>。</returns>
    public abstract bool CancelBuff(GameObjectProperty prop);
    #endregion
}
