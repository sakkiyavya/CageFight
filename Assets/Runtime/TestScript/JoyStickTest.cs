using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyStickTest : MonoBehaviour
{
    #region 生命周期与回调
    /// <summary>
    /// Unity 初始化回调；保留了通过事件订阅摇杆方向的备选测试方式。
    /// </summary>
    void Start()
    {
        // JoyStick.Instance.OnJoystickMove += Move;
    }

    /// <summary>
    /// 每帧读取摇杆当前方向，并将其用于移动测试对象。
    /// </summary>
    void Update()
    {
        Move(JoyStick.Instance.InputDir);
    }
    #endregion
    #region 游戏逻辑
    /// <summary>
    /// 按输入方向和帧间隔移动测试对象。
    /// </summary>
    /// <param name="dir">摇杆提供的归一化二维移动方向。</param>
    public void Move(Vector2 dir)
    {
        transform.position += new Vector3(dir.x, dir.y, 0) * Time.deltaTime;
    }
    #endregion
}
