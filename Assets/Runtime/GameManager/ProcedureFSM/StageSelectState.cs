using System.Collections;
using UnityEngine;

/// <summary>
/// 关卡选择状态。
/// 关卡选择界面的 UI 模块通过 SceneStateBase.stateModules 配置和管理。
/// </summary>
public class StageSelectState : SceneStateBase
{
    #region 生命周期与回调
    /// <summary>
    /// 在关卡选择 UI 已打开后执行进入回调，并记录流程日志。
    /// </summary>
    /// <returns>关卡选择状态的进入协程。</returns>
    protected override IEnumerator OnEnter()
    {
        Debug.Log("[StageSelectState] OnEnter - 进入关卡选择界面。");
        yield return null;
    }

    /// <summary>
    /// 在关卡选择 UI 关闭后执行退出回调，并记录流程日志。
    /// </summary>
    /// <returns>关卡选择状态的退出协程。</returns>
    protected override IEnumerator OnExit()
    {
        Debug.Log("[StageSelectState] OnExit - 离开关卡选择界面。");
        yield return null;
    }
    #endregion
}
