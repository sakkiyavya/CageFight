using System.Collections;
using UnityEngine;

/// <summary>
/// 状态基类：继承 MonoBehaviour，对应场景中的实体对象。
/// 通过 SceneFSM.Instance 访问状态机，不再持有 fsm 字段。
/// 仅暴露可由协程驱动的 Enter 和 Exit。
/// </summary>
public abstract class SceneStateBase : MonoBehaviour
{
    /// <summary>
    /// 进入状态的协程，可在此实现渐入、加载初始化等
    /// </summary>
    public abstract IEnumerator Enter();

    /// <summary>
    /// 退出状态的协程，可在此实现渐出、资源回收等
    /// </summary>
    public abstract IEnumerator Exit();
}
