using UnityEngine;

[RequireComponent(typeof(DamageSource))]
public class ProjectileDirector : MonoBehaviour
{
    [Min(0.1f)]
    public float speed = 1.0f;

    private DamageSource damageSource;
    private Vector3 moveDirection = Vector3.right;
    private float _lastTargetDist = float.MaxValue;   // 上一帧与目标的距离（判断是否仍在接近目标）。
    private int _stuckFrames;                         // 已经贴在目标位置却未触发命中判定的连续帧数。

    private void Awake()
    {
        damageSource = GetComponent<DamageSource>();
    }

    /// <summary>池化启用：先沿用发射方设置的朝向（对象池 Get 之后会写入），有目标后每帧改向追踪。</summary>
    private void OnEnable()
    {
        moveDirection = transform.right;
        _lastTargetDist = float.MaxValue;
        _stuckFrames = 0;
    }

    private void Update()
    {
        if (damageSource == null)
            return;

        // 目标存活时持续追踪：目标会移动，原实现只在首帧定一次方向，
        // 慢速弹体追不上移动中的目标（互相空 A 根因之一）。
        GameObject target = damageSource.target;
        if (target != null)
        {
            GameObjectProperty targetProp = target.GetComponent<GameObjectProperty>();
            if (targetProp != null && (targetProp.isDead || targetProp.isUntargetable))
            {
                target = null;
                damageSource.target = null;
            }
        }

        if (target != null)
        {
            Vector3 toTarget = target.transform.position - transform.position;
            float dist = toTarget.magnitude;

            if (dist > 0.0001f)
            {
                moveDirection = toTarget / dist;
                transform.right = moveDirection;
            }

            // 续命规则：只有“连续两帧在接近目标”（追得上）时才按剩余航程续命。
            // 目标跑得比弹体快（追不上）时不续命，弹体按自身 sustainTime 到期消失——
            // 否则慢弹体会永远追在目标脚后跟、并随追踪逐帧旋转（贴脚旋转的根因）。
            if (_lastTargetDist < float.MaxValue && dist < _lastTargetDist)
                damageSource.EnsureLife(dist / Mathf.Max(0.1f, speed) + 0.4f);
            _lastTargetDist = dist;

            // 逐步逼近目标（不超冲）：到达目标位置即停下进入命中判定，不再绕目标振荡。
            float step = speed * Time.deltaTime;
            if (dist <= step)
            {
                // 已贴在目标上：若命中判定迟迟未触发（目标无碰撞体/极小碰撞体等），
                // 连续 0.25 秒后主动回收——否则弹体会贴着目标无限飘荡、永不消失。
                transform.position = target.transform.position;
                _stuckFrames++;
                if (_stuckFrames > 15)
                {
                    damageSource.ReleaseNow();
                }
                return;
            }

            _stuckFrames = 0;
            transform.position += moveDirection * step;
            return;
        }

        // 无目标（目标死亡/散弹）：直线飞行，并恢复距离测量初值。
        _lastTargetDist = float.MaxValue;
        transform.position += moveDirection * speed * Time.deltaTime;
    }
}
