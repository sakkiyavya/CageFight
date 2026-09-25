using System.Collections.Generic;
using UnityEngine;

public class FindPath : BehaviourBase
{
    private Vector2Int _myPos;               // 执行者当前所在的网格坐标。
    private Vector2Int _targetPos;           // 本轮寻路使用的终点坐标。
    private Vector2Int _lastTargetPos;       // 上次观察到的目标格子，不是旧路径终点。
    private GameObject _lastTarget;
    private bool _useRangeGoal = true;       // 优先用“射程即目标”盒子；失败/已在盒内时退回精确格。

    private void OnEnable()
    {
        _lastTarget = null;
        _useRangeGoal = true;
    }

    #region 公开接口
    /// <summary>
    /// 检查目标移动是否使旧路径失效，并通过可跨帧的 A* 会话计算到目标的路径。
    /// 每帧最多扩展 30 个节点，成功后把结果写入角色属性。
    /// “射程即目标”：终点 = 目标周围 atkRange 盒子内的任意格——目标周围被围死时，
    /// 单位停在射程内即可开打，不再必须走到目标正旁边（原地罚站的根因之一）；
    /// 近战（atkRange.x=1）盒子退化为精确格。
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

        // 目标更换时恢复“射程即目标”策略。
        if (_lastTarget != prop.target)
            _useRangeGoal = true;
        _lastTarget = prop.target;
        _lastTargetPos = _targetPos;

        if (prop.path != null && prop.path.Count > 0)
        {
            return false;
        }

        // 如果没有会话或目标坐标已变，则启动/重启会话。
        if (prop.currentPathSession == null || prop.currentPathSession.end != _targetPos)
        {
            // 射程盒子：X 方向 ≤ atkRange.x-1，Y 方向 ≤ (atkRange.y-1)/2（向下取整，保守覆盖攻击矩形）。
            Vector2Int extent = _useRangeGoal
                ? new Vector2Int(
                    Mathf.Max(0, prop.atkRange.x - 1),
                    Mathf.Max(0, (prop.atkRange.y - 1) / 2))
                : Vector2Int.zero;
            prop.currentPathSession = new AStarUtility.PathSearchSession(
                _myPos, _targetPos, self, prop.target, extent);
        }

        // 执行增量寻路 (每帧最多 30 步)
        prop.currentPathSession.Search(30);

        if (prop.currentPathSession.isFinished)
        {
            if (prop.currentPathSession.isSuccess)
            {
                List<Vector2Int> result = prop.currentPathSession.resultPath;
                if (result != null && result.Count > 0)
                {
                    prop.path = result;
                }
                else if (_useRangeGoal)
                {
                    // 已在射程盒内（起点即目标）：退回“精确格”寻路，
                    // 让单位走到目标格并转向面对目标，避免背身罚站。
                    _useRangeGoal = false;
                    prop.currentPathSession = null;
                    return true;
                }
                else
                {
                    // 精确格模式下起点即目标（与目标同格）：路径为空，交给 Attack 处理。
                    prop.path = result;
                }
            }
            else
            {
                if (_useRangeGoal)
                {
                    // 射程盒不可达：退回精确格再试一次（近战必须同格攻击）。
                    _useRangeGoal = false;
                    prop.currentPathSession = null;
                    return true;
                }

                // 精确格也不可达（目标被完全围死）：拉黑一段时间并放弃该目标，
                // 让 FindEnemy 改选其它敌人——否则全体兵种会一直对着同一个
                // 不可达目标无限重搜寻路，表现为“所有人卡住不动”。
                if (prop.target != null)
                {
                    prop.unreachableTarget = prop.target;
                    prop.unreachableUntil = Time.time + 4f;
                }
                prop.target = null;
                _lastTarget = null;
            }
            prop.currentPathSession = null;
            return true;
        }

        return true; // 只要寻路在进行中，就返回 true
    }
    #endregion
}
