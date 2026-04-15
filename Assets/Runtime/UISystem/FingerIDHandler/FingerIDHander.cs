using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 手指 ID 处理类，支持全局多指绑定，确保不同系统（如摇杆和建筑逻辑）互不干扰。
/// </summary>
public class FingerIDHander
{
    // 全局静态集合，记录所有正在被占用的手指 ID
    private static HashSet<int> globalClaimedIds = new HashSet<int>();

    private int boundFingerId = -1;

    /// <summary>
    /// 是否当前实例已有手指被锁定
    /// </summary>
    public bool IsOccupied => boundFingerId != -1;

    /// <summary>
    /// 获取当前实例绑定的手指 ID
    /// </summary>
    public int BoundFingerId => boundFingerId;

    /// <summary>
    /// 尝试绑定一根手指。
    /// 不仅要求当前实例未绑定，还要求该手指未被其他任何 FingerIDHander 实例占用。
    /// </summary>
    /// <param name="id">手指 ID</param>
    /// <returns>锁定成功返回 true</returns>
    public bool TryBind(int id)
    {
        // 如果本实例已绑定，或者该 ID 已被全局其他模块占用，则绑定失败
        if (IsOccupied || globalClaimedIds.Contains(id)) return false;
        
        boundFingerId = id;
        globalClaimedIds.Add(id); // 声明全局占用
        return true;
    }

    /// <summary>
    /// 验证传入的 ID 是否是本实例锁定的那根手指
    /// </summary>
    public bool IsValid(int id)
    {
        return IsOccupied && boundFingerId == id;
    }

    /// <summary>
    /// 解除本实例的手指锁定，并释放全局占用
    /// </summary>
    public void Unbind()
    {
        if (IsOccupied)
        {
            globalClaimedIds.Remove(boundFingerId); // 释放全局占用
            boundFingerId = -1;
        }
    }

    /// <summary>
    /// 辅助方法：获取本实例当前锁定的 Touch 对象
    /// </summary>
    public Touch? GetActiveTouch()
    {
        if (!IsOccupied) return null;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            if (touch.fingerId == boundFingerId) return touch;
        }
        return null;
    }
}
