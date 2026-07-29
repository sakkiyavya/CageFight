using System.Collections;
using UnityEngine;

/// <summary>
/// 结束结算状态。
/// UI 模块（GameOverPanel）由基类 stateModules 统一驱动开关，
/// OnEnter 负责刷新结算面板内容，OnExit 负责释放本局关卡资源。
/// </summary>
public class GameOverState : SceneStateBase
{
    #region 生命周期与回调
    /// <summary>
    /// 进入结算流程，并预留获取战斗结果及刷新奖励、星级和得分界面的逻辑。
    /// </summary>
    /// <returns>结算状态的进入协程。</returns>
    protected override IEnumerator OnEnter()
    {
        Debug.Log("[GameOverState] OnEnter - 战斗结束，展现胜利/失败结算 UI！");
        // TODO: 从 GameplayState 或事件中获取战斗结果数据
        // TODO: 刷新 GameOverPanel 中的结算数据（星级、得分、奖励等）
        yield return null;
    }

    /// <summary>
    /// 退出结算流程，并预留释放本局资源的逻辑。
    /// </summary>
    /// <returns>结算状态的退出协程。</returns>
    protected override IEnumerator OnExit()
    {
        Debug.Log("[GameOverState] OnExit - 关闭结算界面，释放本局关卡资源...");
        // TODO: 调用 ResourceManager.Instance.ReleaseLevelResources() 释放本局资源句柄
        yield return null;
    }
    #endregion
}
