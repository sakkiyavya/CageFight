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

    public GameObject Source;
    public GameObject target;
    
}

public static class DamageComputor
{
    static Damage f = new Damage();
    public static Damage DamageCompute(Damage sourceDamage)
    {
        f = sourceDamage;
        return f;
    }
}


