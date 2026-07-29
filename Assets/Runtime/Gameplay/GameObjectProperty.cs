using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 统一管理游戏对象（建筑、角色）的核心属性组件。
/// </summary>
public class GameObjectProperty : MonoBehaviour, IStageComponent
{
    public Action OnAtt;                                           // 攻击时事件，在BuildAI/CharacterAI.ShootProjectile中调用，注意避免循环调用
    public Action OnHitted;                                        // 被击时事件，在BuildHealth/CharacterHealth.OnCollide中调用
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
    [Min(0.1f)]public float antiRepel = 1;                         // 抵抗击退的除数，数值越大实际位移越小。
    public Vector2Int atkRange = Vector2Int.one;                   // 攻击矩形的网格尺寸。
    [ResourceKey(typeof(GameObject))]
    public string atkObj;                                          // 攻击投射物预制体的资源键。

    [Header("额外属性")]
    public float barSustainTime = 2f;                              // 受伤或治疗后血条保持显示的时间。
    public float buildTime = 3f;                                   // 建筑从开工到完成的时间。
    public float moveSpeed = 3f;                                   // 角色每秒移动速度。
    public float suckBlood = 0f;                                   // 攻击吸血比例。
    [Header("实时信息")]
    public int currentHp;                                          // 当前生命值。
    public bool isDead;                                            // 当前是否处于死亡状态。
    public GameObject target;                                      // AI 当前锁定的目标对象。
    public List<Vector2Int> path = new List<Vector2Int>();         // AI 当前尚未走完的网格路径。
    public bool isFacingLeft = false;                              // 角色当前是否面向左侧。
    public bool isAttack = false;                                  // 角色当前是否处于攻击状态。
    public bool isRepel = false;                                   // 角色当前是否正在处理击退。
    public float repelDistance = 0f;                               // 尚未消耗完的带方向击退距离。
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

    [Header("空间属性")]
    public Vector2Int occupySpace = Vector2Int.one;                // 对象在地图上占用的网格宽高。
    [ResourceKey(typeof(GameObject))]
    public string buildAnime;                                      // 建筑施工特效预制体的资源键。

    GameObjectPropertyData recordGOPD;                             // 最近一次应用的静态属性快照，用于恢复默认数据。

    #region 生命周期与回调
    /// <summary>
    /// 对象从池中启用时重置生命、目标、路径、战斗状态、Buff 和增量搜索会话。
    /// </summary>
    private void OnEnable()
    {
        ResetRuntimeState();
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 按静态配置恢复生命值，并清空只应在单次启用周期内存在的运行时状态。
    /// </summary>
    public void ResetRuntimeState()
    {
        currentHp = Mathf.Max(0, maxHp);
        isDead = false;
        target = null;
        path.Clear();
        isFacingLeft = false;
        isAttack = false;
        isRepel = false;
        repelDistance = 0f;
        currentBuff.Clear();
        currentDebuff.Clear();
        currentPathSession = null;
        currentScanSession = null;
    }

    public Type DataType => typeof(GameObjectPropertyData);        // 该组件对应的关卡序列化数据类型。

    /// <summary>
    /// 将可持久化的对象类型、阵营、战斗、移动、占地和资源键配置导出为组件数据。
    /// </summary>
    /// <returns>包含当前静态配置的 <see cref="GameObjectPropertyData"/>。</returns>
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
