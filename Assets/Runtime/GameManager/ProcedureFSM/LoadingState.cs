using System.Collections;
using UnityEngine;

public class LoadingState : SceneStateBase
{
    #region 生命周期与回调
    /// <summary>
    /// 加载当前关卡所需资源，等待资源系统完成后实例化关卡对象，并切换到游戏状态。
    /// 任一前置条件或加载步骤失败时会记录错误并终止进入流程。
    /// </summary>
    /// <returns>等待资源加载和关卡实例化完成的协程。</returns>
    protected override IEnumerator OnEnter()
    {
        if (CurrentStageConfig == null)
        {
            Debug.LogError("[LoadingState] StageConfig is missing.");
            yield break;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[LoadingState] ResourceManager is not initialized.");
            yield break;
        }

        Debug.Log($"[LoadingState] Loading resources for stage: {CurrentStageConfig.stageId}");
        if (!ResourceManager.Instance.LoadStageResources(CurrentStageConfig))
        {
            Debug.LogError("[LoadingState] Failed to start resource loading.");
            yield break;
        }

        while (ResourceManager.Instance.CurrentState == ResourceState.Loading)
            yield return null;

        if (ResourceManager.Instance.CurrentState != ResourceState.LoadComplete)
        {
            Debug.LogError($"[LoadingState] Resource loading did not complete. Current state: {ResourceManager.Instance.CurrentState}");
            yield break;
        }

        Debug.Log($"[LoadingState] Resources loaded. Instantiating stage: {CurrentStageConfig.stageId}");
        if (!StageObjectInstantiator.InstantiateStage(CurrentStageConfig))
        {
            Debug.LogError("[LoadingState] Failed to instantiate stage objects.");
            yield break;
        }

        SceneFSM.Instance.LoadState(GameState.Gameplay);
    }

    /// <summary>
    /// 完成加载状态的退出流程；当前无需额外清理，仅等待一帧。
    /// </summary>
    /// <returns>加载状态的退出协程。</returns>
    protected override IEnumerator OnExit()
    {
        yield return null;
    }
    #endregion
}
