using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 局内游戏进行状态。
/// UI 模块（HUDPanel 等）由基类 stateModules 统一驱动开关，
/// OnEnter 负责启动局内逻辑并关闭残留的菜单面板，OnExit 负责清理所有局内实体。
/// </summary>
public class GameplayState : SceneStateBase
{
    [SerializeField] private PlayerLoadoutSpawner playerLoadoutSpawner;
    [Header("局内背景音乐")]
    [SerializeField, ResourceKey(typeof(AudioClip)), Tooltip("进入局内时切换的背景音乐资源键")]
    private string bgmKey = "Crystal Mine Cave";
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 1f;

    [Header("进入局内时关闭的菜单面板")]
    [SerializeField, Tooltip("菜单侧按需打开的面板（图鉴/商店/设置等）：进入局内时统一关闭，避免战斗界面残留 UI")]
    private List<GameObject> closeOnEnter = new List<GameObject>();

    [Header("敌方 AI 指挥官（阶段 3）")]
    [SerializeField, Tooltip("按 StageConfig.enemyTeams 顺序对应的指挥官槽位（最多 4 支敌方队伍）")]
    private TeamCommander[] enemyCommanders = new TeamCommander[0];

    [Header("胜负判定（阶段 4）")]
    [SerializeField, Tooltip("局内胜负判定器（三种模式判定 + 倒计时）")]
    private StageGoalTracker goalTracker;

    #region 生命周期与回调
    /// <summary>
    /// 在关卡对象构造完成后进入局内流程，切换局内背景音乐，并预留计时器与地图单位启动逻辑。
    /// </summary>
    /// <returns>局内状态的进入协程。</returns>
    protected override IEnumerator OnEnter()
    {
        // 先关闭菜单侧残留面板，保证进入战斗时界面干净。
        for (int i = 0; i < closeOnEnter.Count; i++)
        {
            if (closeOnEnter[i] != null)
                closeOnEnter[i].SetActive(false);
        }

        if (GameOverManager.Instance != null)
            GameOverManager.Instance.ResetGameOverState();

        // 局内经济开始打点：经济增长曲线按局内时间推进（Coins 读取当前关卡曲线配置）。
        if (Coins.Instance != null)
            Coins.Instance.OnLevelStart();

        // 敌方 AI：按本关队伍表逐队配置指挥官（未配置的队伍停用）。
        ConfigureEnemyCommanders();

        // 胜负判定：按关卡模式启动（限时、大本营/Boss 目标与倒计时）。
        if (goalTracker != null)
            goalTracker.Setup(CurrentStageConfig);

        // 进入局内：切换关卡背景音乐（经音频框架统一入口，淡入淡出循环）。
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMusic(bgmKey, bgmVolume);

        if (playerLoadoutSpawner)
            yield return playerLoadoutSpawner.SpawnSelectedEngineerRoutine();

        // TODO: 启动局内计时器
        // TODO: 通知 StageSystem 实例化地图网格与单位
        yield return null;
    }

    /// <summary>
    /// 退出局内流程，释放工程师，并清理场上所有由对象池生成的局内实体。
    /// </summary>
    /// <returns>局内状态的退出协程。</returns>
    protected override IEnumerator OnExit()
    {
        // 先停用全部敌方指挥官：防止进入结算后 AI 继续决策、在地图上盖出新建筑。
        if (enemyCommanders != null)
        {
            for (int i = 0; i < enemyCommanders.Length; i++)
            {
                if (enemyCommanders[i] != null)
                    enemyCommanders[i].Configure(null, null);
            }
        }

        if (playerLoadoutSpawner)
            playerLoadoutSpawner.ReleaseSpawnedEngineer();

        // 清理场上所有由对象池生成的局内实体（怪物、子弹、特效等）；
        // 工程师已先释放（处于停用状态），不会被重复回收；非池化对象保持原位。
        if (GameObjectPool.Instance != null)
            GameObjectPool.Instance.ReleaseAllActive();

        // 胜负判定结束：隐藏倒计时、停止判定（防止退出局内后仍触发结算）。
        if (goalTracker != null)
            goalTracker.Clear();

        // TODO: 停止局内计时器
        // TODO: 清理网格占用数据
        yield return null;
    }

    /// <summary>
    /// 按本关队伍表逐队配置敌方指挥官：有配置且种族非空的队伍启用，
    /// 其余停用（enabled=false，指挥官静默）。
    /// </summary>
    private void ConfigureEnemyCommanders()
    {
        if (enemyCommanders == null || enemyCommanders.Length == 0)
            return;

        StageConfig config = CurrentStageConfig;
        List<EnemyTeamConfig> teams =
            config != null && config.enemyTeams != null ? config.enemyTeams : null;

        for (int i = 0; i < enemyCommanders.Length; i++)
        {
            TeamCommander commander = enemyCommanders[i];
            if (commander == null)
                continue;

            bool hasTeam = teams != null && i < teams.Count && teams[i] != null &&
                !string.IsNullOrEmpty(teams[i].raceId);
            commander.Configure(hasTeam ? teams[i] : null, config);
        }
    }
    #endregion
}
