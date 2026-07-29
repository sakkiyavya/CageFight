using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubProjectile : MonoBehaviour
{
    [ResourceKey(typeof(GameObject))]
    public string subProjectilePrefab;    // 子投射物预制体的资源键。

    #region 公开接口
    /// <summary>
    /// 触发具体子投射物定义的行为；基类仅提供可重写入口。
    /// </summary>
    public virtual void TriggleBehaviour()
    {
        
    }
    #endregion
}
