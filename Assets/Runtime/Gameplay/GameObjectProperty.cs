using System;
using UnityEngine;

/// <summary>
/// 统一管理游戏对象（建筑、角色）的核心属性组件。
/// </summary>
public class GameObjectProperty : MonoBehaviour, ILevelComponent
{
    [Header("对象种类")]
    public GameObjectType objectType;
    public int side = 0;

    [Header("基础属性")]
    public int maxHp = 100;
    public int defense = 10;
    public int magicDefense = 5;

    [Header("攻击属性")]
    public int atk = 10;
    public int magicAtk = 5;
    public float atkRange = 1.5f;

    [Header("额外属性")]
    public float barSustainTime = 2f;
    public float buildTime = 3f;
    public float moveSpeed = 3f;

    [Header("实时信息")]
    public GameObject target;
    [Header("空间属性")]
    public Vector2Int occupySpace = Vector2Int.one;
    public GameObject buildAnime;

    #region ILevelComponent 实现

    public Type DataType => typeof(GameObjectPropertyData);

    public ComponentData ExtractData()
    {
        return new GameObjectPropertyData
        {
            objectType = this.objectType,
            side = this.side,
            maxHp = this.maxHp,
            defense = this.defense,
            magicDefense = this.magicDefense,
            atk = this.atk,
            magicAtk = this.magicAtk,
            atkRange = this.atkRange,
            occupySpace = this.occupySpace,
            moveSpeed = this.moveSpeed
        };
    }

    public void ApplyData(ComponentData data)
    {
        if (data is GameObjectPropertyData pData)
        {
            this.objectType = pData.objectType;
            this.side = pData.side;
            this.maxHp = pData.maxHp;
            this.defense = pData.defense;
            this.magicDefense = pData.magicDefense;
            this.atk = pData.atk;
            this.magicAtk = pData.magicAtk;
            this.atkRange = pData.atkRange;
            this.occupySpace = pData.occupySpace;
            this.moveSpeed = pData.moveSpeed;
        }
    }

    #endregion
}
