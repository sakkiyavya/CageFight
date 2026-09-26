using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


/// <summary>
/// 关卡全局配置数据
/// </summary>
[Serializable]
public class StageSettings
{
    // 未来可以扩展：例如时间限制、背景音乐、天气环境参数等
}

/// <summary>
/// 收入增长阶段（阶梯增长）：在 [startTime, endTime] 内每 stepInterval 秒，每秒产量增加 stepAmount。
/// 例如 startTime=0、endTime=120、stepInterval=10、stepAmount=10：
/// 前两分钟每 10 秒每秒产量 +10（累计阶梯增长）。
/// </summary>
[Serializable]
public class IncomeGrowthPhase
{
    [Tooltip("阶段开始时间（秒，关卡开始后）")]
    [Min(0)] public float startTime = 0f;

    [Tooltip("阶段结束时间（秒）；小于等于开始时间表示持续到关卡结束")]
    [Min(0)] public float endTime = 0f;

    [Tooltip("每步间隔（秒）")]
    [Min(0.1f)] public float stepInterval = 10f;

    [Tooltip("每步增加的每秒金币量")]
    [Min(0)] public float stepAmount = 10f;
}

/// <summary>敌方 AI 策略人格：龟缩（重经济防守）/均衡/猛攻（重产出进攻）。</summary>
public enum EnemyStrategy
{
    Defensive = 0,
    Balanced = 1,
    Aggressive = 2,
}

/// <summary>敌方阵地槽位：AI 按列表顺序在指定网格建造指定类型建筑。</summary>
[Serializable]
public class EnemyBuildSlot
{
    [Tooltip("要建造的建筑类型")]
    public BuildingType buildingType = BuildingType.Barracks;

    [Tooltip("建筑左下基准网格坐标")]
    public Vector2Int gridPosition = Vector2Int.zero;
}

/// <summary>
/// 敌方队伍兵种白名单条目：本队本局可训练的兵种之一。
/// troopUnlocks 整表为空 = 本局自动解锁兵营全部兵种；
/// 一旦填写了条目，本局就**只能**训练列表内出现的兵种（未列出的兵种不可生产）。
/// </summary>
[Serializable]
public class EnemyTroopUnlock
{
    [Tooltip("兵种稳定 ID（与 TroopDefinition.Id 一致）")]
    public string troopId = string.Empty;

    [Tooltip("本局可训练该兵种的时间门槛（秒，关卡开始后；0 = 开局即可训练）")]
    [Min(0)] public float unlockTime = 0f;
}

/// <summary>
/// 单支敌方队伍配置（队伍 2/4/6/8，最多 4 支）：
/// 队伍 = 种族（决定建筑）→ 建筑（决定兵种）；经济按自己的增长曲线结算；
/// AI 只做“建造 + 生产”两类决策，与玩家同一套规则，不添加单位指令。
/// </summary>
[Serializable]
public class EnemyTeamConfig
{
    [Header("队伍")]
    [Tooltip("队伍编号（偶数队 = 敌方）")]
    [Min(2)] public int teamId = 2;
    [Tooltip("种族 ID（与 RaceDefinition.Id 一致，决定可建建筑清单）")]
    public string raceId = string.Empty;

    [Header("经济")]
    [Min(0)] public int startGold = 200;
    [Min(0)] public float baseGoldPerSecond = 10f;
    public List<IncomeGrowthPhase> incomeGrowth = new List<IncomeGrowthPhase>();
    [Tooltip("预留底线：净收入低于该值时不开启新的维护费来源（0 = 只要求净收入为正）")]
    [Min(0)] public int reserve = 0;
    [Tooltip("防守建筑维护费占净收入的比例上限（回防时不超过，其余留给兵种生产）")]
    [Range(0f, 1f)] public float defenseSpendRatio = 0.4f;
    [Tooltip("盈余持续多少秒才扩张（开新兵营/上兵种）")]
    [Min(0.5f)] public float surplusSeconds = 10f;

    [Header("策略与反应")]
    public EnemyStrategy strategy = EnemyStrategy.Balanced;
    [Tooltip("回防威胁阈值：己方阵地半径内玩家单位威胁值超过该值进入回防态")]
    [Min(0)] public float threatThreshold = 500f;
    [Tooltip("回防半径（格）：以阵地锚点为中心统计玩家威胁的范围")]
    [Min(1)] public float defendRadius = 12f;

    [Header("阵地")]
    [Tooltip("阵地锚点网格坐标（敌方基地/核心区位置；回防统计与建筑计划围绕它）")]
    public Vector2Int homeGridPosition = new Vector2Int(60, 3);
    [Tooltip("建筑槽位：AI 按列表顺序逐格建造（回防态优先哨塔槽，其余状态优先兵营槽）")]
    public List<EnemyBuildSlot> buildSlots = new List<EnemyBuildSlot>();
    [Tooltip("勾选后每局随机打乱槽位建造顺序（增加变化性）")]
    public bool randomizeBuildOrder = true;
    [Tooltip("相邻两次开工的随机间隔（秒；x=最短，y=最长）")]
    public Vector2 buildGapRange = new Vector2(1.5f, 5f);
    [Tooltip("位置随机偏移（格）：在槽位坐标周围 ±N 格内随机偏移（0 = 精确位置）")]
    [Min(0)] public int positionJitter = 1;

    [Header("动态扩张")]
    [Tooltip("兵营总数上限：槽位表用完后，经济允许时在阵地锚点附近动态补建兵营")]
    [Min(0)] public int maxBarracks = 3;
    [Tooltip("哨塔总数上限：槽位表用完后，回防/富余时在阵地锚点附近动态补建哨塔")]
    [Min(0)] public int maxSentries = 2;

    [Header("兵种")]
    [Tooltip("本队本局兵种白名单：留空 = 自动解锁兵营全部兵种；填写后本局只能训练列表内兵种（unlockTime 为该兵种本局可训练的时间门槛，0 = 开局即可）")]
    public List<EnemyTroopUnlock> troopUnlocks = new List<EnemyTroopUnlock>();
}

/// <summary>
/// 导游对话条目：选关时点击该关弹出的一条导游台词。
/// 头像精灵与音频均为资源键（经 ResourceManager 解析，加载期由 GuideDialoguePlayer.Preload 预载）。
/// </summary>
[Serializable]
public class GuideDialogueEntry
{
    [ResourceKey(typeof(Sprite))]
    [Tooltip("导游头像精灵键（SpriteRegistry）；留空则本条不显示头像")]
    public string avatarSpriteKey = string.Empty;

    [TextArea]
    [Tooltip("本条对话文本")]
    public string text = string.Empty;

    [ResourceKey(typeof(AudioClip))]
    [Tooltip("本条对话弹出时播放的音频键（AudioRegistry）；留空静音")]
    public string audioKey = string.Empty;
}

/// <summary>
/// 关卡配置的根数据结构（ScriptableObject）
/// 这是编辑器和运行时唯一共享的核心数据源
/// </summary>
[CreateAssetMenu(fileName = "NewStageConfig", menuName = "StageSystem/Stage Config")]
public class StageConfig : ScriptableObject
{
    [Tooltip("关卡唯一标识 ID")]
    [FormerlySerializedAs("levelId")]
    public int stageId;                                                    // 用于存档、选关和运行时寻址的关卡唯一编号。

    [Tooltip("关卡玩法类型")]
    public UserGlobalInfo.StageType stageType;

    [Tooltip("防守关卡时限（秒）")]
    public float DefenseTime;

    [Tooltip("Stage icon.")]
    public Sprite icon;                                                    // 选关界面用于展示该关卡的图标。

    [Header("选关弹窗展示图")]
    [Tooltip("选中关卡后，弹窗正中间展示的图标（最多 4 个，按顺序从左到右居中横排）。")]
    public List<Sprite> displayIcons = new List<Sprite>();                 // 选中关卡时展示在弹窗正中间的图标。

    [Tooltip("关卡的全局设置")]
    public StageSettings settings;                                         // 该关卡共用的全局规则和环境参数。

    [Tooltip("该关卡内包含的所有物体数据集合")]
    public List<StageObjectData> objects = new List<StageObjectData>();    // 进入关卡时需要实例化的全部对象数据。

    [Header("己方大本营")]
    [Tooltip("构建时根据场景中的己方大本营标记自动生成。启用后，在指定网格位置生成玩家所选种族的大本营。")]
    public bool hasFriendlyMainBaseGridPosition;

    [Tooltip("构建时从己方大本营标记自动扫描的占地区域左下网格坐标；运行时按实际占地尺寸换算为预制体中心位置。")]
    public Vector2Int friendlyMainBaseGridPosition;

    [Tooltip("预制体资源 Key 清单")]
    public List<string> prefabs = new List<string>();                      // 本关卡依赖的预制体资源键集合。

    [Tooltip("音频资源 Key 清单")]
    public List<string> audios = new List<string>();                       // 本关卡依赖的音频资源键集合。

    [Tooltip("纹理资源 Key 清单")]
    public List<string> textures = new List<string>();                     // 本关卡依赖的纹理资源键集合。

    [Tooltip("动画片段资源 Key 清单")]
    public List<string> animationClips = new List<string>();               // 本关卡依赖的动画片段资源键集合。

    [Tooltip("动画控制器资源 Key 清单")]
    public List<string> animatorControllers = new List<string>();          // 本关卡依赖的动画控制器资源键集合。

    [Tooltip("Sprite 资源 Key 清单")]
    public List<string> sprites = new List<string>();                      // 本关卡依赖的精灵资源键集合。

    [Header("经济增长曲线（玩家）")]
    [Tooltip("启用后本关收入按下方曲线增长（基础值 + 各阶段阶梯增量）；关闭时沿用场景 Coins 的时间阶梯配置")]
    public bool useIncomeCurve = false;

    [Header("导游对话")]
    [Tooltip("选关界面点击本关时依次弹出的导游对话（头像/文本/音频按条目配置）；空列表 = 无对话")]
    public List<GuideDialogueEntry> guideDialogues = new List<GuideDialogueEntry>();

    [Tooltip("关卡开始时的基础每秒金币产量")]
    [Min(0)] public float baseGoldPerSecond = 10f;

    [Tooltip("收入增长阶段列表：每阶段内每 stepInterval 秒，每秒产量增加 stepAmount（累计阶梯增长）")]
    public List<IncomeGrowthPhase> incomeGrowth = new List<IncomeGrowthPhase>();

    [Header("本关敌方等级")]
    [Tooltip("敌方普通兵营等级：决定该兵营血量与其训练兵种的生命/攻击/魔攻（1.1^(等级-1) 缩放）")]
    [Min(1)] public int enemyBarracksLevel = 1;

    [Tooltip("敌方黑暗兵营等级：同普通兵营，作用于黑暗兵营及其训练兵种")]
    [Min(1)] public int enemyDarkBarracksLevel = 1;

    [Tooltip("敌方哨塔等级：只影响哨塔的血量与伤害")]
    [Min(1)] public int enemySentryTowerLevel = 1;

    [Tooltip("敌方防御魔法等级 = 敌方防御类 Buff 的等级")]
    [Min(1)] public int enemyDefenseMagicLevel = 1;

    [Tooltip("敌方攻击魔法等级 = 敌方攻击类 Buff 的等级")]
    [Min(1)] public int enemyAttackMagicLevel = 1;

    [Header("敌方队伍（阶段 3：敌方 AI）")]
    [Tooltip("敌方大本营距我方大本营的格数（默认 60；距离代表局内对战规模）")]
    [Min(1)] public int enemyBaseDistance = 60;

    [Tooltip("本关敌方队伍配置（偶数队，最多 4 支）")]
    public List<EnemyTeamConfig> enemyTeams = new List<EnemyTeamConfig>();

    [Header("胜负（阶段 4）")]
    [Tooltip("进攻关时限（秒，0 = 不限时）：到点未摧毁敌方大本营即失败")]
    [Min(0)] public float attackTimeLimit = 180f;

    [Tooltip("Boss 关时限（秒，0 = 不限时）：到点未击败 Boss 即失败")]
    [Min(0)] public float bossTimeLimit = 180f;

    [ResourceKey(typeof(GameObject))]
    [Tooltip("Boss 关的 Boss 预制体资源键（须登记 PrefabRegistry 并按关卡预载）")]
    public string bossPrefabKey = string.Empty;

    [Tooltip("Boss 出生左下基准网格坐标")]
    public Vector2Int bossGridPosition = Vector2Int.zero;
}

