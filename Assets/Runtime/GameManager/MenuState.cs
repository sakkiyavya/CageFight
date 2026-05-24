using System.Collections;
using UnityEngine;

/// <summary>
/// 主菜单状态
/// </summary>
public class MenuState : SceneStateBase
{
    public MenuState(SceneFSM fsm) : base(fsm) { }

    public override IEnumerator Enter()
    {
        Debug.Log("[MenuState] Enter - 正在展示主菜单，播放UI淡入...");
        // TODO: 触发主菜单UI渐入或资源准备
        yield return new WaitForSeconds(0.5f); // 模拟一个温和的UI淡入转场
    }

    public override IEnumerator Exit()
    {
        Debug.Log("[MenuState] Exit - 正在退出主菜单，播放UI淡出...");
        // TODO: 触发主菜单UI渐出，隐藏菜单界面
        yield return new WaitForSeconds(0.3f);
    }
}
