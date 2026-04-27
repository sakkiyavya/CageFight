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
