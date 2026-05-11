using System;
using UnityEngine;

/// <summary>
/// 角色生命值相关的数据模型。
/// </summary>
[Serializable]
public class CharacterHealthData : ComponentData
{
    public float barSustainTime;
    public int defen;
    public int magicDefen;
    public int maxHp;
}

/// <summary>
/// 角色攻击属性相关的数据模型。
/// </summary>
[Serializable]
public class CharacterAtkData : ComponentData
{
    public int atk;
    public int magicAtk;
    public float atkRange;
}

/// <summary>
/// 角色 AI/移动相关的数据模型。
/// </summary>
[Serializable]
public class CharacterAIData : ComponentData
{
    public float moveSpeed;
}
