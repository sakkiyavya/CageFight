using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DamageType
{
    normal = 0,
    magic = 1,
}

/// <summary>
/// 未命中反馈接口：攻击者的攻击在伤害结算中被判定未命中（目盲等）时回调，
/// 供攻击者获得反馈（如 Cat dad 每次未命中获得一层狂暴）。
/// </summary>
public interface IMissHandler
{
    void OnAttackMissed();
}
[Serializable]
public struct Damage
{
    public int side;                   // 发起伤害的阵营编号，用于友军和敌军判定。
    public int initialDamage;          // 进入防御、抗性等结算前的基础伤害值。

    public int finalDamage;            // 完成全部伤害修正后实际扣除的生命值。
    public bool missed;                // 本次伤害是否因未命中（目盲）而落空。
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
    /// 应用来源增伤倍率（damageMultiplier）、目标受伤倍率（damageTakenMultiplier），
    /// 并按来源暴击概率（critChance）判定是否造成 200% 伤害。
    /// </summary>
    /// <param name="sourceDamage">伤害源提供的原始伤害数据。</param>
    /// <returns>已经写入最终伤害值的计算结果副本。</returns>
    public static Damage DamageCompute(Damage sourceDamage)
    {
        f = sourceDamage;
        f.finalDamage = f.initialDamage;
        f.missed = false;

        GameObjectProperty sourceProp = f.source != null
            ? f.source.GetComponent<GameObjectProperty>()
            : null;
        GameObjectProperty targetProp = f.target != null
            ? f.target.GetComponent<GameObjectProperty>()
            : null;

        // 目盲未命中：攻击者持有未命中率（missChance）时掷骰，命中则本次伤害与击退全部落空。
        if (sourceProp != null && sourceProp.missChance > 0f &&
            UnityEngine.Random.value < sourceProp.missChance)
        {
            f.finalDamage = 0;
            f.repel = 0f;
            f.missed = true;

            // 未命中反馈：攻击者实现 IMissHandler 时在此回调（如 Cat dad 未命中得狂暴）。
            IMissHandler missHandler = f.source != null
                ? f.source.GetComponent<IMissHandler>()
                : null;
            if (missHandler != null)
                missHandler.OnAttackMissed();

            return f;
        }

        float multiplier = 1f;
        if (sourceProp != null)
            multiplier *= sourceProp.damageMultiplier;
        if (targetProp != null)
            multiplier *= targetProp.damageTakenMultiplier;

        if (sourceProp != null && sourceProp.critChance > 0f &&
            UnityEngine.Random.value < sourceProp.critChance)
        {
            multiplier *= 2f;
        }

        f.finalDamage = Mathf.Max(0, Mathf.RoundToInt(f.initialDamage * multiplier));

        // 目标免伤（护甲 Buff 提供）：在全部乘算修正后直接扣除，最低为 0。
        if (targetProp != null && targetProp.armor > 0)
            f.finalDamage = Mathf.Max(0, f.finalDamage - targetProp.armor);

        // 目标最终伤害减免（勇气 Buff 提供）：在结算最后按比例乘算，最低为 0。
        if (targetProp != null && targetProp.damageReduction > 0f)
            f.finalDamage = Mathf.Max(0, Mathf.RoundToInt(f.finalDamage * (1f - targetProp.damageReduction)));

        // 坚毅格挡：目标还有格挡次数时，本次伤害变为 1 点（层消耗与击退免疫由 CharacterHealth 处理）。
        if (targetProp != null && targetProp.blockHits > 0)
            f.finalDamage = 1;

        return f;
    }
    #endregion
}


