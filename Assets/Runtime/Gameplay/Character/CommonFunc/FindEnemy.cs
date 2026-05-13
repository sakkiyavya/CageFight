using System.Collections.Generic;
using UnityEngine;

public class FindEnemy : BehaviourBase
{
    private List<GameObjectProperty> _enemiesCache = new List<GameObjectProperty>();
    private Vector2Int _myPos;

    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        // 否定条件：如果已经有目标，则不需要重新寻敌
        if (prop.target != null)
        {
            return false;
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

    private void ProcessScanResult(GameObject self, GameObjectProperty prop)
    {
        _myPos.x = (int)(self.transform.position.x - 0.5f + 0.5f);
        _myPos.y = (int)(self.transform.position.y - 0.5f + 0.5f);
        
        _enemiesCache.Clear();

        foreach (var otherProp in prop.currentScanSession.foundEnemies)
        {
            if (otherProp == null || otherProp.gameObject == self) continue;

            if (otherProp.side != prop.side)
            {
                _enemiesCache.Add(otherProp);
            }
        }

        prop.currentScanSession = null;

        if (_enemiesCache.Count > 0)
        {
            // 按曼哈顿距离排序
            _enemiesCache.Sort(SortByDistance);

            int count = Mathf.Min(3, _enemiesCache.Count);
            GameObjectProperty targetProp = _enemiesCache[Random.Range(0, count)];
            prop.target = targetProp.gameObject;
            
            Debug.Log($"[FindEnemy] 索敌成功，锁定目标: {prop.target.name}");
        }
    }

    private int SortByDistance(GameObjectProperty a, GameObjectProperty b)
    {
        int distA = Mathf.Abs((int)(a.transform.position.x - 0.5f + 0.5f) - _myPos.x) + Mathf.Abs((int)(a.transform.position.y - 0.5f + 0.5f) - _myPos.y);
        int distB = Mathf.Abs((int)(b.transform.position.x - 0.5f + 0.5f) - _myPos.x) + Mathf.Abs((int)(b.transform.position.y - 0.5f + 0.5f) - _myPos.y);
        return distA.CompareTo(distB);
    }
}
