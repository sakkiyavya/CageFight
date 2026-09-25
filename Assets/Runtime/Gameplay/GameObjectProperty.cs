using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一管理游戏对象（建筑、角色）的核心属性组件。
/// </summary>
public class GameObjectProperty : MonoBehaviour, IStageComponent
{
    public Action OnAtt;                                           // 攻击时事件，在BuildAI/CharacterAI.ShootProjectile中调用，注意避免循环调用
    public Action<Damage> OnHitted;                                // 被击时事件，在BuildHealth/CharacterHealth.OnCollide中调用
    public Action OnMove;                                          // 移动时触发，在Move.AIBehaviour中调用
    [Header("对象种类")]
    public GameObjectType objectType;                              // 当前对象是角色还是建筑。
    public int side = 0;                                           // 阵营编号，相同编号视为友方。

    [Header("基础属性")]
    public int maxHp = 100;                                        // 最大生命值。
    public int defense = 10;                                       // 物理防御力。
    public int magicDefense = 5;                                   // 魔法防御力。

    [Header("攻击属性")]
    public bool isRemoteAtk = false;                               // 是否使用远程攻击。
    public int atk = 10;                                           // 物理攻击力。
    public float atkRate = 1f;                                     // 攻击频率或攻击速度系数。
    public int magicAtk = 5;                                       // 魔法攻击力。
    public float repel = 1;                                        // 攻击命中时施加的击退强度。
    [Min(0.1f)] public float antiRepel = 1;                         // 抵抗击退的除数，数值越大实际位移越小。
    public Vector2Int atkRange = Vector2Int.one;                   // 攻击矩形的网格尺寸。
    [ResourceKey(typeof(GameObject))]
    public string atkObj;                                          // 攻击投射物预制体的资源键。

    [Header("战斗修正（由 Buff 管理，默认无修正）")]
    public float damageMultiplier = 1f;                            // 造成的伤害倍率（如愤怒增伤）。
    public float damageTakenMultiplier = 1f;                       // 受到的伤害倍率（如愤怒受伤增加）。
    public float critChance = 0f;                                  // 造成 200% 伤害的概率（如愤怒暴击）。
    public int armor = 0;                                          // 免伤值（护甲 Buff 管理，伤害结算时直接扣除）。
    public float damageReduction = 0f;                             // 最终伤害减免比例 0~1（勇气 Buff 管理，结算最后乘算）。
    public int blockHits = 0;                                      // 剩余格挡次数（坚毅 Buff 管理，受伤时伤害变 1 且免疫击退并消耗一层）。
    public float repelTakenMultiplier = 1f;                        // 受到击退的倍率（重伤 Buff 管理，默认 1，受伤结算时乘算）。
    public float healTakenMultiplier = 1f;                        // 受到治疗的倍率（重伤 Buff 管理，默认 1，治疗结算时乘算）。
    public float missChance = 0f;                                 // 未命中率 0~1（当前值，含基础值与目盲 Buff 叠加，攻击结算时掷骰落空）。
    public float baseMissChance = 0f;                             // 基础未命中率（静态配置，如 Cat dad 50%；运行时重置回此值，目盲 Buff 叠加时保底）。

    [Header("额外属性")]
    public float barSustainTime = 2f;                              // 受伤或治疗后血条保持显示的时间。
    public float buildTime = 3f;                                   // 建筑从开工到完成的时间。
    public float moveSpeed = 3f;                                   // 角色每秒移动速度。
    public float suckBlood = 0f;                                   // 攻击吸血比例。
    [Header("实时信息")]
    public int currentHp;                                          // 当前生命值。
    public bool isDead;                                            // 当前是否处于死亡状态。
    public bool isUntargetable;                                    // 当前是否不可被索敌和攻击。
    public GameObject target;                                      // AI 当前锁定的目标对象。
    public List<Vector2Int> path = new List<Vector2Int>();         // AI 当前尚未走完的网格路径。
    public bool isFacingLeft = false;                              // 角色当前是否面向左侧。
    public bool isAttack = false;                                  // 角色当前是否处于攻击状态。
    public bool isRepel = false;                                   // 角色当前是否正在处理击退。
    public float repelDistance = 0f;                               // 尚未消耗完的带方向击退距离。
    [NonSerialized] public Vector3 repelStart;
    [NonSerialized] public Vector3 repelControl;
    [NonSerialized] public Vector3 repelEnd;
    [NonSerialized] public float repelElapsed;
    [NonSerialized] public float repelDuration;
    [NonSerialized] public bool repelInitialized;
    /// <summary>由 StageAudio 在运行时注入，按 audioKey 顺序存放加载好的音频片段</summary>
    public List<AudioClip> audioClips = new List<AudioClip>();     // StageAudio 按资源键顺序注入的运行时音频片段。
    // 攻击范围的世界坐标（左下角和右上角），由 CharacterBase 每帧更新
    public Vector2Int atkRangeMin;                                 // 当前世界攻击矩形的最小网格坐标。
    public Vector2Int atkRangeMax;                                 // 当前世界攻击矩形的最大网格坐标。
    public List<BuffBase> currentBuff = new List<BuffBase>();      // 当前生效的增益 Buff。
    public List<BuffBase> currentDebuff = new List<BuffBase>();    // 当前生效的减益 Buff。

    // AI 增量搜索会话
    public AStarUtility.PathSearchSession currentPathSession;      // 跨帧推进的当前 A* 寻路会话。
    public EnemyScanSession currentScanSession;                    // 跨帧推进的当前全图索敌会话。
    [NonSerialized] public GameObject unreachableTarget;           // 寻路失败后临时拉黑的目标（避免反复选同一不可达目标）。
    [NonSerialized] public float unreachableUntil;                 // 拉黑截止时间（Time.time 基准）。

    [Header("空间属性")]
    public Vector2Int occupySpace = Vector2Int.one;                // 对象在地图上占用的网格宽高。
    [ResourceKey(typeof(GameObject))]
    public string buildAnime;                                      // 建筑施工特效预制体的资源键。

    [Header("等级上下文（1 起；由生成方注入，决定等级缩放与 Buff 等级）")]
    [Min(1)] public int barracksLevel = 1;                         // 普通兵营等级（兵营建筑与其训练兵种的生命/攻击/魔攻缩放）。
    [Min(1)] public int darkBarracksLevel = 1;                     // 黑暗兵营等级（预留：黑暗兵营及其兵种）。
    [Min(1)] public int sentryTowerLevel = 1;                      // 哨塔等级（哨塔血量与伤害）。
    [Min(1)] public int defenseMagicLevel = 1;                     // 防御魔法等级 = 防御类 Buff 的等级。
    [Min(1)] public int attackMagicLevel = 1;                      // 攻击魔法等级 = 攻击类 Buff 的等级。

    [Header("AI 定位（兵种出生时由兵营按 TroopDefinition 注入）")]
    public TroopTag troopTag = TroopTag.None;                      // 兵种定位标签（敌方 AI 判定作用/转产克制）。
    [Min(0f)] public float threatScore = 1f;                       // 对建筑的威胁权重（敌方 AI 威胁评估）。

    GameObjectPropertyData recordGOPD;                             // 最近一次应用的静态属性快照，用于恢复默认数据。

    [NonSerialized] private int _levelBaseMaxHp;                   // 等级缩放前的基准最大生命（Awake/ApplyData 时记录）。
    [NonSerialized] private int _levelBaseAtk;                     // 等级缩放前的基准攻击力。
    [NonSerialized] private int _levelBaseMagicAtk;                // 等级缩放前的基准魔法攻击力。

    #region 生命周期与回调
    /// <summary>
    /// 对象从池中启用时重置生命、目标、路径、战斗状态、Buff 和增量搜索会话。
    /// </summary>
    private void OnEnable()
    {
        ResetRuntimeState();
    }

    /// <summary>记录等级缩放的基准值（实例首次生成 / 应用静态数据时调用）。</summary>
    private void Awake()
    {
        CaptureLevelBase();
    }
    #endregion

    #region 公开接口
    /// <summary>开始一次带轻微上抛弧线的击退。</summary>
    public void StartRepel(Vector3 start, float signedDistance)
    {
        repelDistance = signedDistance;
        repelStart = start;
        repelEnd = start + Vector3.right * signedDistance;
        repelControl = (repelStart + repelEnd) * .5f + Vector3.up *
            Mathf.Clamp(Mathf.Abs(signedDistance) * .35f, .12f, .8f);
        repelElapsed = 0f;
        repelDuration = Mathf.Clamp(.1f + Mathf.Abs(signedDistance) * .045f, .12f, .32f);
        repelInitialized = true;
        isRepel = Mathf.Abs(signedDistance) > .001f;
    }

    /// <summary>
    /// 按静态配置恢复生命值，并清空只应在单次启用周期内存在的运行时状态。
    /// </summary>
    public void ResetRuntimeState()
    {
        currentHp = Mathf.Max(0, maxHp);
        isDead = false;
        isUntargetable = false;
        target = null;
        path.Clear();
        if (recordGOPD != null)
            isFacingLeft = recordGOPD.isFacingLeft;
        isAttack = false;
        isRepel = false;
        repelDistance = 0f;
        repelElapsed = 0f;
        repelDuration = 0f;
        repelInitialized = false;
        // 先逐一撤销各状态实例的实际效果（数值、修正器、视觉、计时），再清空登记列表。
        // 只清列表不清实例，会在对象池复用（重新召唤同一单位）时继承上一轮的 Buff，
        // 且因登记丢失、实例不再受任何到期/取消管理，临时 Buff 会表现为"永久"。
        for (int i = currentBuff.Count - 1; i >= 0; i--)
        {
            if (currentBuff[i] != null)
                currentBuff[i].CancelBuff(this);
        }
        for (int i = currentDebuff.Count - 1; i >= 0; i--)
        {
            if (currentDebuff[i] != null)
                currentDebuff[i].CancelBuff(this);
        }
        currentBuff.Clear();
        currentDebuff.Clear();
        damageMultiplier = 1f;
        damageTakenMultiplier = 1f;
        critChance = 0f;
        armor = 0;
        damageReduction = 0f;
        blockHits = 0;
        repelTakenMultiplier = 1f;
        healTakenMultiplier = 1f;
        missChance = baseMissChance;
        currentPathSession = null;
        currentScanSession = null;
        unreachableTarget = null;      // 拉黑目标随对象池复用复位。
        unreachableUntil = 0f;
        troopTag = TroopTag.None;      // AI 定位标签随对象池复用复位（出生时由兵营重新注入）。
        threatScore = 1f;
    }

    public Type DataType => typeof(GameObjectPropertyData);        // 该组件对应的关卡序列化数据类型。

    /// <summary>
    /// 将可持久化的对象类型、阵营、战斗、移动、占地和资源键配置导出为组件数据。
    /// </summary>
    /// <returns>包含当前静态配置的 <see cref="GameObjectPropertyData"/>。</returns>
    public void CopyPersistentDataTo(GameObjectProperty target)
    {
        if (target == null)
            return;

        target.objectType = objectType;
        target.side = side;
        target.maxHp = maxHp;
        target.defense = defense;
        target.magicDefense = magicDefense;
        target.isRemoteAtk = isRemoteAtk;
        target.atk = atk;
        target.atkRate = atkRate;
        target.magicAtk = magicAtk;
        target.repel = repel;
        target.antiRepel = antiRepel;
        target.atkRange = atkRange;
        target.atkObj = atkObj;
        target.barSustainTime = barSustainTime;
        target.buildTime = buildTime;
        target.moveSpeed = moveSpeed;
        target.suckBlood = suckBlood;
        target.occupySpace = occupySpace;
        target.buildAnime = buildAnime;
        target.isFacingLeft = isFacingLeft;
        target.audioClips.Clear();
        target.audioClips.AddRange(audioClips);
        target.RecordData(target.ExtractData() as GameObjectPropertyData);
        target.ResetRuntimeState();
    }

    public ComponentData ExtractData()
    {
        return new GameObjectPropertyData
        {
            objectType = this.objectType,
            side = this.side,
            maxHp = this.maxHp,
            defense = this.defense,
            magicDefense = this.magicDefense,
            atk = this.atk,
            atkRate = this.atkRate,
            magicAtk = this.magicAtk,
            atkRange = this.atkRange,
            isRemoteAtk = this.isRemoteAtk,
            isFacingLeft = this.isFacingLeft,
            occupySpace = this.occupySpace,
            barSustainTime = this.barSustainTime,
            buildTime = this.buildTime,
            moveSpeed = this.moveSpeed,
            atkObj = this.atkObj,
            buildAnime = this.buildAnime,
            repel = this.repel
        };
    }

    /// <summary>
    /// 从对象属性数据恢复全部静态配置，记录默认快照并重置运行时状态。
    /// </summary>
    /// <param name="data">期望为 <see cref="GameObjectPropertyData"/> 的关卡组件数据；类型不匹配时忽略。</param>
    public void ApplyData(ComponentData data)
    {
        if (data is GameObjectPropertyData pData)
        {
            this.objectType = pData.objectType;
            this.side = pData.side;
            this.maxHp = pData.maxHp;
            this.defense = pData.defense;
            this.magicDefense = pData.magicDefense;
            this.atk = pData.atk;
            this.atkRate = pData.atkRate;
            this.magicAtk = pData.magicAtk;
            this.atkRange = pData.atkRange;
            this.isRemoteAtk = pData.isRemoteAtk;
            this.isFacingLeft = pData.isFacingLeft;
            this.occupySpace = pData.occupySpace;
            this.barSustainTime = pData.barSustainTime;
            this.buildTime = pData.buildTime;
            this.moveSpeed = pData.moveSpeed;
            this.atkObj = pData.atkObj;
            this.buildAnime = pData.buildAnime;
            this.repel = pData.repel;

            RecordData(pData);
            ResetRuntimeState();
        }
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 深复制一份静态对象属性，用作之后恢复默认配置的快照。
    /// </summary>
    /// <param name="d">需要记录的对象属性数据。</param>
    /// <summary>
    /// 记录等级缩放的基准值（当前静态属性）；ApplyData 写入新静态数据后基准同步更新。
    /// </summary>
    private void CaptureLevelBase()
    {
        _levelBaseMaxHp = maxHp;
        _levelBaseAtk = atk;
        _levelBaseMagicAtk = magicAtk;
    }

    /// <summary>
    /// 按等级缩放生命/攻击/魔攻（1.1^(等级-1)；从基准值计算，可重复调用不叠加）。
    /// 防御/攻速/击退/移速不缩放。供兵种单位出生时按所属兵营等级调用。
    /// 生命经 CharacterHealth 受控 API 写入（规范禁止业务直写 maxHp/currentHp）；
    /// 攻击/魔攻无受控入口，按项目现状直写（与 BuildUP 写 prop.atk 一致）。
    /// </summary>
    public void ApplyLevelScale(int level)
    {
        float scale = LevelScale.Pow(level);
        int newMaxHp = Mathf.Max(1, Mathf.RoundToInt(_levelBaseMaxHp * scale));

        CharacterHealth characterHealth = GetComponent<CharacterHealth>();
        if (characterHealth != null)
        {
            characterHealth.SetMaxHp(newMaxHp);
            characterHealth.SetHpKeepDeadState(newMaxHp);   // 等级注入即满血（出生状态）。
        }
        else
        {
            maxHp = newMaxHp;
            currentHp = Mathf.Max(0, newMaxHp);
        }

        atk = Mathf.Max(1, Mathf.RoundToInt(_levelBaseAtk * scale));
        magicAtk = Mathf.Max(1, Mathf.RoundToInt(_levelBaseMagicAtk * scale));
    }

    void RecordData(GameObjectPropertyData d)
    {
        recordGOPD = new GameObjectPropertyData
        {
            objectType = d.objectType,
            side = d.side,
            maxHp = d.maxHp,
            defense = d.defense,
            magicDefense = d.magicDefense,
            atk = d.atk,
            atkRate = d.atkRate,
            magicAtk = d.magicAtk,
            atkRange = d.atkRange,
            isRemoteAtk = d.isRemoteAtk,
            isFacingLeft = d.isFacingLeft,
            occupySpace = d.occupySpace,
            barSustainTime = d.barSustainTime,
            buildTime = d.buildTime,
            moveSpeed = d.moveSpeed,
            atkObj = d.atkObj,
            buildAnime = d.buildAnime,
            repel = d.repel
        };

        CaptureLevelBase();    // 静态数据更新后同步等级缩放基准。
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 重新应用最近记录的默认属性快照，并重置所有运行时状态。
    /// </summary>
    public void ApplyDefaultData()
    {
        ApplyData(recordGOPD);
    }
    #endregion

}

/// <summary>
/// 等级缩放工具：生命/攻击/魔攻按 1.1^(等级-1) 缩放；等级 ≤1 不缩放。
/// </summary>
public static class LevelScale
{
    public const float PerLevel = 1.1f;

    /// <summary>等级缩放系数：1.1^(等级-1)。</summary>
    public static float Pow(int level)
    {
        return Mathf.Pow(PerLevel, Mathf.Max(0, level - 1));
    }
}

/// <summary>
/// 队伍规则：奇数队 = 友方，偶数队 = 敌方；同奇偶 = 同一阵线。
/// 玩家固定为队伍 1；奇数队（3、5、7…）为 AI 队友/联机预留，偶数队（2、4、6、8）为敌方队伍。
/// </summary>
public static class TeamRules
{
    /// <summary>该队伍编号是否为敌方（偶数队）。</summary>
    public static bool IsEnemySide(int side) => (side & 1) == 0;

    /// <summary>两支队伍是否同属一个阵线（同为奇数或同为偶数）。</summary>
    public static bool IsSameAlliance(int sideA, int sideB) => (sideA & 1) == (sideB & 1);
}
