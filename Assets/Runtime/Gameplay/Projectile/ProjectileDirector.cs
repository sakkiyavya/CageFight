using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProjectileDirector : MonoBehaviour
{
    [Min(0.1f)]public float speed = 1.0f;
    DamageSource damageSource;
    
    private Vector3 _moveDirection = Vector3.right;
    private bool _hasSetDirection = false;

    void Awake()
    {
        damageSource = GetComponent<DamageSource>();
    }

    void OnEnable()
    {
        _hasSetDirection = false;
        _moveDirection = Vector3.right;
    }

    void Update()
    {
        Move();
    }

    void Move()
    {
        if(!damageSource)
            return;

        if (!_hasSetDirection)
        {
            if (damageSource.target != null)
            {
                Vector3 targetPos = damageSource.target.transform.position;
                Vector3 diff = targetPos - transform.position;
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
}
