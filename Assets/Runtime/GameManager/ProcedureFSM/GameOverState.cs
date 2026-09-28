using System.Collections;
using UnityEngine;

/// <summary>
/// 结束结算状态。
/// UI 模块（GameOverCanvas）由基类 stateModules 统一驱动开关；
/// OnEnter 按本局胜负显示 Next（胜利）或 Try again（失败）按钮。
/// </summary>
public class GameOverState : SceneStateBase
{
    [SerializeField, Tooltip("胜利时显示的下一关按钮")]
    private GameObject nextButton;

    [SerializeField, Tooltip("失败时显示的重试按钮")]
    private GameObject tryAgainButton;

    #region 生命周期与回调
    /// <summary>
    /// 进入结算流程：按 GameOverManager 记录的胜负结果显示对应按钮。
    /// </summary>
    /// <returns>结算状态的进入协程。</returns>
    protected override IEnumerator OnEnter()
    {
        bool victory = GameOverManager.Instance != null && GameOverManager.Instance.IsVictory;

        if (nextButton != null)
            nextButton.SetActive(victory);
        if (tryAgainButton != null)
            tryAgainButton.SetActive(!victory);

        yield return null;
    }

    /// <summary>
    /// 退出结算流程，并预留释放本局资源的逻辑。
    /// </summary>
    /// <returns>结算状态的退出协程。</returns>
    protected override IEnumerator OnExit()
    {
        // TODO: 调用 ResourceManager 释放本局资源句柄
        yield return null;
    }
    #endregion
}
