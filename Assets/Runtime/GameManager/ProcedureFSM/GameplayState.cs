using System.Collections;
using UnityEngine;

/// <summary>
/// 局内游戏进行状态。
/// UI 模块（HUDPanel 等）由基类 stateModules 统一驱动开关，
/// OnEnter 负责启动局内逻辑，OnExit 负责清理所有局内实体。
/// </summary>
public class GameplayState : SceneStateBase
{
    #region 生命周期与回调
    /// <summary>
    /// 在关卡对象构造完成后进入局内流程，并预留计时器与地图单位启动逻辑。
    /// </summary>
    /// <returns>局内状态的进入协程。</returns>
    protected override IEnumerator OnEnter()
    {
        Debug.Log("[GameplayState] OnEnter - 场景关卡构造完毕，游戏正式开始！");
        // TODO: 启动局内计时器
        // TODO: 通知 StageSystem 实例化地图网格与单位
        yield return null;
    }

    /// <summary>
    /// 退出局内流程，并预留停止计时器、清理实体及地图占用数据的逻辑。
    /// </summary>
    /// <returns>局内状态的退出协程。</returns>
    protected override IEnumerator OnExit()
    {
        Debug.Log("[GameplayState] OnExit - 退出战斗，清理场上残留实体...");
        // TODO: 停止局内计时器
        // TODO: 清理场上所有怪物、子弹与网格占用数据
        yield return null;
    }
    #endregion
}
