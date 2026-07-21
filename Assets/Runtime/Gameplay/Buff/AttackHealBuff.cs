using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// 触发类buff实例，此buff为攻击时回血buff
/// </summary>
public class AttackHealBuff : BuffBase
{
    public override bool isDeBuff => false;         //增益Buff
    public override float buffSustainTime => 10f;   //持续时间10s
    int value = 10; //回血值
    CharacterHealth charHeak;
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        charHeak = prop.gameObject.GetComponent<CharacterHealth>();
        if(!charHeak)
            return false;

        prop.OnAtt += OnAttack;
        return true;
    }


    public override bool CancelBuff(GameObjectProperty prop)
    {
        prop.OnAtt -= OnAttack;
        return true;
    }

    public void OnAttack()
    {
        charHeak.Heal(value);
    }
}
