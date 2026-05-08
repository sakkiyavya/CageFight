using UnityEngine;
using System.Collections;

/// <summary>
/// 角色死亡弹簧效果
/// 当HP <= 0时，角色会被弹飞出屏幕
/// 支持预览功能
/// </summary>
[RequireComponent(typeof(Collider2D))] // 需要2D碰撞体用于物理
public class DeathSpringEffect : MonoBehaviour
{
    [Header("死亡设置")]
    [Tooltip("当前生命值")]
    public float currentHP = 100f;

    [Tooltip("当HP小于等于0时触发死亡")]
    [SerializeField] private float deathThreshold = 0f;

    [Header("弹簧效果")]
    [Tooltip("弹射的初始速度")]
    public float launchForce = 20f;

    [Tooltip("弹射的角度（0=向右，90=向上）")]
    [Range(0f, 360f)] public float launchAngle = 45f;

    [Tooltip("旋转扭矩，让角色旋转飞出")]
    public float torqueForce = 5f;

    [Tooltip("重力缩放（死亡后）")]
    public float deathGravityScale = 2f;

    [Tooltip("死亡后多长时间销毁（秒）")]
    public float destroyDelay = 3f;

    [Header("预览设置")]
    [Tooltip("勾选后立即触发死亡效果（仅用于测试）")]
    public bool immediateTrigger = false;

    [Tooltip("死亡后的颜色")]
    public Color deathColor = new Color(0.5f, 0.5f, 0.5f, 0.7f);

    [Header("状态")]
    [Tooltip("是否已死亡")]
    public bool isDead = false;

    [Tooltip("死亡时停止的组件")]
    public MonoBehaviour[] componentsToDisable;

    // 私有变量
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Animator animator;

    [Header("调试信息")]
    [SerializeField] private Vector2 launchDirection = Vector2.zero;

    // 用于在编辑器中绘制方向预览
    private void OnDrawGizmosSelected()
    {
        if (immediateTrigger || Application.isPlaying)
        {
            // 计算弹射方向
            float angleRad = launchAngle * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

            // 在场景中绘制方向
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, transform.position + (Vector3)direction * 2f);

            // 绘制起点
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.2f);
        }
    }

    private void Start()
    {
        // 获取组件
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // 如果没有Rigidbody2D，自动添加
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f; // 初始重力为0
        }

        // 保存原始颜色
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        // 自动查找要禁用的组件
        if (componentsToDisable == null || componentsToDisable.Length == 0)
        {
            FindComponentsToDisable();
        }
    }

    private void Update()
    {
        // 检测是否需要触发死亡
        if (currentHP <= deathThreshold && !isDead)
        {
            TriggerDeathEffect();
        }

        // 立即触发预览（在编辑器中）
        if (immediateTrigger && !isDead)
        {
            TriggerDeathEffect();
            immediateTrigger = false; // 重置触发器
        }
    }

    /// <summary>
    /// 自动查找需要禁用的组件
    /// </summary>
    private void FindComponentsToDisable()
    {
        // 获取所有组件，排除不需要的
        MonoBehaviour[] allComponents = GetComponents<MonoBehaviour>();
        System.Collections.Generic.List<MonoBehaviour> toDisable = new System.Collections.Generic.List<MonoBehaviour>();

        foreach (MonoBehaviour component in allComponents)
        {
            // 不要禁用自己
            if (component == this) continue;

            // 不要禁用Transform、SpriteRenderer等
            if (component is Transform) continue;
            if (component is SpriteRenderer) continue;
            if (component is Rigidbody2D) continue;

            toDisable.Add(component);
        }

        componentsToDisable = toDisable.ToArray();
    }

    /// <summary>
    /// 应用伤害
    /// </summary>
    public void ApplyDamage(float damage)
    {
        if (isDead) return;

        currentHP -= damage;
        Debug.Log($"{gameObject.name}受到{damage}点伤害，剩余HP: {currentHP}");
    }

    /// <summary>
    /// 触发死亡效果
    /// </summary>
    [ContextMenu("触发死亡效果")]
    public void TriggerDeathEffect()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log($"{gameObject.name}死亡！开始弹簧效果");

        // 停止所有行为
        DisableComponents();

        // 停止动画
        StopAnimations();

        // 应用死亡视觉效果
        ApplyDeathVisuals();

        // 启动弹簧效果
        StartCoroutine(SpringDeathRoutine());

        // 销毁对象
        StartCoroutine(DestroyAfterDelay());
    }

    /// <summary>
    /// 弹簧效果协程
    /// </summary>
    private IEnumerator SpringDeathRoutine()
    {
        // 等待一帧，确保所有组件已禁用
        yield return null;

        // 激活物理
        rb.gravityScale = 0.5f; // 先给一点重力
        rb.angularDrag = 0.5f;

        // 第一次弹跳
        LaunchCharacter();

        // 等待短暂时间
        yield return new WaitForSeconds(0.3f);

        // 增加重力和旋转
        rb.gravityScale = deathGravityScale;

        // 如果碰到边界，再次弹跳
        yield return new WaitForSeconds(0.2f);

        // 添加随机旋转
        if (torqueForce > 0)
        {
            float randomTorque = Random.Range(-torqueForce, torqueForce);
            rb.AddTorque(randomTorque, ForceMode2D.Impulse);
        }
    }

    /// <summary>
    /// 弹射角色
    /// </summary>
    private void LaunchCharacter()
    {
        if (rb == null) return;

        // 计算弹射方向
        float angleRad = launchAngle * Mathf.Deg2Rad;
        launchDirection = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));

        // 施加弹射力
        Vector2 force = launchDirection * launchForce;
        rb.AddForce(force, ForceMode2D.Impulse);

        Debug.Log($"{gameObject.name}被弹射，方向: {launchDirection}, 力度: {launchForce}");
    }

    /// <summary>
    /// 禁用所有组件
    /// </summary>
    private void DisableComponents()
    {
        foreach (MonoBehaviour component in componentsToDisable)
        {
            if (component != null)
            {
                component.enabled = false;
                Debug.Log($"已禁用: {component.GetType().Name}");
            }
        }
    }

    /// <summary>
    /// 停止动画
    /// </summary>
    private void StopAnimations()
    {
        if (animator != null)
        {
            animator.enabled = false;
        }
    }

    /// <summary>
    /// 应用死亡视觉效果
    /// </summary>
    private void ApplyDeathVisuals()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.color = deathColor;
        }
    }

    /// <summary>
    /// 延迟销毁
    /// </summary>
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(destroyDelay);

        // 淡出效果
        float fadeDuration = 0.5f;
        float elapsedTime = 0f;

        if (spriteRenderer != null)
        {
            Color startColor = spriteRenderer.color;
            Color targetColor = new Color(startColor.r, startColor.g, startColor.b, 0f);

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / fadeDuration);
                spriteRenderer.color = Color.Lerp(startColor, targetColor, t);
                yield return null;
            }
        }

        Debug.Log($"{gameObject.name}已被销毁");
        Destroy(gameObject);
    }

    /// <summary>
    /// 立即死亡（测试用）
    /// </summary>
    [ContextMenu("立即死亡")]
    public void DieNow()
    {
        currentHP = 0;
    }

    /// <summary>
    /// 重置角色
    /// </summary>
    [ContextMenu("重置角色")]
    public void ResetCharacter()
    {
        isDead = false;

        // 重新启用组件
        foreach (MonoBehaviour component in componentsToDisable)
        {
            if (component != null)
            {
                component.enabled = true;
            }
        }

        // 恢复动画
        if (animator != null)
        {
            animator.enabled = true;
        }

        // 恢复颜色
        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        // 重置物理
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.gravityScale = 0f;
        }

        Debug.Log($"{gameObject.name}已重置");
    }
}