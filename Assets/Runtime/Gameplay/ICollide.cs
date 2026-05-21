using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICollide
{
    bool IsFriendly(Damage damage);
    // 处理碰撞回调。
    Damage OnCollide(Damage damage);
}
