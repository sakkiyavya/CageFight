using UnityEngine;

/// <summary>
/// 单位控制锁（冻结/麻痹等硬控共用）：引用计数管理 CharacterAI 与 Animator 的冻结状态。
/// 多个控制来源叠加时只在首个来源记录"原始状态"，全部来源解除后才恢复，
/// 避免两个 Debuff 各自保存/恢复 AI.enabled 与 animator.speed 互相覆盖
/// （表现：冻结在寒冷层数下降后不结束、动画永远停在 0 帧）。
/// 生成方式：Buff 层管理器 AddComponent（与 22 个 Buff 状态组件同模式），
/// 不自行维护战斗数值，只统一管理控制开关。
/// </summary>
[DisallowMultipleComponent]
public class UnitFreezeLock : MonoBehaviour
{
    private int _lockCount;
    private CharacterAI _ai;
    private Animator _animator;
    private Rigidbody2D _body;
    private bool _aiWasEnabled;
    private float _originalAnimatorSpeed = 1f;

    /// <summary>当前是否有任意控制来源持有锁定。</summary>
    public bool IsLocked => _lockCount > 0;

    private void Awake()
    {
        _ai = GetComponent<CharacterAI>();
        _animator = GetComponent<Animator>();
        _body = GetComponent<Rigidbody2D>();
    }

    /// <summary>加锁：首个来源时记录原始状态并冻结 AI/动画。</summary>
    public void Lock()
    {
        if (_lockCount == 0)
        {
            if (_ai != null)
            {
                _aiWasEnabled = _ai.enabled;
                _ai.enabled = false;
            }
            if (_animator != null)
            {
                _originalAnimatorSpeed = _animator.speed;
                _animator.speed = 0f;
            }
        }

        _lockCount++;
    }

    /// <summary>解锁：全部来源解除后才恢复 AI 与动画。</summary>
    public void Unlock()
    {
        _lockCount = Mathf.Max(0, _lockCount - 1);
        if (_lockCount > 0)
            return;

        if (_ai != null && _aiWasEnabled)
            _ai.enabled = true;

        if (_animator != null)
            _animator.speed = _originalAnimatorSpeed;
    }

    private void Update()
    {
        // 任一控制锁生效期间持续压住物理速度，防止冻结单位被残余速度滑走。
        if (_lockCount > 0 && _body != null)
        {
            _body.velocity = Vector2.zero;
            _body.angularVelocity = 0f;
        }
    }

    private void OnDisable()
    {
        // 宿主回收/失效时复位，避免池化复用后残留上一任单位的锁状态。
        if (_ai != null && _aiWasEnabled && _lockCount > 0)
            _ai.enabled = true;

        if (_animator != null && _lockCount > 0)
            _animator.speed = _originalAnimatorSpeed;

        _lockCount = 0;
    }
}
