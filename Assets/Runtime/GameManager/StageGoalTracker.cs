using TMPro;
using UnityEngine;

/// <summary>
/// 阶段 4：局内胜负判定器（挂 GameplayState，进入局内时 Setup）。
/// 三种模式：
///   Defense：计时结束且我方大本营存活 → 胜；提前推掉敌方大本营 → 胜；我方大本营被拆 → 败。
///   Attack ：摧毁敌方大本营 → 胜；计时到点未摧毁 → 败；我方大本营被拆 → 败。
///   Boss   ：限时内击败 Boss → 胜；计时到点 → 败；我方大本营被拆 → 败。
/// 工程师无敌（失败判定不含工程师阵亡）。
/// 敌方大本营按 enemyBaseDistance 自动摆放在我方大本营右侧（第一支敌方队伍的种族大本营）；
/// Boss 按 bossPrefabKey / bossGridPosition 生成（Boss 血条由预制体 CharacterHealth.alwaysShowBar 常显）。
/// 倒计时文本在 Setup 时按模式限时显隐，结算统一走 GameOverManager.TriggerGameOver。
/// </summary>
[DisallowMultipleComponent]
public sealed class StageGoalTracker : MonoBehaviour
{
    [SerializeField, Tooltip("倒计时文本（屏幕中上方；未配置则不显示倒计时）")]
    private TMP_Text countdownText;

    private StageConfig _config;
    private UserGlobalInfo.StageType _stageType;
    private GameObject _friendlyBase;
    private GameObject _enemyBase;
    private GameObject _boss;
    private BuildingHealth _friendlyHealth;
    private BuildingHealth _enemyHealth;
    private CharacterHealth _bossHealth;
    private bool _friendlyBaseExpected;
    private bool _enemyBaseSpawned;
    private bool _bossSpawned;
    private float _levelStart;
    private float _timeLimit;
    private bool _finished;

    /// <summary>
    /// 进入局内时初始化：读取模式与限时，定位我方大本营，
    /// 并按模式生成敌方大本营/Boss。
    /// </summary>
    public void Setup(StageConfig config)
    {
        _config = config;
        _finished = false;
        _enemyBase = null;
        _enemyHealth = null;
        _enemyBaseSpawned = false;
        _boss = null;
        _bossHealth = null;
        _bossSpawned = false;

        _friendlyBase = StageObjectInstantiator.LastFriendlyMainBase;
        _friendlyHealth = _friendlyBase != null ? _friendlyBase.GetComponent<BuildingHealth>() : null;
        _friendlyBaseExpected = config != null && config.hasFriendlyMainBaseGridPosition && _friendlyBase != null;

        if (config == null)
        {
            SetCountdownVisible(false);
            return;
        }

        _stageType = config.stageType;
        _levelStart = Time.time;

        switch (_stageType)
        {
            case UserGlobalInfo.StageType.Attack:
                _timeLimit = Mathf.Max(0f, config.attackTimeLimit);
                SpawnEnemyBase(config);
                break;
            case UserGlobalInfo.StageType.Defense:
                _timeLimit = Mathf.Max(0f, config.DefenseTime);
                // 防守关敌方同样有基地（视觉/目标一致）；本模式胜负不取决于它。
                SpawnEnemyBase(config);
                break;
            case UserGlobalInfo.StageType.Boss:
                _timeLimit = Mathf.Max(0f, config.bossTimeLimit);
                SpawnBoss(config);
                break;
        }

        SetCountdownVisible(_timeLimit > 0f);
    }

    /// <summary>局内结束/退出时隐藏倒计时并停止判定。</summary>
    public void Clear()
    {
        _finished = true;
        SetCountdownVisible(false);
    }

    private void Update()
    {
        if (_finished || _config == null || GameOverManager.Instance == null)
            return;

        if (countdownText != null && _timeLimit > 0f)
        {
            float remaining = Mathf.Max(0f, _timeLimit - (Time.time - _levelStart));
            countdownText.text = FormatTime(remaining);
        }

        bool friendlyDead = _friendlyBaseExpected &&
            (_friendlyHealth == null || _friendlyHealth.IsDead() ||
             !_friendlyBase.activeInHierarchy);

        switch (_stageType)
        {
            case UserGlobalInfo.StageType.Defense:
                bool enemyBaseDead = _enemyBaseSpawned &&
                    (_enemyHealth == null || _enemyHealth.IsDead() ||
                     !_enemyBase.activeInHierarchy);
                if (friendlyDead)
                    Finish(false);
                else if (enemyBaseDead)
                    Finish(true);   // 提前推掉敌方大本营 = 直接胜利。
                else if (_timeLimit > 0f && Time.time - _levelStart >= _timeLimit)
                    Finish(true);
                break;

            case UserGlobalInfo.StageType.Attack:
                bool enemyDead = _enemyBaseSpawned &&
                    (_enemyHealth == null || _enemyHealth.IsDead() ||
                     !_enemyBase.activeInHierarchy);
                if (friendlyDead)
                    Finish(false);
                else if (enemyDead)
                    Finish(true);
                else if (_timeLimit > 0f && Time.time - _levelStart >= _timeLimit)
                    Finish(false);
                break;

            case UserGlobalInfo.StageType.Boss:
                bool bossDead = _bossSpawned &&
                    (_bossHealth == null || _bossHealth.IsDead() ||
                     !_boss.activeInHierarchy);
                if (friendlyDead)
                    Finish(false);
                else if (bossDead)
                    Finish(true);
                else if (_timeLimit > 0f && Time.time - _levelStart >= _timeLimit)
                    Finish(false);
                break;
        }
    }

    private void Finish(bool victory)
    {
        _finished = true;
        SetCountdownVisible(false);
        GameOverManager.Instance.TriggerGameOver(victory);
    }

    private void SetCountdownVisible(bool visible)
    {
        if (countdownText != null && countdownText.gameObject.activeSelf != visible)
            countdownText.gameObject.SetActive(visible);
    }

    /// <summary>本局敌方大本营实例（第一支敌方队伍种族；供局内等级规则等读取，未生成时为 null）。</summary>
    public static GameObject LastEnemyMainBase { get; private set; }

    /// <summary>
    /// 敌方大本营 = 第一支敌方队伍种族的大本营预制体，
    /// 摆放在我方大本营右侧 enemyBaseDistance 格（同一行）。
    /// </summary>
    private void SpawnEnemyBase(StageConfig config)
    {
        EnemyTeamConfig team = config.enemyTeams != null && config.enemyTeams.Count > 0
            ? config.enemyTeams[0]
            : null;
        if (team == null || string.IsNullOrEmpty(team.raceId))
        {
            Debug.LogWarning("[StageGoalTracker] 进攻关未配置敌方队伍/种族，敌方大本营无法生成（胜利条件不可达成）。");
            return;
        }

        GameObject prefab = BuildingButton.TryResolveBuilding(team.raceId, BuildingType.MainBase);
        if (prefab == null)
        {
            Debug.LogWarning($"[StageGoalTracker] 种族 {team.raceId} 未配置 MainBase 建筑，敌方大本营无法生成。");
            return;
        }

        if (GameObjectPool.Instance == null)
            return;

        _enemyBase = GameObjectPool.Instance.Get(prefab);
        if (_enemyBase == null)
            return;

        GameObjectProperty prop = _enemyBase.GetComponent<GameObjectProperty>();
        int side = Mathf.Max(2, team.teamId);
        if (prop != null)
            prop.side = side;   // 偶数队 = 敌方。

        Vector2Int occupy = prop != null ? prop.occupySpace : Vector2Int.one;
        int gridX = config.friendlyMainBaseGridPosition.x + Mathf.Max(1, config.enemyBaseDistance);
        int gridY = config.friendlyMainBaseGridPosition.y;
        _enemyBase.transform.position = new Vector3(
            gridX + occupy.x / 2f,
            gridY + occupy.y / 2f,
            0f);

        StageObjectInstantiator.ApplyEnemyLevels(_enemyBase, config);

        _enemyHealth = _enemyBase.GetComponent<BuildingHealth>();
        if (_enemyHealth != null)
            _enemyHealth.SetPercentHp(1f);   // 预摆建筑无施工流程，出生即满血。

        BuildingBase building = _enemyBase.GetComponent<BuildingBase>();
        if (building != null)
            building.RefreshOccupancy();

        _enemyBaseSpawned = true;
        LastEnemyMainBase = _enemyBase;   // 记录本局敌方大本营（局内等级规则等读取）。
    }

    /// <summary>按关卡配置生成 Boss（Boss 血条由预制体 CharacterHealth.alwaysShowBar 常显）。</summary>
    private void SpawnBoss(StageConfig config)
    {
        if (string.IsNullOrEmpty(config.bossPrefabKey) || ResourceManager.Instance == null)
        {
            Debug.LogWarning("[StageGoalTracker] Boss 关未配置 bossPrefabKey，Boss 无法生成（胜利条件不可达成）。");
            return;
        }

        GameObject prefab = ResourceManager.Instance.GetGameObject(config.bossPrefabKey);
        if (prefab == null)
        {
            Debug.LogWarning($"[StageGoalTracker] Boss 预制体未预载：{config.bossPrefabKey}，请登记 PrefabRegistry 并加入关卡预载清单。");
            return;
        }

        if (GameObjectPool.Instance == null)
            return;

        _boss = GameObjectPool.Instance.Get(prefab);
        if (_boss == null)
            return;

        GameObjectProperty prop = _boss.GetComponent<GameObjectProperty>();
        if (prop != null)
        {
            prop.side = 2;   // 敌方。
            prop.defenseMagicLevel = Mathf.Max(1, config.enemyDefenseMagicLevel);
            prop.attackMagicLevel = Mathf.Max(1, config.enemyAttackMagicLevel);
            prop.barracksLevel = Mathf.Max(1, config.enemyBarracksLevel);
        }

        Vector2Int occupy = prop != null ? prop.occupySpace : Vector2Int.one;
        _boss.transform.position = new Vector3(
            config.bossGridPosition.x + occupy.x / 2f,
            config.bossGridPosition.y + occupy.y / 2f,
            0f);

        _bossHealth = _boss.GetComponent<CharacterHealth>();
        _bossSpawned = true;
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }
}
