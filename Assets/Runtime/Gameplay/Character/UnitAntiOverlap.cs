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

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Discrete;
        rb.interpolation = RigidbodyInterpolation2D.None;
    }
}