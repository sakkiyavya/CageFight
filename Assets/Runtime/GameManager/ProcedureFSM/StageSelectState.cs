using System.Collections;
using UnityEngine;

/// <summary>
/// 关卡选择状态。
/// 关卡选择界面的 UI 模块通过 SceneStateBase.stateModules 配置和管理。
/// </summary>
public class StageSelectState : SceneStateBase
{
    protected override IEnumerator OnEnter()
    {
        Debug.Log("[StageSelectState] OnEnter - 进入关卡选择界面。");
        yield return null;
    }

    protected override IEnumerator OnExit()
    {
        Debug.Log("[StageSelectState] OnExit - 离开关卡选择界面。");
        yield return null;
    }
}
