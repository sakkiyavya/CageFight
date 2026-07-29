using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DamageType
{
    normal = 0,
    magic = 1,
}
[Serializable]
public struct Damage
{
    public int side;                   // 发起伤害的阵营编号，用于友军和敌军判定。
    public int initialDamage;          // 进入防御、抗性等结算前的基础伤害值。

    public int finalDamage;            // 完成全部伤害修正后实际扣除的生命值。
    public int collideDir;             // 命中方向，通常以 -1 或 1 表示左右。

    public float repel;                // 命中后施加给目标的击退强度。
    public DamageType type;            // 决定采用物理防御还是魔法防御的伤害类型。

    public GameObject source;          // 产生本次伤害的游戏对象。
    public GameObject target;          // 接收本次伤害的游戏对象。
    public BuffBase[] buffs;           // 命中目标后需要一并施加的 Buff 列表。

    
    public static Damage DefaultDamage => new Damage
    {
        side = 0,
        initialDamage = 10,
        finalDamage = 0,
        collideDir = 1,
        type = DamageType.normal,
        source = null,
        target = null,
        repel = 0,
        buffs = null,
    };
}

public static class DamageComputor
{
    static Damage f = new Damage();    // 用于保存本次计算结果的临时伤害数据。
    #region 游戏逻辑
    /// <summary>
    /// 复制来源伤害，并将最终伤害初始化为基础伤害。
    /// 当前实现尚未加入防御、抗性或伤害类型修正。
    /// </summary>
    /// <param name="sourceDamage">伤害源提供的原始伤害数据。</param>
    /// <returns>已经写入最终伤害值的计算结果副本。</returns>
    public static Damage DamageCompute(Damage sourceDamage)
    {
        f = sourceDamage;
        f.finalDamage = f.initialDamage;
        return f;
    }
    #endregion
}


