using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICollide
{
    // 处理碰撞回调。
    void OnCollide(Collider2D other);
}
