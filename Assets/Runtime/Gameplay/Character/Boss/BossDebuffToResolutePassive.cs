using UnityEngine;

/// <summary>
/// BOSS 专属被动（BabaDoctor Y-7）：每次受到 Debuff 时将其“转化”为坚毅 ——
/// 拦截该 Debuff（原状态不再施加），改为施加一层持续 resoluteDuration 秒的坚毅（ResoluteBuff）。
/// 转化入口经 CharacterHealth.RegisterBuffFilter 状态扩展点登记（返回 true 表示已接管）。
/// 该扩展点覆盖“伤害携带”与“业务直调 ApplyBuff”两条 Debuff 施加路径（统一入口统一询问）。
/// 坚毅施加走 CharacterHealth.ApplyBuff 统一入口，OnDisable 对称注销过滤器；
/// 每层持续秒在转化时写入（ResoluteBuff.SetDuration），保证本 BOSS 的坚毅层固定为配置秒数。
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
        // 关键配置缺失给出一次性可定位告警，此后 Debuff 将正常施加（明确退化行为）。
        if (resoluteBuff == null)
            Debug.LogWarning("[BossDebuffToResolutePassive] 未配置坚毅 Buff（resoluteBuff），Debuff 将正常施加、不转化。", this);

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
    /// 非 Debuff 放行（返回 false 由框架正常施加）；无法转化时同样放行，避免吞掉状态。
    /// </summary>
    /// <param name="buff">本次即将施加的状态。</param>
    /// <returns>是否已接管该状态（true = 已转化为坚毅，不再施加原状态）。</returns>
    private bool HandleIncomingBuff(BuffBase buff)
    {
        if (buff == null || !buff.isDeBuff)
            return false;

        if (_health == null || resoluteBuff == null)
            return false;

        // 每次转化前写入每层持续秒：坚毅层按写入值计时到期（0 表示永久）。
        resoluteBuff.SetDuration(resoluteDuration);
        _health.ApplyBuff(resoluteBuff);
        return true;
    }
}
