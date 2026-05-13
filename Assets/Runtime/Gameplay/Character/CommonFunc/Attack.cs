using UnityEngine;

public class Attack : BehaviourBase
{
    private Vector2Int _myBasePos;
    private Vector2Int _targetBasePos;

    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (prop.target == null)
        {
            return false;
        }

        GameObjectProperty targetProp = prop.target.GetComponent<GameObjectProperty>();
        if (targetProp == null)
        {
            return false;
        }

        // 1. 计算自身的攻击范围矩形
        _myBasePos.x = (int)(self.transform.position.x - prop.occupySpace.x / 2f + 0.5f);
        _myBasePos.y = (int)(self.transform.position.y - prop.occupySpace.y / 2f + 0.5f);

        int rangeStartX = prop.isFacingLeft ? _myBasePos.x - prop.atkRange.x : _myBasePos.x + prop.occupySpace.x;
        int rangeStartY = _myBasePos.y + Mathf.CeilToInt((prop.occupySpace.y - prop.atkRange.y) / 2.0f);
        
        int rangeEndX = rangeStartX + prop.atkRange.x - 1;
        int rangeEndY = rangeStartY + prop.atkRange.y - 1;

        // 2. 计算目标的占用矩形
        _targetBasePos.x = (int)(prop.target.transform.position.x - targetProp.occupySpace.x / 2f + 0.5f);
        _targetBasePos.y = (int)(prop.target.transform.position.y - targetProp.occupySpace.y / 2f + 0.5f);

        int targetEndX = _targetBasePos.x + targetProp.occupySpace.x - 1;
        int targetEndY = _targetBasePos.y + targetProp.occupySpace.y - 1;

        // 3. 矩形重叠判定 (AABB 碰撞检测)
        bool isOverlapX = !(rangeEndX < _targetBasePos.x || rangeStartX > targetEndX);
        bool isOverlapY = !(rangeEndY < _targetBasePos.y || rangeStartY > targetEndY);

        if (isOverlapX && isOverlapY)
        {
            // TODO: 在此执行实际的攻击逻辑
            // Debug.Log($"[Attack] 目标 {prop.target.name} 进入攻击范围！");
            return true;
        }

        return false;
    }
}
