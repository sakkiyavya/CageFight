using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class SceneStateBase : MonoBehaviour
{
    [Header("该状态激活时打开的 UI 模块")]
    [SerializeField] List<UISystemBase> stateModules = new List<UISystemBase>();    // 进入该状态时需要显示、退出时需要隐藏的 UI 模块。

    private StageConfig _stageConfig;                                               // 当前流程正在使用的关卡配置。

    protected StageConfig CurrentStageConfig => _stageConfig;                       // 供具体状态读取的当前关卡配置。

    #region 内部辅助
    /// <summary>
    /// 保存状态机传入的关卡配置，供加载和游戏状态使用。
    /// </summary>
    /// <param name="stageConfig">即将进入的关卡配置。</param>
    internal void SetStageConfig(StageConfig stageConfig)
    {
        _stageConfig = stageConfig;
    }
    #endregion

    #region 特效与协程
    /// <summary>
    /// 依次播放并打开该状态关联的 UI 模块，然后执行子类的进入逻辑。
    /// </summary>
    /// <returns>等待全部 UI 进入动画和状态进入逻辑完成的协程。</returns>
    public virtual IEnumerator Enter()
    {
        yield return OpenModules();
        yield return OnEnter();
    }

    /// <summary>
    /// 依次关闭该状态关联的 UI 模块，然后执行子类的退出逻辑。
    /// </summary>
    /// <returns>等待全部 UI 退出动画和状态退出逻辑完成的协程。</returns>
    public virtual IEnumerator Exit()
    {
        yield return CloseModules();
        yield return OnExit();
    }

    /// <summary>
    /// 激活所有已配置的 UI 模块，并并行等待它们的进入动画完成。
    /// </summary>
    /// <returns>等待全部模块进入动画完成的协程。</returns>
    private IEnumerator OpenModules()
    {
        var coroutines = new List<Coroutine>();
        foreach (var module in stateModules)
        {
            if (module != null)
            {
                module.gameObject.SetActive(true);
                coroutines.Add(StartCoroutine(module.UIMotionEffectRoutine(true)));
            }
        }
        foreach (var coroutine in coroutines)
            yield return coroutine;
    }

    /// <summary>
    /// 并行播放所有活动 UI 模块的退出动画，完成后停用对应对象。
    /// </summary>
    /// <returns>等待全部模块退出动画完成的协程。</returns>
    private IEnumerator CloseModules()
    {
        var coroutines = new List<Coroutine>();
        foreach (var module in stateModules)
        {
            if (module != null)
            {
                if (module.gameObject.activeInHierarchy)
                    coroutines.Add(StartCoroutine(module.UIMotionEffectRoutine(false)));
            }
        }
        foreach (var coroutine in coroutines)
            yield return coroutine;

        foreach (var module in stateModules)
        {
            if (module != null)
                module.gameObject.SetActive(false);
        }
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 为具体状态提供可重写的进入流程；基类默认只等待一帧。
    /// </summary>
    /// <returns>具体状态的进入协程。</returns>
    protected virtual IEnumerator OnEnter() { yield return null; }
    /// <summary>
    /// 为具体状态提供可重写的退出流程；基类默认只等待一帧。
    /// </summary>
    /// <returns>具体状态的退出协程。</returns>
    protected virtual IEnumerator OnExit() { yield return null; }
    #endregion
}
