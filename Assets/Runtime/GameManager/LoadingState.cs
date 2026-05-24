using System.Collections;
using UnityEngine;

/// <summary>
/// 场景过渡与加载状态
/// </summary>
public class LoadingState : SceneStateBase
{
    public LoadingState(SceneFSM fsm) : base(fsm) { }

    public override IEnumerator Enter()
    {
        Debug.Log("[LoadingState] Enter - 正在打开加载黑屏遮罩，加载基础数据...");
        // TODO: 加载资源、黑屏遮罩渐入
        yield return new WaitForSeconds(0.5f);
    }

    public override IEnumerator Exit()
    {
        Debug.Log("[LoadingState] Exit - 加载完毕，黑屏遮罩渐出消失...");
        // TODO: 加载UI隐去
        yield return new WaitForSeconds(0.3f);
    }
}
