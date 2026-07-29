using System.Collections;
using System.Collections.Generic;
// using Unity.Mathematics;
using UnityEngine;

public class TinyMoveScript : MonoBehaviour
{
    #region 生命周期与回调
    /// <summary>
    /// Unity 初始化回调；当前测试脚本不需要启动时设置。
    /// </summary>
    void Start()
    {
        
    }

    /// <summary>
    /// 每帧根据时间计算正弦和余弦坐标，使测试对象沿半径为 3 的圆周运动。
    /// </summary>
    void Update()
    {
        transform.position = new Vector3(3 * (float)Mathf.Cos(Time.time * 3.1415f), 3 * (float)Mathf.Sin(Time.time * 3.1415f), 0);
    }
    #endregion
}
