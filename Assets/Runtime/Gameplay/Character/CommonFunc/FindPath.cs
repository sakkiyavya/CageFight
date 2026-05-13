using System.Collections.Generic;
using UnityEngine;

public class FindPath : BehaviourBase
{
    private Vector2Int _currentTargetPos;
    private Vector2Int _myPos;
    private Vector2Int _targetPos;
    private Vector2Int _lastPoint;

    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        // 如果有目标且有路径，检查目标是否移动了位置
        if (prop.target != null && prop.path != null && prop.path.Count > 0)
        {
            _lastPoint = prop.path[prop.path.Count - 1];
            _currentTargetPos.x = (int)(prop.target.transform.position.x - 0.5f + 0.5f); // 简化为 (int)pos.x ? 不，保持 (int)(pos.x - space/2 + 0.5) 逻辑
            // 角色通常占据中心，坐标即为中心。
            // 按照之前的逻辑：GetBasePos = (int)(pos.x - space.x/2f + 0.5f)
            // 如果 space=1,1: (int)(pos.x - 0.5f + 0.5f) = (int)pos.x
            // 这里为了严谨，直接调用一致的逻辑
            _currentTargetPos.x = (int)(prop.target.transform.position.x - 0.5f + 0.5f);
            _currentTargetPos.y = (int)(prop.target.transform.position.y - 0.5f + 0.5f);
            
            // 如果目标位置变了，清除旧路径触发重新寻路
            if (_lastPoint != _currentTargetPos)
            {
                prop.path.Clear();
                prop.currentPathSession = null;
            }
        }

        // 否定条件：如果没有目标，或者已经有有效路径，则不需要寻路
        if (prop.target == null || (prop.path != null && prop.path.Count > 0))
        {
            if (prop.target == null) prop.currentPathSession = null;
            return false;
        }

        _myPos.x = (int)(self.transform.position.x - 0.5f + 0.5f);
        _myPos.y = (int)(self.transform.position.y - 0.5f + 0.5f);
        _targetPos.x = (int)(prop.target.transform.position.x - 0.5f + 0.5f);
        _targetPos.y = (int)(prop.target.transform.position.y - 0.5f + 0.5f);

        // 如果没有会话或目标坐标已变，则启动/重启会话
        if (prop.currentPathSession == null || prop.currentPathSession.end != _targetPos)
        {
            prop.currentPathSession = new AStarUtility.PathSearchSession(_myPos, _targetPos);
        }

        // 执行增量寻路 (每帧最多 30 步)
        prop.currentPathSession.Search(30);

        if (prop.currentPathSession.isFinished)
        {
            if (prop.currentPathSession.isSuccess)
            {
                prop.path = prop.currentPathSession.resultPath;
            }
            prop.currentPathSession = null;
            return true;
        }

        return true; // 只要寻路在进行中，就返回 true
    }
}
