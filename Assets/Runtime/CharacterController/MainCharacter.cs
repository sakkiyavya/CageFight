using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCharacter : MonoBehaviour
{
    public float speed = 3f;

    // 每帧处理角色移动。
    void Update()
    {
        Move(JoyStick.Instance.InputDir);
    }
    // 按方向移动角色。
    public void Move(Vector2 dir)
    {
        transform.position += new Vector3(dir.x, dir.y, 0) * Time.deltaTime * speed;
    }
}

