using UnityEngine;

[RequireComponent(typeof(DamageSource))]
public class ProjectileDirector : MonoBehaviour
{
    [Min(0.1f)]
    public float speed = 1.0f;

    private DamageSource damageSource;
    private Vector3 moveDirection = Vector3.right;
    private bool hasSetDirection;

    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
    }

    private void OnEnable()
    {
        hasSetDirection = false;
        moveDirection = transform.right;
    }

    private void Update()
    {
        Move();
    }

    private void Move()
    {
        if (damageSource == null)
            return;

        if (!hasSetDirection)
        {
            if (damageSource.target != null)
            {
                Vector3 direction =
                    damageSource.target.transform.position - transform.position;

                if (direction.sqrMagnitude > 0.001f)
                    moveDirection = direction.normalized;
            }
            else
            {
                // 无追踪目标（如散弹）时，使用发射方在对象池 Get 之后设置的当前朝向，
                // 而不是 OnEnable 捕获的陈旧方向（避免覆盖射手设置的弹道方向）。
                moveDirection = transform.right;
            }

            hasSetDirection = true;
            transform.right = moveDirection;
        }

        transform.position += moveDirection * speed * Time.deltaTime;
    }
}