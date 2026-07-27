using System.Collections;
using UnityEngine;

public class LoadingState : SceneStateBase
{
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

    protected override IEnumerator OnExit()
    {
        yield return null;
    }
}
