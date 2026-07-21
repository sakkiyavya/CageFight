using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BuffBase : MonoBehaviour
{
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
