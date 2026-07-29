using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileDirector : MonoBehaviour
{
    [Min(0.1f)]public float speed = 1.0f;                                      // 投射物每秒移动速度。
    DamageSource damageSource;                                                 // 提供目标引用和伤害数据的同级组件。
    
    private Vector3 _moveDirection = Vector3.right;                            // 本次启用周期锁定的飞行方向。
    private bool _hasSetDirection = false;                                     // 是否已经根据目标计算过飞行方向。

    #region 生命周期与回调
    /// <summary>
    /// 缓存同一对象上的伤害源组件。
    /// </summary>
    void Awake()
    {
        damageSource = GetComponent<DamageSource>();
    }

    /// <summary>
    /// 对象从池中启用时重置方向锁定状态，并默认朝右飞行。
    /// </summary>
    void OnEnable()
    {
        _hasSetDirection = false;
        _moveDirection = Vector3.right;
    }

    /// <summary>
    /// 每帧推进投射物移动。
    /// </summary>
    void Update()
    {
        Move();
    }
    #endregion

    #region 游戏逻辑
    /// <summary>
    /// 首次更新时朝预设目标计算并锁定飞行方向，调整物体朝向后按速度持续直线移动。
    /// </summary>
    void Move()
    {
        if(!damageSource)
            return;

        if (!_hasSetDirection)
        {
            if (damageSource.target != null)
            {
                Vector3 targetPos = damageSource.target.transform.position;    // 目标当前世界坐标。
                Vector3 diff = targetPos - transform.position;                 // 从投射物指向目标的位移。
                if (diff.sqrMagnitude > 0.001f)
                {
                    _moveDirection = diff.normalized;
                    _hasSetDirection = true;
                }
            }
            // 调整朝向（弹幕默认朝向是右边，将 transform.right 设为飞行方向即可）
            transform.right = _moveDirection;
        }

        transform.position += _moveDirection * speed * Time.deltaTime;
    }
    #endregion
}
