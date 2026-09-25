using System.Collections.Generic;
using UnityEngine;

/// <summary>兵种定位标签（敌方 AI 判定兵种作用与转产克制用）。</summary>
public enum TroopTag
{
    None = 0,      // 未标注
    Melee = 1,     // 近战（高速突进）
    Ranged = 2,    // 远程
    Siege = 3,     // 攻城（对建筑高威胁）
    Tank = 4,      // 肉盾
    Support = 5,   // 辅助
}

/// <summary>
/// 训练兵种的静态定义数据：阶数、解锁建筑等级、单次产出数量、冷却与资源键。
/// 运行时资源必须通过 ResourceManager 按资源键取得，不直接持有贴图/预制体引用。
/// </summary>
[CreateAssetMenu(fileName = "NewTroop", menuName = "Building/Troop")]
public sealed class TroopDefinition : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;

    [Header("AI 定位")]
    [SerializeField, Tooltip("兵种定位标签（敌方 AI 判断兵种作用与转产克制用）")]
    private TroopTag tag = TroopTag.Melee;
    [SerializeField, Min(0f), Tooltip("对建筑的威胁权重（敌方 AI 威胁评估：攻击力 × 该权重计入威胁值）")]
    private float threatScore = 1f;

    [Header("训练规则")]
    [SerializeField, Range(1, 3)] private int tier = 1;
    [SerializeField, Min(0), Tooltip("解锁所需兵营等级（1 起；兵营显示等级 = BuildUP 等级 + 1）")]
    private int unlockLevel;
    [SerializeField, Min(1)] private int trainCount = 1;
    [SerializeField, Min(.1f)] private float cooldown = 5f;
    [SerializeField, Min(0), Tooltip("选定本兵种后每秒扣除的资源获取量（维护费）")]
    private int upkeep;
    [SerializeField, Tooltip("勾选后，该兵种需要玩家拥有后才能训练（未拥有时头像暗淡）")]
    private bool requiresOwnership;

    [Header("资源")]
    [ResourceKey(typeof(Sprite))]
    [SerializeField] private string iconKey;
    [ResourceKey(typeof(GameObject))]
    [SerializeField] private string prefabKey;
    [ResourceKey(typeof(RuntimeAnimatorController))]
    [SerializeField, Tooltip("兵种单位的动画控制器资源键（数据驱动预载；框架暂无单位依赖扫描 API，先由本配置提供）")]
    private string animatorControllerKey;
    [ResourceKey(typeof(Sprite))]
    [SerializeField, Tooltip("兵种单位的基础动画贴图资源键（同上，用于召唤前预载）")]
    private string animationSpriteKey;

#if UNITY_EDITOR
    [SerializeField] private Sprite editorIcon;
    [SerializeField] private GameObject editorPrefab;
#endif

    public string Id => id;
    public string DisplayName => displayName;
    public TroopTag Tag => tag;
    public float ThreatScore => threatScore;
    public int Tier => tier;
    public int UnlockLevel => unlockLevel;
    public int TrainCount => trainCount;
    public float Cooldown => cooldown;
    public int Upkeep => upkeep;
    public string IconKey => iconKey;
    public string PrefabKey => prefabKey;
    public string AnimatorControllerKey => animatorControllerKey;
    public string AnimationSpriteKey => animationSpriteKey;
    public bool RequiresOwnership => requiresOwnership;

#if UNITY_EDITOR
    public Sprite EditorIcon => editorIcon;
    public GameObject EditorPrefab => editorPrefab;

    /// <summary>把旧 Inspector 引用迁移为资源键（与 EngineerDefinition 同规则）。</summary>
    private void OnValidate()
    {
        id = id?.Trim();
        displayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(iconKey) && editorIcon) iconKey = editorIcon.name;
        if (string.IsNullOrWhiteSpace(prefabKey) && editorPrefab) prefabKey = editorPrefab.name;
    }
#endif
}

/// <summary>
/// 兵种资源统一预载服务：从 TroopDefinition 与其预制体组件**自动收集**全部依赖并异步预载——
/// 预制体、图标、动画控制器、动画贴图、攻击投射物（GameObjectProperty.atkObj）、
/// 兵种与投射物预制体上的 StageAudio 音效键。重复调用幂等（已缓存直接返回）。
/// 解决两个问题：
/// 1) 设计师无需逐关手拉兵种、扫描资源填清单——按种族一调即可；
/// 2) 玩家携带兵种不一——加载期按“本局实际出现的种族”动态预载（玩家所选种族 + 每支敌方队伍种族）。
/// </summary>
public static class TroopPreloader
{
    /// <summary>预载单个兵种的全部依赖资源（幂等）。</summary>
    public static void Preload(TroopDefinition troop)
    {
        if (troop == null || ResourceManager.Instance == null)
            return;

        ResourceManager.Instance.LoadExtraResourceAsync<GameObject>(troop.PrefabKey, prefab =>
        {
            if (prefab == null)
                return;

            // 自动扫描预制体携带的依赖：攻击投射物（含其音效）+ 本体的 StageAudio 音效键。
            GameObjectProperty prop = prefab.GetComponent<GameObjectProperty>();
            if (prop != null && !string.IsNullOrEmpty(prop.atkObj))
            {
                ResourceManager.Instance.LoadExtraResourceAsync<GameObject>(prop.atkObj, projectile =>
                {
                    if (projectile != null)
                        PreloadStageAudio(projectile.GetComponent<StageAudio>());
                });
            }

            PreloadStageAudio(prefab.GetComponent<StageAudio>());
        });

        ResourceManager.Instance.LoadExtraResourceAsync<Sprite>(troop.IconKey);
        if (!string.IsNullOrEmpty(troop.AnimatorControllerKey))
            ResourceManager.Instance.LoadExtraResourceAsync<RuntimeAnimatorController>(troop.AnimatorControllerKey);
        if (!string.IsNullOrEmpty(troop.AnimationSpriteKey))
            ResourceManager.Instance.LoadExtraResourceAsync<Sprite>(troop.AnimationSpriteKey);
    }

    /// <summary>预载某种族全部兵营（普通/黑暗）可训练的兵种资源。</summary>
    public static void PreloadRaceTroops(string raceId)
    {
        if (string.IsNullOrEmpty(raceId) || ResourceManager.Instance == null)
            return;

        PreloadBuildingTroops(raceId, BuildingType.Barracks);
        PreloadBuildingTroops(raceId, BuildingType.DarkBarracks);
    }

    /// <summary>
    /// 按敌方队伍配置预载本队兵种资源：
    /// troopUnlocks 为空 = 本局全部兵种可用 → 整族预载；
    /// 填写了具体兵种 = 本局只能生产列表内兵种 → 只预载白名单里的兵种（省资源）。
    /// </summary>
    public static void PreloadTeamTroops(EnemyTeamConfig team)
    {
        if (team == null || string.IsNullOrEmpty(team.raceId) || ResourceManager.Instance == null)
            return;

        if (team.troopUnlocks == null || team.troopUnlocks.Count == 0)
        {
            PreloadRaceTroops(team.raceId);
            return;
        }

        // 白名单集合（按 TroopDefinition.Id 匹配）。
        var allowed = new HashSet<string>();
        for (int i = 0; i < team.troopUnlocks.Count; i++)
        {
            EnemyTroopUnlock unlock = team.troopUnlocks[i];
            if (unlock != null && !string.IsNullOrEmpty(unlock.troopId))
                allowed.Add(unlock.troopId);
        }

        PreloadAllowedTroops(team.raceId, BuildingType.Barracks, allowed);
        PreloadAllowedTroops(team.raceId, BuildingType.DarkBarracks, allowed);
    }

    private static void PreloadAllowedTroops(string raceId, BuildingType type, HashSet<string> allowed)
    {
        GameObject prefab = BuildingButton.TryResolveBuilding(raceId, type);
        if (prefab == null)
            return;

        BuildingTraining training = prefab.GetComponent<BuildingTraining>();
        TroopDefinition[] troops = training != null ? training.Troops : null;
        if (troops == null)
            return;

        for (int i = 0; i < troops.Length; i++)
        {
            TroopDefinition troop = troops[i];
            if (troop != null && allowed.Contains(troop.Id))
                Preload(troop);
        }
    }

    private static void PreloadBuildingTroops(string raceId, BuildingType type)
    {
        GameObject prefab = BuildingButton.TryResolveBuilding(raceId, type);
        if (prefab == null)
            return;

        BuildingTraining training = prefab.GetComponent<BuildingTraining>();
        TroopDefinition[] troops = training != null ? training.Troops : null;
        if (troops == null)
            return;

        for (int i = 0; i < troops.Length; i++)
            Preload(troops[i]);
    }

    /// <summary>预载 StageAudio 组件上的全部非空音效键。</summary>
    private static void PreloadStageAudio(StageAudio stageAudio)
    {
        if (stageAudio == null || ResourceManager.Instance == null)
            return;

        PreloadAudio(stageAudio.audioKey1);
        PreloadAudio(stageAudio.audioKey2);
        PreloadAudio(stageAudio.audioKey3);
        PreloadAudio(stageAudio.audioKey4);
        PreloadAudio(stageAudio.audioKey5);
        PreloadAudio(stageAudio.audioKey6);
        PreloadAudio(stageAudio.audioKey7);
        PreloadAudio(stageAudio.audioKey8);
    }

    private static void PreloadAudio(string key)
    {
        if (!string.IsNullOrEmpty(key))
            ResourceManager.Instance.LoadExtraResourceAsync<AudioClip>(key);
    }
}
