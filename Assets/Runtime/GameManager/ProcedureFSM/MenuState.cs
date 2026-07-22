using System.Collections;
using UnityEngine;

/// <summary>
/// 主菜单状态
/// UI 模块（GameStartPanel 等）由基类 stateModules 统一驱动开关，
/// 此处只处理菜单业务逻辑。
/// </summary>
public class MenuState : SceneStateBase
{
    protected override IEnumerator OnEnter()
    {
        // TODO: 触发关卡列表异步加载（LevelConfigLoader）
        // TODO: 重置翻页状态、关卡选择滚动位置
        yield return null;
    }

    protected override IEnumerator OnExit()
    {
        // TODO: 清理翻页状态、重置关卡选择滚动位置
        yield return null;
    }
}
