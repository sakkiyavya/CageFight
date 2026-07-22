using System.Collections;
using UnityEngine;

public class LoadingState : SceneStateBase
{
    protected override IEnumerator OnEnter()
    {
        if (CurrentLevelConfig == null)
        {
            Debug.LogError("[LoadingState] LevelConfig is missing.");
            yield break;
        }

        if (ResourceManager.Instance == null)
        {
            Debug.LogError("[LoadingState] ResourceManager is not initialized.");
            yield break;
        }

        Debug.Log($"[LoadingState] Loading resources for level: {CurrentLevelConfig.levelId}");
        if (!ResourceManager.Instance.LoadStageResources(CurrentLevelConfig))
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

        Debug.Log($"[LoadingState] Resources loaded. Instantiating level: {CurrentLevelConfig.levelId}");
        if (!StageObjectInstantiator.InstantiateLevel(CurrentLevelConfig))
        {
            Debug.LogError("[LoadingState] Failed to instantiate level objects.");
            yield break;
        }

        SceneFSM.Instance.LoadState(GameState.Gameplay);
    }

    protected override IEnumerator OnExit()
    {
        yield return null;
    }
}
