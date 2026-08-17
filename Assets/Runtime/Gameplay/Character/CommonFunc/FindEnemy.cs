using System.Collections.Generic;
using UnityEngine;

public class FindEnemy : BehaviourBase
{
    [SerializeField] private bool excludeBuildings;                                                                                                               // 索敌时排除建筑（如 Cat addict 不可攻击建筑）。
    private List<GameObjectProperty> _enemiesCache = new List<GameObjectProperty>();                                                                          // 本轮扫描筛选出的敌方目标。
    private Vector2Int _myPos;                                                                                                                                // 执行者用于距离排序的网格坐标。

    #region 公开接口
    /// <summary>
    /// 设置索敌是否排除建筑（供运行时的单位机制按需配置）。
    /// </summary>
    public void SetExcludeBuildings(bool value)
    {
        excludeBuildings = value;
    }
    /// <summary>
    /// 在角色没有目标时创建或继续分帧全图扫描；扫描完成后筛选敌方对象并选择近距离目标。
    /// </summary>
    /// <param name="self">正在寻找目标的角色对象。</param>
    /// <param name="prop">保存阵营、目标和增量扫描会话的角色属性。</param>
    /// <param name="health">角色生命组件；当前索敌逻辑不使用。</param>
    /// <returns>本帧是否启动或推进了索敌流程。</returns>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        // 否定条件：如果已经有目标，则不需要重新寻敌
        if (prop.target != null)
        {
            GameObjectProperty currentTarget =
                prop.target.GetComponent<GameObjectProperty>();

            if (currentTarget != null &&
                !currentTarget.isDead &&
                !currentTarget.isUntargetable)
            {
                return false;
            }

            prop.target = null;
        }

        // 如果没有正在进行的索敌，则开启新的索敌会话
        if (prop.currentScanSession == null)
        {
            prop.currentScanSession = new EnemyScanSession();
        }

        // 1. 执行增量索敌扫描 (每帧最多 10 步)
        if (prop.currentScanSession != null)
        {
            prop.currentScanSession.Scan(10);
            
            if (prop.currentScanSession.isFinished)
            {
                ProcessScanResult(self, prop);
            }
            return true; 
        }

        return false;
    }
    #endregion

    #region 游戏逻辑
    /// <summary>
    /// 从完成的扫描会话中排除自身和同阵营对象，按曼哈顿距离排序，
    /// 再从最近的最多三个候选中随机选择一个作为目标。
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
            // 按曼哈顿距离排序
            _enemiesCache.Sort(SortByDistance);

            int count = Mathf.Min(3, _enemiesCache.Count);                                                                                                    // 可参与随机选择的最近候选数量。
            GameObjectProperty targetProp = _enemiesCache[Random.Range(0, count)];                                                                            // 随机选中的敌方属性。
            prop.target = targetProp.gameObject;
            
            // Debug.Log($"[FindEnemy] 索敌成功，锁定目标: {prop.target.name}");
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
        int distA = Mathf.Abs((int)(a.transform.position.x - 0.5f + 0.5f) - _myPos.x) + Mathf.Abs((int)(a.transform.position.y - 0.5f + 0.5f) - _myPos.y);    // 候选 A 的曼哈顿距离。
        int distB = Mathf.Abs((int)(b.transform.position.x - 0.5f + 0.5f) - _myPos.x) + Mathf.Abs((int)(b.transform.position.y - 0.5f + 0.5f) - _myPos.y);    // 候选 B 的曼哈顿距离。
        return distA.CompareTo(distB);
    }
    #endregion
}
