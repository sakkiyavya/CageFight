using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 触发类buff实例，此buff为攻击时回血buff
/// </summary>
public class AttackHealBuff : BuffBase
{
    public override bool isDeBuff => false;          // 增益Buff
    public override float buffSustainTime => 10f;    // 持续时间10s
    int value = 10;                                  // 回血值
    CharacterHealth charHeak;                        // 当前 Buff 目标的生命组件。
    #region 公开接口
    /// <summary>
    /// 获取目标的生命组件，并订阅其攻击事件，使目标每次攻击时恢复固定生命值。
    /// </summary>
    /// <param name="prop">需要获得攻击回血效果的目标属性组件。</param>
    /// <returns>目标存在生命组件并成功订阅攻击事件时返回 <see langword="true"/>。</returns>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        charHeak = prop.gameObject.GetComponent<CharacterHealth>();
        if(!charHeak)
            return false;

        prop.OnAtt += OnAttack;
        return true;
    }


    /// <summary>
    /// 解除目标攻击事件上的回血回调，停止后续攻击治疗。
    /// </summary>
    /// <param name="prop">需要移除攻击回血效果的目标属性组件。</param>
    /// <returns>解除事件订阅后返回 <see langword="true"/>。</returns>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        prop.OnAtt -= OnAttack;
        return true;
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 响应目标攻击事件，通过缓存的生命组件恢复配置的生命值。
    /// </summary>
    public void OnAttack()
    {
        charHeak.Heal(value);
    }
    #endregion
}
