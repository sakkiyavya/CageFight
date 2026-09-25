using System.Collections.Generic;
using UnityEngine;

public class FindEnemy : BehaviourBase
{
    [SerializeField] private bool excludeBuildings;                                                                                                               // 索敌时排除建筑（如 Cat addict 不可攻击建筑）。
    [SerializeField, Min(0.5f), Tooltip("已有目标时的后台重扫间隔：发现显著更近的敌人自动换目标")]
    private float rescanInterval = 2.5f;                                                                                                                         // 后台重扫周期（秒）。

    private List<GameObjectProperty> _enemiesCache = new List<GameObjectProperty>();                                                                          // 本轮扫描筛选出的敌方目标。
    private Vector2Int _myPos;                                                                                                                                // 执行者用于距离排序的网格坐标。
    private float _lastScanTime = float.NegativeInfinity;                                                                                                     // 上一次开始全图扫描的时间。

    /// <summary>池化复用复位：扫描计时回到初始值，下一次获得目标后立即开始后台重扫。</summary>
    private void OnEnable()
    {
        _lastScanTime = float.NegativeInfinity;
    }

    #region 公开接口
    /// <summary>
    /// 设置索敌是否排除建筑（供运行时的单位机制按需配置）。
    /// </summary>
    public void SetExcludeBuildings(bool value)
    {
        excludeBuildings = value;
    }

    /// <summary>
    /// 索敌主入口：
    /// - 已有有效目标 → 周期后台重扫（不阻塞移动/攻击），发现显著更近的敌人自动替换；
    /// - 无目标 → 全图分帧扫描（阻塞移动，保持原节奏），扫完选最近目标；
    /// - 全部候选不可用 → 向敌方大本营行军（保持推进，不原地罚站；后台重扫会在敌人出现后换回）。
    /// </summary>
    /// <param name="self">正在寻找目标的角色对象。</param>
    /// <param name="prop">保存阵营、目标和增量扫描会话的角色属性。</param>
    /// <param name="health">角色生命组件；当前索敌逻辑不使用。</param>
    /// <returns>本帧是否启动或推进了索敌流程。</returns>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (prop.target != null)
        {
            GameObjectProperty currentTarget = prop.target.GetComponent<GameObjectProperty>();

            if (currentTarget != null && !currentTarget.isDead && !currentTarget.isUntargetable)
            {
                // 已有有效目标：周期后台重扫（不阻塞），发现更近敌人由 ProcessScanResult 替换。
                if (Time.time - _lastScanTime >= rescanInterval)
                {
                    _lastScanTime = Time.time;
                    if (prop.currentScanSession == null)
                        prop.currentScanSession = new EnemyScanSession();
                }

                if (prop.currentScanSession != null)
                {
                    prop.currentScanSession.Scan(30);
                    if (prop.currentScanSession.isFinished)
                        ProcessScanResult(self, prop);
                }

                return false;   // 不阻塞：让 FindPath/Move 继续用当前目标行动。
            }

            prop.target = null;
        }

        // 无目标：全图扫描（阻塞移动，保持原有节奏）。
        if (prop.currentScanSession == null)
        {
            prop.currentScanSession = new EnemyScanSession();
        }

        prop.currentScanSession.Scan(30);
        if (prop.currentScanSession.isFinished)
        {
            ProcessScanResult(self, prop);
        }
        return true;
    }
    #endregion

    #region 游戏逻辑
    /// <summary>
    /// 从完成的扫描会话中排除自身、同阵营与拉黑对象，按曼哈顿距离排序：
    /// 有候选 → 选最近者（已有目标时只在“显著更近”时替换，避免来回切换）；
    /// 无候选 → 向敌方大本营行军（保持推进，不原地罚站）。
    /// </summary>
    /// <param name="self">正在选择目标的角色对象。</param>
    /// <param name="prop">提供阵营、扫描结果并接收最终目标的角色属性。</param>
    private void ProcessScanResult(GameObject self, GameObjectProperty prop)
    {
        _myPos.x = (int)(self.transform.position.x - 0.5f + 0.5f);
        _myPos.y = (int)(self.transform.position.y - 0.5f + 0.5f);

        _enemiesCache.Clear();

        foreach (var otherProp in prop.currentScanSession.foundEnemies)
        {
            if (otherProp == null || otherProp.gameObject == self) continue;

            // 寻路失败拉黑中的目标暂时跳过（FindPath 写入 unreachableTarget/unreachableUntil）。
            if (prop.unreachableTarget != null &&
                prop.unreachableTarget == otherProp.gameObject &&
                Time.time < prop.unreachableUntil)
                continue;

            if (otherProp.side != prop.side &&
                !otherProp.isDead &&
                !otherProp.isUntargetable &&
                (!excludeBuildings || (otherProp.objectType & GameObjectType.Building) == 0))
            {
                _enemiesCache.Add(otherProp);
            }
        }

        prop.currentScanSession = null;

        if (_enemiesCache.Count > 0)
        {
            _enemiesCache.Sort(SortByDistance);
            GameObjectProperty nearest = _enemiesCache[0];
            int nearestDist = DistanceTo(nearest);

            // 已有有效目标时：仅当新候选显著更近（迟滞 2 格）才替换，避免两个目标间来回切。
            GameObjectProperty currentProp =
                prop.target != null ? prop.target.GetComponent<GameObjectProperty>() : null;
            if (currentProp != null && !currentProp.isDead && !currentProp.isUntargetable)
            {
                if (nearestDist + 2 >= DistanceTo(currentProp))
                    return;
            }

            prop.target = nearest.gameObject;
            return;
        }

        // 没有可用敌人：向敌方大本营行军（保持推进节奏，不原地罚站）。
        if (prop.target == null)
        {
            GameObject marchBase = TeamRules.IsEnemySide(prop.side)
                ? StageObjectInstantiator.LastFriendlyMainBase
                : StageGoalTracker.LastEnemyMainBase;

            if (marchBase != null)
            {
                GameObjectProperty baseProp = marchBase.GetComponent<GameObjectProperty>();
                if (baseProp == null || (!baseProp.isDead && !baseProp.isUntargetable))
                    prop.target = marchBase;
            }
        }
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 比较两个候选目标到执行者网格位置的曼哈顿距离，用于由近到远排序。
    /// </summary>
    /// <param name="a">第一个候选目标。</param>
    /// <param name="b">第二个候选目标。</param>
    /// <returns>符合列表排序约定的距离比较结果。</returns>
    private int SortByDistance(GameObjectProperty a, GameObjectProperty b)
    {
        return DistanceTo(a).CompareTo(DistanceTo(b));
    }

    /// <summary>候选目标到执行者的曼哈顿距离（网格）。</summary>
    private int DistanceTo(GameObjectProperty other)
    {
        int ox = (int)(other.transform.position.x - 0.5f + 0.5f);
        int oy = (int)(other.transform.position.y - 0.5f + 0.5f);
        return Mathf.Abs(ox - _myPos.x) + Mathf.Abs(oy - _myPos.y);
    }
    #endregion
}
