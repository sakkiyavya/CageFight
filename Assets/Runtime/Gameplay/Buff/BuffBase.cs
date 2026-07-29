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
    /// 从目标属性组件移除此前施加的 Buff 效果。
    /// </summary>
    /// <param name="prop">需要移除 Buff 的目标属性组件。</param>
    /// <returns>效果成功撤销时返回 <see langword="true"/>。</returns>
    public abstract bool CancelBuff(GameObjectProperty prop);
    #endregion
}
