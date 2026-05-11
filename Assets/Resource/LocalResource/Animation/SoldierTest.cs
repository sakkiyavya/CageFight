using UnityEngine;
using System.Collections;

public class SoldierTest : MonoBehaviour
{
    [Header("=== 基本属性 ===")]
    public float health = 100f;
    public float maxHealth = 100f;
    public float attackDamage = 10f;
    public float attackSpeed = 1f; // 每秒攻击次数
    public float engineeringDamage = 5f;
    public float pushForce = 5f;
    public float moveSpeed = 3f;
    public float mass = 1f;
    public float detectionRange = 5f;
    public float attackRange = 1.5f;

    [Header("=== 阵营设置 ===")]
    public bool isEnemy = false; // √ 敌方
    public bool isRanged = false; // √ 远程

    [Header("=== 动画设置 ===")]
    [Tooltip("拖拽Animator组件到这里")]
    public Animator animator;
    [Tooltip("移动动画参数名")]
    public string moveParam = "IsMoving";
    [Tooltip("攻击动画参数名")]
    public string attackParam = "Attack";
    [Tooltip("死亡动画参数名")]
    public string deathParam = "Die";

    [Header("=== 物理设置 ===")]
    public float drag = 10f; // 增大阻力，防止滑动

    [Header("=== 受击反馈 ===")]
    public Color hitColor = Color.red;
    public float hitFlashTime = 0.1f;
    public float hitSquash = 0.3f;

    [Header("=== 死亡效果 ===")]
    public float deathDarkness = 0.5f;
    public float deathAlpha = 0.2f;
    public float deathJumpForce = 3f;
    public float deathFallSpeed = 5f;
    public float deathDestroyTime = 2f;

    [Header("=== 状态（调试用）===")]
    [SerializeField] private bool isDead = false;
    [SerializeField] private bool isAttacking = false;
    [SerializeField] private float attackCooldown = 0f;
    [SerializeField] private GameObject currentTarget = null;
    [SerializeField] private bool facingRight = true;
    [SerializeField] private Vector2 moveDirection = Vector2.right;

    [Header("=== 组件 ===")]
    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private Collider2D col;
    private Color originalColor;
    private Vector3 originalScale;
    private float originalLocalScaleX; // 用于转向

    void Awake()
    {
        // 获取或添加组件
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        spriteRenderer = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();

        if (animator == null) animator = GetComponent<Animator>();
    }

    void Start()
    {
        // 初始化组件
        InitializeComponents();

        // 保存原始值
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        originalScale = transform.localScale;
        originalLocalScaleX = Mathf.Abs(transform.localScale.x);

        Debug.Log($"{gameObject.name} 初始化完成 | 阵营: {(isEnemy ? "敌方" : "我方")} | 生命: {health}/{maxHealth}");
    }

    void InitializeComponents()
    {
        // 设置刚体
        rb.gravityScale = 0f; // 上帝视角，无重力
        rb.drag = drag; // 阻力，防止滑动
        rb.freezeRotation = true; // 锁定旋转
        rb.mass = mass;

        // 动画参数检查
        if (animator != null)
        {
            Debug.Log($"{gameObject.name} 动画参数检查:");
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                Debug.Log($"  - {param.name} ({param.type})");
            }
        }
        else
        {
            Debug.LogWarning($"{gameObject.name} 没有Animator组件！");
        }
    }

    void Update()
    {
        if (isDead) return;

        // 更新攻击冷却
        if (attackCooldown > 0)
        {
            attackCooldown -= Time.deltaTime;
        }

        // 寻找目标
        FindTarget();

        // 行为决策
        if (currentTarget != null && IsInAttackRange() && !isAttacking)
        {
            // 在攻击范围内，停止移动并转向目标
            StopMoving();
            LookAtTarget();

            // 检查是否可以攻击
            if (attackCooldown <= 0)
            {
                StartAttack();
            }
        }
        else if (!isAttacking) // 攻击时不能移动
        {
            Move();
        }

        // 更新动画
        UpdateAnimation();
    }

    // 寻找目标
    void FindTarget()
    {
        // 如果当前目标有效，保持
        if (currentTarget != null)
        {
            SoldierTest target = currentTarget.GetComponent<SoldierTest>();
            if (target != null && !target.isDead && target.isEnemy != isEnemy)
            {
                return; // 目标有效
            }
            currentTarget = null; // 目标无效
        }

        // 寻找新目标
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRange);
        float closestDistance = Mathf.Infinity;
        GameObject closestTarget = null;

        foreach (Collider2D hit in hits)
        {
            SoldierTest soldier = hit.GetComponent<SoldierTest>();
            if (soldier != null && !soldier.isDead && soldier.isEnemy != isEnemy)
            {
                float distance = Vector2.Distance(transform.position, hit.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = hit.gameObject;
                }
            }
        }

        if (closestTarget != null)
        {
            currentTarget = closestTarget;
            Debug.Log($"{gameObject.name} 发现目标: {currentTarget.name}");
        }
    }

    // 移动
    void Move()
    {
        if (isAttacking) return; // 攻击时不能移动

        Vector2 moveVector = Vector2.zero;

        if (currentTarget != null)
        {
            // 向目标移动
            Vector2 toTarget = (currentTarget.transform.position - transform.position);
            moveVector = toTarget.normalized * moveSpeed;

            // 转向
            if (toTarget.x > 0.1f && !facingRight)
            {
                Flip();
            }
            else if (toTarget.x < -0.1f && facingRight)
            {
                Flip();
            }
        }
        else
        {
            // 默认向右移动
            moveVector = facingRight ? Vector2.right * moveSpeed : Vector2.left * moveSpeed;
        }

        // 应用移动
        rb.velocity = moveVector;
    }

    // 停止移动
    void StopMoving()
    {
        rb.velocity = Vector2.zero;
    }

    // 面向目标
    void LookAtTarget()
    {
        if (currentTarget == null) return;

        Vector2 direction = currentTarget.transform.position - transform.position;

        // 转向
        if (direction.x > 0 && !facingRight)
        {
            Flip();
        }
        else if (direction.x < 0 && facingRight)
        {
            Flip();
        }
    }

    // 转向
    void Flip()
    {
        facingRight = !facingRight;
        Vector3 scale = transform.localScale;
        scale.x = originalLocalScaleX * (facingRight ? 1 : -1);
        transform.localScale = scale;

        Debug.Log($"{gameObject.name} 转向: {(facingRight ? "右" : "左")}");
    }

    // 检查是否在攻击范围内
    bool IsInAttackRange()
    {
        if (currentTarget == null) return false;

        float distance = Vector2.Distance(transform.position, currentTarget.transform.position);
        return distance <= attackRange;
    }

    // 开始攻击
    void StartAttack()
    {
        isAttacking = true;
        attackCooldown = 1f / attackSpeed; // 设置攻击冷却

        // 播放攻击动画
        if (animator != null)
        {
            animator.SetTrigger(attackParam);
            Debug.Log($"{gameObject.name} 触发攻击动画: {attackParam}");
        }

        // 攻击持续时间内不能移动
        float attackDuration = 1f / attackSpeed;
        StartCoroutine(EndAttackAfterDelay(attackDuration));
    }

    // 攻击结束
    IEnumerator EndAttackAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        isAttacking = false;
    }

    // 动画事件：攻击命中
    public void OnAttackHit()
    {
        if (isDead || currentTarget == null) return;

        Debug.Log($"{gameObject.name} 攻击命中 {currentTarget.name}");

        SoldierTest target = currentTarget.GetComponent<SoldierTest>();
        if (target != null)
        {
            // 计算击退
            float pushDistance = pushForce / target.mass;
            Vector2 pushDirection = (target.transform.position - transform.position).normalized;

            // 造成伤害
            target.TakeDamage(attackDamage, pushDirection, pushDistance, engineeringDamage);
        }
    }

    // 受到伤害
    public void TakeDamage(float damage, Vector2 pushDirection, float pushDistance, float engineeringDmg = 0)
    {
        if (isDead) return;

        health -= damage;
        Debug.Log($"{gameObject.name} 受到 {damage} 伤害，剩余 {health}");

        // 受击反馈
        StartCoroutine(HitEffect());

        // 应用击退
        if (pushDistance > 0)
        {
            // 重置速度
            rb.velocity = Vector2.zero;

            // 计算击退力
            Vector2 force = pushDirection * pushDistance * 100f; // 乘以系数增强效果

            // 使用冲量
            rb.AddForce(force, ForceMode2D.Impulse);

            Debug.Log($"击退应用: 方向{pushDirection}, 距离{pushDistance}, 力{force}");
        }

        if (health <= 0)
        {
            Die();
        }
    }

    // 受击效果
    IEnumerator HitEffect()
    {
        if (spriteRenderer == null) yield break;

        // 变红
        Color original = spriteRenderer.color;
        spriteRenderer.color = hitColor;

        // 变扁
        Vector3 squashedScale = originalScale;
        squashedScale.y *= (1f - hitSquash);
        squashedScale.x *= (1f + hitSquash * 0.5f);
        transform.localScale = squashedScale;

        // 等待
        yield return new WaitForSeconds(hitFlashTime);

        // 恢复
        spriteRenderer.color = original;
        transform.localScale = originalScale;
    }

    // 死亡
    void Die()
    {
        if (isDead) return;

        isDead = true;
        Debug.Log($"{gameObject.name} 死亡");

        // 立即停止所有行为
        StopAllCoroutines();
        StopMoving();
        rb.velocity = Vector2.zero;

        // 变暗透明
        if (spriteRenderer != null)
        {
            Color deathColor = originalColor * deathDarkness;
            deathColor.a = deathAlpha;
            spriteRenderer.color = deathColor;
        }

        // 禁用组件
        if (col != null) col.enabled = false;
        if (animator != null) animator.enabled = false;

        // 启用重力
        rb.gravityScale = 1f;

        // 死亡效果
        StartCoroutine(DeathEffect());
    }

    // 死亡效果
    IEnumerator DeathEffect()
    {
        // 向上跳一下
        rb.AddForce(Vector2.up * deathJumpForce, ForceMode2D.Impulse);
        yield return new WaitForSeconds(0.2f);

        // 弧形掉落
        float timer = 0f;
        Vector3 startPos = transform.position;
        Vector3 randomSide = Random.value > 0.5f ? Vector3.right : Vector3.left;

        while (timer < deathDestroyTime)
        {
            timer += Time.deltaTime;
            float t = timer / deathDestroyTime;

            // 向下掉落
            rb.AddForce(Vector2.down * deathFallSpeed, ForceMode2D.Force);

            // 添加水平偏移制造弧形
            rb.AddForce(randomSide * deathFallSpeed * 0.5f, ForceMode2D.Force);

            // 旋转
            transform.Rotate(0, 0, 180f * Time.deltaTime);

            yield return null;
        }

        // 销毁
        Destroy(gameObject);
    }

    // 更新动画
    void UpdateAnimation()
    {
        if (animator == null) return;

        // 移动动画
        bool isMoving = rb.velocity.magnitude > 0.1f && !isAttacking;
        animator.SetBool(moveParam, isMoving);
    }

    // 调试功能
    [ContextMenu("测试攻击")]
    public void TestAttack()
    {
        if (isDead || isAttacking) return;
        StartAttack();
    }

    [ContextMenu("测试受伤")]
    public void TestTakeDamage()
    {
        if (isDead) return;
        TakeDamage(20f, Vector2.left, 2f);
    }

    [ContextMenu("测试死亡")]
    public void TestDie()
    {
        if (isDead) return;
        health = 0;
        Die();
    }

    [ContextMenu("强制寻找目标")]
    public void ForceFindTarget()
    {
        FindTarget();
        Debug.Log($"当前目标: {currentTarget?.name ?? "无"}");
    }

    [ContextMenu("转向测试")]
    public void TestFlip()
    {
        Flip();
    }

    // 绘制调试范围
    void OnDrawGizmosSelected()
    {
        // 索敌范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 攻击范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 朝向
        Gizmos.color = facingRight ? Color.green : Color.blue;
        Vector3 direction = facingRight ? Vector3.right : Vector3.left;
        Gizmos.DrawRay(transform.position, direction * 1f);
    }
}