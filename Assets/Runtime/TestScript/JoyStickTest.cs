using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyStickTest : MonoBehaviour
{
    // 初始化测试逻辑。
    void Start()
    {
        // JoyStick.Instance.OnJoystickMove += Move;
    }

    // 每帧读取摇杆输入。
    void Update()
    {
        Move(JoyStick.Instance.InputDir);
    }
    // 按输入方向移动。
    public void Move(Vector2 dir)
    {
        transform.position += new Vector3(dir.x, dir.y, 0) * Time.deltaTime;
    }
}
