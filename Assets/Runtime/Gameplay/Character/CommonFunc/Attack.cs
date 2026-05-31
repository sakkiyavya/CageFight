using UnityEngine;

public class Attack : BehaviourBase
{
    private Vector2Int _targetBasePos;
    private GameObjectProperty _prop;
    private GameObject _self;

    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        _prop = prop;
        _self = self;
    }

    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if (prop.target == null)
        {
            prop.isAttack = false;
            return false;
        }

        GameObjectProperty targetProp = prop.target.GetComponent<GameObjectProperty>();
        if (targetProp == null)
        {
            prop.isAttack = false;
            return false;
        }

        // 1. 直接读取 prop 中已由 CharacterBase 更新好的攻击范围世界坐标
        int rangeStartX = prop.atkRangeMin.x;
        int rangeStartY = prop.atkRangeMin.y;
        int rangeEndX = prop.atkRangeMax.x;
        int rangeEndY = prop.atkRangeMax.y;

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
            prop.isAttack = true;
            return true;
        }

        prop.isAttack = false;
        return false;
    }

    // public void ShootProjectile()
    // {
    //     if (_prop == null || _prop.atkObj == null || _self == null) return;

    //     GameObject projectile = GameObjectPool.Instance.Get(_prop.atkObj);
    //     if (projectile != null)
    //     {
    //         projectile.transform.position = _self.transform.position;

    //         DamageSource ds = projectile.GetComponent<DamageSource>();
    //         if (ds != null)
    //         {
    //             ds.damage.initialDamage = _prop.atk;
    //             ds.damage.source = _self;
    //             // ds.damage.target = _prop.target;
    //             ds.damage.type = DamageType.normal;
    //         }
    //     }
    // }
}
