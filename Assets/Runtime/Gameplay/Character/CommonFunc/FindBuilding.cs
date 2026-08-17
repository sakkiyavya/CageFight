using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 建筑专用索敌行为：与 FindEnemy 同构，但只把敌方“建筑”作为目标
/// （如 Fortress 只攻击建筑单位）。接入 CharacterAI.Behaviours 即可替换通用索敌。
/// </summary>
public class FindBuilding : BehaviourBase
{
    private List<GameObjectProperty> _buildingsCache =
        new List<GameObjectProperty>();        // 本轮扫描筛选出的敌方建筑。
    private Vector2Int _myPos;                 // 执行者用于距离排序的网格坐标。

    #region 公开接口
    /// <summary>
    /// 在角色没有目标时创建或继续分帧全图扫描；扫描完成后筛选敌方建筑并选择最近目标。
    /// 已有目标时校验其仍为存活的敌方建筑。
    /// </summary>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (prop.target != null)
        {
            GameObjectProperty currentTarget = prop.target.GetComponent<GameObjectProperty>();
            if (currentTarget != null &&
                !currentTarget.isDead &&
                !currentTarget.isUntargetable &&
                IsBuilding(currentTarget) &&
                currentTarget.side != prop.side)
            {
                return false;
            }

            prop.target = null;
        }

        if (prop.currentScanSession == null)
            prop.currentScanSession = new EnemyScanSession();

        if (prop.currentScanSession != null)
        {
            prop.currentScanSession.Scan(10);

            if (prop.currentScanSession.isFinished)
                ProcessScanResult(self, prop);
            return true;
        }

        return false;
    }
    #endregion

    #region 游戏逻辑
    /// <summary>
    /// 从完成的扫描会话中排除自身、同阵营与非建筑对象，按曼哈顿距离选择最近的敌方建筑。
    /// </summary>
    private void ProcessScanResult(GameObject self, GameObjectProperty prop)
    {
        _myPos.x = (int)(self.transform.position.x - 0.5f + 0.5f);
        _myPos.y = (int)(self.transform.position.y - 0.5f + 0.5f);

        _buildingsCache.Clear();

        foreach (var otherProp in prop.currentScanSession.foundEnemies)
        {
            if (otherProp == null || otherProp.gameObject == self) continue;

            if (otherProp.side != prop.side &&
                !otherProp.isDead &&
                !otherProp.isUntargetable &&
                IsBuilding(otherProp) &&
                otherProp.GetComponent<ICollide>() != null)
            {
                _buildingsCache.Add(otherProp);
            }
        }

        prop.currentScanSession = null;

        if (_buildingsCache.Count == 0)
        {
            prop.target = null;
            return;
        }

        _buildingsCache.Sort(SortByDistance);
        prop.target = _buildingsCache[0].gameObject;
    }

    private bool IsBuilding(GameObjectProperty p)
    {
        return p != null && (p.objectType & GameObjectType.Building) != 0;
    }

    private int SortByDistance(GameObjectProperty a, GameObjectProperty b)
    {
        int distA = Mathf.Abs((int)(a.transform.position.x - 0.5f + 0.5f) - _myPos.x) +
                    Mathf.Abs((int)(a.transform.position.y - 0.5f + 0.5f) - _myPos.y);
        int distB = Mathf.Abs((int)(b.transform.position.x - 0.5f + 0.5f) - _myPos.x) +
                    Mathf.Abs((int)(b.transform.position.y - 0.5f + 0.5f) - _myPos.y);
        return distA.CompareTo(distB);
    }
    #endregion
}
