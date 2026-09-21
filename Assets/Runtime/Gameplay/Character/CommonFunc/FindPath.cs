using UnityEngine;

public class FindPath : BehaviourBase
{
    private Vector2Int _myPos;               // 执行者当前所在的网格坐标。
    private Vector2Int _targetPos;           // 本轮寻路使用的终点坐标。
    private Vector2Int _lastTargetPos;       // 上次观察到的目标格子，不是旧路径终点。
    private GameObject _lastTarget;

    private void OnEnable()
    {
        _lastTarget = null;
    }

    #region 公开接口
    /// <summary>
    /// 检查目标移动是否使旧路径失效，并通过可跨帧的 A* 会话计算到目标的路径。
    /// 每帧最多扩展 30 个节点，成功后把结果写入角色属性。
    /// </summary>
    /// <param name="self">需要寻路的角色对象。</param>
    /// <param name="prop">保存目标、路径和增量 A* 会话的角色属性。</param>
    /// <param name="health">角色生命组件；当前寻路逻辑不使用。</param>
    /// <returns>本帧是否正在处理寻路或刚完成寻路。</returns>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (prop.target == null)
        {
            _lastTarget = null;
            prop.currentPathSession = null;
            return false;
        }

        _myPos = new Vector2Int((int)self.transform.position.x, (int)self.transform.position.y);
        _targetPos = new Vector2Int((int)prop.target.transform.position.x, (int)prop.target.transform.position.y);

        // 只在同一目标换格时判断；保留旧路后也更新观察位置，避免下一帧重复判断。
        if (_lastTarget == prop.target && _lastTargetPos != _targetPos &&
            prop.path != null && prop.path.Count > 0)
        {
            Vector2Int nextCell = prop.path[0];
            int currentDistance = Mathf.Abs(_myPos.x - _targetPos.x) + Mathf.Abs(_myPos.y - _targetPos.y);
            int nextDistance = Mathf.Abs(nextCell.x - _targetPos.x) + Mathf.Abs(nextCell.y - _targetPos.y);
            if (nextDistance > currentDistance)
            {
                prop.path.Clear();
                prop.currentPathSession = null;
            }
        }
        _lastTarget = prop.target;
        _lastTargetPos = _targetPos;

        if (prop.path != null && prop.path.Count > 0)
        {
            return false;
        }

        // 如果没有会话或目标坐标已变，则启动/重启会话
        if (prop.currentPathSession == null || prop.currentPathSession.end != _targetPos)
        {
            prop.currentPathSession = new AStarUtility.PathSearchSession(_myPos, _targetPos, self, prop.target);
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
    #endregion
}
