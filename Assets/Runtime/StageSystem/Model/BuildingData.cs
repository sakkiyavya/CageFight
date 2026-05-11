using System;
using UnityEngine;

[Serializable]
public class BuildingBaseData : ComponentData
{
    public Vector2Int occupySpace;
    public float buildTime;
}

[Serializable]
public class BuildingHealthData : ComponentData
{
    public float barSustainTime;
    public int defen;
    public int magicDefen;
    public int maxHp;
}

[Serializable]
public class BuildingAIData : ComponentData
{
    // 预留给建筑 AI 的序列化参数（如攻击间隔、索敌范围等）
}
