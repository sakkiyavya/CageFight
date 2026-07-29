using System;
using UnityEngine;

/// <summary>
/// 角色生命值相关的数据模型。
/// </summary>
[Serializable]
public class CharacterHealthData : ComponentData
{
    public float barSustainTime;    // 角色受伤后血条继续显示的时间，单位为秒。
    public int defen;               // 抵扣普通伤害时使用的物理防御值。
    public int magicDefen;          // 抵扣魔法伤害时使用的魔法防御值。
    public int maxHp;               // 角色可拥有的最大生命值。
}

/// <summary>
/// 角色攻击属性相关的数据模型。
/// </summary>
[Serializable]
public class CharacterAtkData : ComponentData
{
    public int atk;                 // 角色造成普通伤害时使用的基础攻击力。
    public int magicAtk;            // 角色造成魔法伤害时使用的基础攻击力。
    public float atkRange;          // 角色能够命中目标的最大攻击距离。
}

/// <summary>
/// 角色 AI/移动相关的数据模型。
/// </summary>
[Serializable]
public class CharacterAIData : ComponentData
{
    public float moveSpeed;         // 角色沿路径移动时采用的速度。
}
