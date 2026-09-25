using System.Collections;
using UnityEngine;

public class LoadingState : SceneStateBase
{
    [SerializeField] private PlayerLoadoutManager playerLoadout;
    [SerializeField, Tooltip("图鉴目录：进关前注册，供“遇见即解锁”标记解析条目。")]
    private BookCatalog bookCatalog;

    #region 生命周期与回调
    /// <summary>
    /// 加载当前关卡所需资源，等待资源系统完成后实例化关卡对象，并切换到游戏状态。
    /// 任一前置条件或加载步骤失败时会记录错误并终止进入流程。
    /// </summary>
    /// <returns>等待资源加载和关卡实例化完成的协程。</returns>
    protected override IEnumerator OnEnter()
    {
        RemoteStartup.GetForStageLoading()?.ShowLoading(this);
        try
        {
            yield return null;
            yield return LoadStage();
        }
        finally
        {
            RemoteStartup.Instance?.HideLoading(this);
        }
    }

    // 遮罩覆盖关卡预载、出战资源预载以及关卡实例化的完整过程。
    private IEnumerator LoadStage()
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
        BookProgress.RegisterCatalog(bookCatalog);
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

        if (playerLoadout)
        {
            while (!playerLoadout.IsReady) yield return null;
            yield return playerLoadout.PreloadGameplayResources();
        }

        // 兵种资源按“本局实际出现的种族”动态预载（自动扫描兵种预制体携带的全部依赖，含音效）：
        // 玩家所选种族 + 每支敌方队伍。敌方队伍遵循 troopUnlocks 规则：
        // 列表为空 = 本局全兵种可用 → 整族预载；填写了具体兵种 = 本局只能生产列表内兵种 → 只预载白名单。
        if (playerLoadout && playerLoadout.TryGetSelectedRace(out RaceDefinition playerRace))
            TroopPreloader.PreloadRaceTroops(playerRace.Id);
        if (CurrentStageConfig.enemyTeams != null)
        {
            for (int i = 0; i < CurrentStageConfig.enemyTeams.Count; i++)
            {
                EnemyTeamConfig team = CurrentStageConfig.enemyTeams[i];
                if (team != null && !string.IsNullOrEmpty(team.raceId))
                    TroopPreloader.PreloadTeamTroops(team);
            }
        }

        Debug.Log($"[LoadingState] Resources loaded. Instantiating stage: {CurrentStageConfig.stageId}");
        string friendlyMainBasePrefabKey = playerLoadout
            ? playerLoadout.SelectedRaceMainBasePrefabKey
            : string.Empty;
        if (!StageObjectInstantiator.InstantiateStage(CurrentStageConfig, friendlyMainBasePrefabKey))
        {
            Debug.LogError("[LoadingState] Failed to instantiate stage objects.");
            yield break;
        }

        if (RemoteStartup.Instance != null)
            yield return RemoteStartup.Instance.WaitForMinimumDisplay();

        SceneFSM.Instance.LoadState(GameState.Gameplay);
    }

    /// <summary>
    /// 完成加载状态的退出流程；当前无需额外清理，仅等待一帧。
    /// </summary>
    /// <returns>加载状态的退出协程。</returns>
    protected override IEnumerator OnExit()
    {
        RemoteStartup.Instance?.HideLoading(this);
        yield return null;
    }

    private void OnDisable()
    {
        RemoteStartup.Instance?.HideLoading(this);
    }
    #endregion
}
