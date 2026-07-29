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
    public GameObjectType objectType;               // 对象所属类别，可组合标记为建筑或角色。
    public int maxHp;                               // 对象可拥有的最大生命值。
    public int defense;                             // 抵扣普通伤害时使用的物理防御值。
    public int magicDefense;                        // 抵扣魔法伤害时使用的魔法防御值。
    public int atk;                                 // 造成普通伤害时使用的基础攻击力。
    public float atkRate;                           // 两次攻击之间的时间间隔，单位为秒。
    public int magicAtk;                            // 造成魔法伤害时使用的基础攻击力。
    public Vector2Int atkRange = Vector2Int.one;    // 以网格宽、高表示的攻击判定范围。
    public bool isRemoteAtk;                        // 是否通过投射物执行远程攻击。
    public bool isFacingLeft = true;                // 初始朝向是否为左侧。
    public Vector2Int occupySpace;                  // 对象在地图网格中占用的宽度和高度。
    public float barSustainTime;                    // 受伤后血条继续显示的时间，单位为秒。
    public float buildTime;                         // 建筑对象完成施工所需的秒数。
    public float moveSpeed;                         // 角色对象沿路径移动时采用的速度。
    public float repel;                             // 攻击命中后施加给目标的击退强度。
    public int side;                                // 阵营编号，用于区分友方和敌方对象。
    [ResourceKey(typeof(GameObject))]
    public string atkObj;                           // 远程攻击时生成的投射物预制体资源键。

    [ResourceKey(typeof(GameObject))]
    public string buildAnime;                       // 建筑施工过程中显示的特效预制体资源键。
}
