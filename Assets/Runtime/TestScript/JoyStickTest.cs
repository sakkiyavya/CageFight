using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoyStickTest : MonoBehaviour
{
    void Start()
    {
        // JoyStick.Instance.OnJoystickMove += Move;
    }

    void Update()
    {
        Move(JoyStick.Instance.InputDir);
    }
    public void Move(Vector2 dir)
    {
        transform.position += new Vector3(dir.x, dir.y, 0) * Time.deltaTime;
    }
}
