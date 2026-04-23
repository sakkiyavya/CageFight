using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DamageType
{
    normal = 0,
    magic = 1,
}
public struct Damage
{
    public int initialDamage;

    public int finalDamage;
    public DamageType type;
}


