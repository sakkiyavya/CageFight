using System.Collections;
using UnityEngine;

/// <summary>
/// 结束结算状态
/// </summary>
public class GameOverState : SceneStateBase
{
    public override IEnumerator Enter()
    {
        Debug.Log("[GameOverState] Enter - 战斗结束，展现胜利/失败结算 UI 弹窗！");
        // TODO: 展现结算看板
        yield return new WaitForSeconds(0.8f);
    }

    public override IEnumerator Exit()
    {
        Debug.Log("[GameOverState] Exit - 关闭结算 UI，回收结算界面...");
        // TODO: 结算面板关闭
        yield return null;
    }
}
