using System;
using UnityEngine;

[Serializable]
public class BuildingBaseData : ComponentData
{
    public Vector2Int occupySpace;    // 建筑在地图网格中占用的宽度和高度。
    public float buildTime;           // 建筑从放置到施工完成所需的秒数。
}

[Serializable]
public class BuildingHealthData : ComponentData
{
    public float barSustainTime;      // 建筑受伤后血条继续显示的时间，单位为秒。
    public int defen;                 // 抵扣普通伤害时使用的物理防御值。
    public int magicDefen;            // 抵扣魔法伤害时使用的魔法防御值。
    public int maxHp;                 // 建筑可拥有的最大生命值。
}

[Serializable]
public class BuildingAIData : ComponentData
{
    // 预留给建筑 AI 的序列化参数（如攻击间隔、索敌范围等）
}
