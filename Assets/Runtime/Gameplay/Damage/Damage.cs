using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DamageType
{
    normal = 0,
    magic = 1,
}
[Serializable]
public struct Damage
{
    public int initialDamage;

    public int finalDamage;
    public DamageType type;

    public GameObject source;
    public GameObject target;
    
    public static Damage DefaultDamage => new Damage
    {
        initialDamage = 10,
        finalDamage = 0,
        type = DamageType.normal,
        source = null,
        target = null
    };
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


