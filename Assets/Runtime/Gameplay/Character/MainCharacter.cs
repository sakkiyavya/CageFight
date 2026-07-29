using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCharacter : MonoBehaviour
{
    public float speed = 3f;                                                            // 玩家角色每秒移动速度。

    #region 生命周期与回调
    /// <summary>
    /// 每帧读取虚拟摇杆方向并驱动玩家角色移动。
    /// </summary>
    void Update()
    {
        Move(JoyStick.Instance.InputDir);
    }
    #endregion
    #region 游戏逻辑
    /// <summary>
    /// 按输入方向、移动速度和帧间隔更新角色世界坐标。
    /// </summary>
    /// <param name="dir">虚拟摇杆提供的归一化二维方向。</param>
    public void Move(Vector2 dir)
    {
        transform.position += new Vector3(dir.x, dir.y, 0) * Time.deltaTime * speed;
    }
    #endregion
}

