using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BuffBase : MonoBehaviour
{
    public float buffApplyTime = 0;
    public abstract float buffSustainTime{get;}
    public abstract bool isDeBuff{get;}
    /// <summary>
    /// 上buff的函数
    /// </summary>
    /// <returns></returns>
    public abstract bool ApplyBuff(GameObjectProperty prop);
    /// <summary>
    /// 取消buff的函数
    /// </summary>
    /// <returns></returns>
    public abstract bool CancelBuff(GameObjectProperty prop);
}
