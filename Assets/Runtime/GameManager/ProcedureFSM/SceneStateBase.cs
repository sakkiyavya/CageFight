using System.Collections;

/// <summary>
/// 状态基类：不负责 Update，仅暴露可由协程驱动的 Enter 和 Exit
/// </summary>
public abstract class SceneStateBase
{
    protected SceneFSM fsm;

    protected SceneStateBase(SceneFSM fsm)
    {
        this.fsm = fsm;
    }

    /// <summary>
    /// 进入状态的协程，可在此实现渐入、加载初始化等
    /// </summary>
    public abstract IEnumerator Enter();

    /// <summary>
    /// 退出状态的协程，可在此实现渐出、资源回收等
    /// </summary>
    public abstract IEnumerator Exit();
}
