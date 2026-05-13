using System;
using UnityEngine;

[Serializable]
[Flags]
public enum GameObjectType
{
    None = 0,
    Building = 1 << 0,
    Character = 1 << 1
}

/// <summary>
/// 统一的游戏对象属性数据模型，用于关卡序列化。
/// </summary>
[Serializable]
public class GameObjectPropertyData : ComponentData
{
    public GameObjectType objectType;
    public int maxHp;
    public int defense;
    public int magicDefense;
    public int atk;
    public int magicAtk;
    public Vector2Int atkRange = Vector2Int.one;
    public bool isFacingLeft = true;
    public Vector2Int occupySpace;
    public float barSustainTime;
    public float buildTime;
    public float moveSpeed;
    public int side;
}
