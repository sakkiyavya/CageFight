using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class UnitAntiOverlap : MonoBehaviour
{
    [Range(0.05f, 0.5f)]
    public float radius = 0.2f;

    private void Awake()
    {
        CircleCollider2D col = GetComponent<CircleCollider2D>();
        col.radius = radius;
        col.isTrigger = false;

        // 运动学刚体：单位位移完全由 Move（transform 直写）驱动。
        // 原动态刚体会让“出生在兵营内部/叠在一起的单位”被物理推出、卡在建筑碰撞体上，
        // 以及人群互相推挤锁死（“生产了却不作为”的根因之一）；
        // 运动学刚体不参与物理推挤，投射物触发器命中判定不受影响。
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        rb.interpolation = RigidbodyInterpolation2D.None;
    }
}