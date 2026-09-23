using System.Collections;
using UnityEngine;

/// <summary>
/// 关卡选择状态。
/// 关卡选择界面的 UI 模块通过 SceneStateBase.stateModules 配置和管理。
/// </summary>
public class StageSelectState : SceneStateBase
{
    [SerializeField, Tooltip("选关界面根 Canvas：进入本状态时强制激活。模块都挂在它下面，父节点被关掉会导致整个选关界面打不开。")]
    private GameObject stageSelectCanvas;

    /// <summary>
    /// 先确保选关根 Canvas 激活，再走基类流程打开 UI 模块并播放进入动画；
    /// 若先开模块再激活父节点，进入动画会在不可见状态下播完，看不到效果。
    /// </summary>
    /// <returns>等待 Canvas 激活与基类进入流程完成的协程。</returns>
    public override IEnumerator Enter()
    {
        if (stageSelectCanvas != null)
            stageSelectCanvas.SetActive(true);

        yield return base.Enter();
    }

    #region 生命周期与回调
    /// <summary>
    /// 在关卡选择 UI 已打开后执行进入回调。
    /// </summary>
    /// <returns>关卡选择状态的进入协程。</returns>
    protected override IEnumerator OnEnter()
    {
        yield return null;
    }

    /// <summary>
    /// 在关卡选择 UI 关闭后执行退出回调。
    /// </summary>
    /// <returns>关卡选择状态的退出协程。</returns>
    protected override IEnumerator OnExit()
    {
        yield return null;
    }
    #endregion
}
