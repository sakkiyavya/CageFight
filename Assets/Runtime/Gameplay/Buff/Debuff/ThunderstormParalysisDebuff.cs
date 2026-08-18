using UnityEngine;

/// <summary>
/// 雷暴猫专用麻痹：与标准麻痹（ParalysisDebuff，0.5 秒）行为一致
/// （期间无法移动/行动、图像闪烁），仅持续时长可配置（默认 3 秒），
/// 用于雷霆模式结束后（护盾被击破或时间结束）的自身麻痹。
/// </summary>
public class ThunderstormParalysisDebuff : ParalysisDebuff
{
    [SerializeField, Min(0.1f)]
    private float duration = 3f;    // 麻痹持续秒。

    public override float buffSustainTime => duration;

    /// <summary>供运行时创建/配置实例时设置持续秒。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }
}
