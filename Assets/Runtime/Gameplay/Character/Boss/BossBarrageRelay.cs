using UnityEngine;

/// <summary>
/// BOSS 弹幕动画事件转发器：挂在 BOSS 根物体（Animator 所在物体）上。
/// Unity 动画事件只会调用 Animator 所在物体上的组件方法，而 A/B/C 攻击模组挂在子物体上，
/// 因此由本组件接收动画事件的 FireBarrage 调用，并转发给全部攻击模组
/// （模组内部的“当前动画状态 == 本模组攻击名”过滤会保证只有正在播放的那个模组发射）。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossBarrageRelay : MonoBehaviour
{
    private BossAttackBehaviour[] _modules;

    private void Awake()
    {
        _modules = GetComponentsInChildren<BossAttackBehaviour>(true);
    }

    /// <summary>动画事件调用入口：转发给当前正在播放的攻击模组。</summary>
    public void FireBarrage()
    {
        if (_modules == null)
            return;

        foreach (BossAttackBehaviour module in _modules)
        {
            if (module != null)
                module.FireBarrage();
        }
    }
}
