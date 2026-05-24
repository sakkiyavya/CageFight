using System.Collections;
using UnityEngine;

/// <summary>
/// 局内游戏进行状态
/// </summary>
public class GameplayState : SceneStateBase
{
    public GameplayState(SceneFSM fsm) : base(fsm) { }

    public override IEnumerator Enter()
    {
        Debug.Log("[GameplayState] Enter - 场景关卡构造完毕，游戏正式开始，初始化场上怪物与基地！");
        // TODO: 启动局内计时器，通知 StageSystem 实例化网格和单位
        yield return null;
    }

    public override IEnumerator Exit()
    {
        Debug.Log("[GameplayState] Exit - 退出战斗，清理场上残留实体...");
        // TODO: 清理局内所有怪物、子弹与网格占用数据
        yield return null;
    }
}
