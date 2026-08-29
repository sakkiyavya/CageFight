using System;
using UnityEngine;

/// <summary>
/// BOSS 专属被动（BabaDoctor Y-7）：每次受到 Debuff 时将其“转化”为坚毅 ——
/// 拦截该 Debuff（原状态不再施加），改为施加一层持续 resoluteDuration 秒的坚毅（ResoluteBuff）。
/// 经 CharacterHealth.RegisterBuffFilter 状态扩展点登记（返回 true 表示已接管），
/// OnDisable 对称注销；坚毅实例由预制体拖入（RemoteResource/Buff/ResoluteBuff），
/// 施加走 CharacterHealth.ApplyBuff 统一入口，与铁壁卫兵（IronWallGuard）同做法。
/// 该被动为 BOSS 专属配置，保持独立组件，不并入可复用的 BossAttackBehaviour。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public sealed class BossDebuffToResolutePassive : MonoBehaviour
{
    [SerializeField, Tooltip("坚毅 Buff 预制体实例（RemoteResource/Buff/ResoluteBuff）")]
    private ResoluteBuff resoluteBuff;
    [SerializeField, Min(0f), Tooltip("转化后坚毅每层持续秒")]
    private float resoluteDuration = 3f;

    private CharacterHealth _health;

    private void Awake()
    {
        _health = GetComponent<CharacterHealth>();
    }

    private void OnEnable()
    {
        // 按配置写入坚毅层持续时长（转化后每层 3 秒）。
        if (resoluteBuff != null)
            resoluteBuff.SetDuration(resoluteDuration);

        // 登记状态过滤器（免疫/转化扩展点）：重复登记由框架按委托去重。
        if (_health != null)
            _health.RegisterBuffFilter(HandleIncomingBuff);
    }

    private void OnDisable()
    {
        // 对称注销状态过滤器（池化复用不残留监听）。
        if (_health != null)
            _health.UnregisterBuffFilter(HandleIncomingBuff);
    }

    /// <summary>
    /// 状态过滤器：Debuff 到达时接管（返回 true，原状态不再施加），改为施加一层坚毅；
    /// 非 Debuff 放行（返回 false 由框架正常施加）。
    /// </summary>
    /// <param name="buff">本次碰撞即将施加的状态。</param>
    /// <returns>是否已接管该状态（true = 已转化，不再施加原状态）。</returns>
    private bool HandleIncomingBuff(BuffBase buff)
    {
        if (buff == null || !buff.isDeBuff)
            return false;

        if (_health != null && resoluteBuff != null)
            _health.ApplyBuff(resoluteBuff);

        return true;
    }
}
