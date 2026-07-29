using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 手指 ID 处理类，支持全局多指绑定，确保不同系统（如摇杆和建筑逻辑）互不干扰。
/// </summary>
public class FingerIDHander
{
    // 全局静态集合，记录所有正在被占用的手指 ID
    private static HashSet<int> globalClaimedIds = new HashSet<int>();    // 已被所有绑定器占用的全局手指编号。

    private int boundFingerId = -1;                                       // 当前实例占用的手指编号，-1 表示未绑定。

    /// <summary>
    /// 是否当前实例已有手指被锁定
    /// </summary>
    public bool IsOccupied => boundFingerId != -1;                        // 当前实例是否已经绑定手指。

    /// <summary>
    /// 获取当前实例绑定的手指 ID
    /// </summary>
    public int BoundFingerId => boundFingerId;                            // 当前绑定的手指编号。

    #region 公开接口
    /// <summary>
    /// 尝试绑定一根手指。
    /// 不仅要求当前实例未绑定，还要求该手指未被其他任何 FingerIDHander 实例占用。
    /// </summary>
    /// <param name="id">准备绑定的指针或手指编号。</param>
    /// <returns>当前实例和其他绑定器均未占用该编号时返回 <see langword="true"/>。</returns>
    public bool TryBind(int id)
    {
        // 如果本实例已绑定，或者该 ID 已被全局其他模块占用，则绑定失败
        if (IsOccupied || globalClaimedIds.Contains(id)) return false;
        
        boundFingerId = id;
        globalClaimedIds.Add(id); // 声明全局占用
        return true;
    }

    /// <summary>
    /// 判断指定编号是否正由当前实例绑定。
    /// </summary>
    /// <param name="id">需要验证的指针或手指编号。</param>
    /// <returns>当前实例已绑定且编号一致时返回 <see langword="true"/>。</returns>
    public bool IsValid(int id)
    {
        return IsOccupied && boundFingerId == id;
    }

    /// <summary>
    /// 解除当前实例的手指绑定，并从全局占用集合中释放该编号。
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
    /// 在当前触摸列表中查找本实例所绑定的触摸数据。
    /// </summary>
    /// <returns>仍处于活动状态的绑定触摸；未绑定或该触摸已经消失时返回 <see langword="null"/>。</returns>
    public Touch? GetActiveTouch()
    {
        if (!IsOccupied) return null;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);                              // 当前遍历到的触摸数据。
            if (touch.fingerId == boundFingerId) return touch;
        }
        return null;
    }
    #endregion
}
