using UnityEngine;

public class Move : BehaviourBase
{
    private Vector3 _targetWorldPos;
    private Vector3 _lastPos;
    private Vector2Int _nextCell;
    private SpriteRenderer _spr;

    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        // 否定条件占位符：如果没有路径数据，则无法移动
        if (prop.path == null || prop.path.Count == 0)
        {
            return false;
        }

        // 获取路径中的下一个格点
        _nextCell = prop.path[0];
        // 计算格点中心的世界坐标 (0.5f 偏移)
        _targetWorldPos.x = _nextCell.x + 0.5f;
        _targetWorldPos.y = _nextCell.y + 0.5f;
        _targetWorldPos.z = self.transform.position.z;
        
        // 根据 speed 进行八向移动
        float step = prop.moveSpeed * Time.deltaTime;
        _lastPos = self.transform.position;
        self.transform.position = Vector3.MoveTowards(self.transform.position, _targetWorldPos, step);

        // 更新朝向逻辑：素材默认朝左
        if (self.transform.position.x < _lastPos.x)
        {
            prop.isFacingLeft = true;
        }
        else if (self.transform.position.x > _lastPos.x)
        {
            prop.isFacingLeft = false;
        }

        // 应用视觉翻转
        if (_spr == null) _spr = self.GetComponentInChildren<SpriteRenderer>();
        if (_spr != null)
        {
            _spr.flipX = !prop.isFacingLeft;
        }

        // 如果足够接近目标格点，则从路径中移除该点，准备前往下一个点
        if (Vector3.Distance(self.transform.position, _targetWorldPos) < 0.05f)
        {
            prop.path.RemoveAt(0);
        }

        return true;
    }
}
