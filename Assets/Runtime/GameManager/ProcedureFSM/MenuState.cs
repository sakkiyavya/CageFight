using System.Collections;
using UnityEngine;

/// <summary>
/// 主菜单状态
/// UI 模块（GameStartPanel 等）由基类 stateModules 统一驱动开关，
/// 此处只处理菜单业务逻辑。
/// </summary>
public class MenuState : SceneStateBase
{
    #region 生命周期与回调
    /// <summary>
    /// 执行主菜单进入流程；当前预留关卡列表加载和界面状态重置逻辑。
    /// </summary>
    /// <returns>主菜单状态的进入协程。</returns>
    protected override IEnumerator OnEnter()
    {
        // TODO: 触发关卡列表异步加载（StageConfigLoader）
        // TODO: 重置翻页状态、关卡选择滚动位置
        yield return null;
    }

    /// <summary>
    /// 执行主菜单退出流程；当前预留翻页和关卡选择界面的清理逻辑。
    /// </summary>
    /// <returns>主菜单状态的退出协程。</returns>
    protected override IEnumerator OnExit()
    {
        // TODO: 清理翻页状态、重置关卡选择滚动位置
        yield return null;
    }
    #endregion
}
