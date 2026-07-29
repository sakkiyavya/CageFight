using UnityEngine;

public class Attack : BehaviourBase
{
    private Vector2Int _targetBasePos;                                                     // 目标占用矩形的左下网格坐标。
    private GameObjectProperty _prop;                                                      // 初始化时缓存的执行者属性。
    private GameObject _self;                                                              // 初始化时缓存的执行者对象。

    #region 公开接口
    /// <summary>
    /// 缓存攻击行为的执行者对象及其属性组件。
    /// </summary>
    /// <param name="self">执行攻击行为的角色对象。</param>
    /// <param name="prop">执行者的属性和攻击状态。</param>
    /// <param name="health">执行者的生命组件；当前攻击范围判断不使用该参数。</param>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        _prop = prop;
        _self = self;
    }

    /// <summary>
    /// 判断目标占用矩形是否与角色当前世界攻击范围重叠。
    /// 命中范围后设置攻击状态；没有目标、属性无效或范围不重叠时返回失败。
    /// </summary>
    /// <param name="self">执行攻击判断的角色对象；当前实现使用初始化时的缓存。</param>
    /// <param name="prop">包含目标、攻击范围和攻击状态的角色属性。</param>
    /// <param name="health">执行者的生命组件；当前实现不使用。</param>
    /// <returns>角色是否已经处于攻击状态或目标当前位于攻击范围内。</returns>
    public override bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        if(prop.isAttack)
            return true;

        if (prop.target == null)
        {
            return false;
        }

        GameObjectProperty targetProp = prop.target.GetComponent<GameObjectProperty>();    // 目标的占地信息。
        if (targetProp == null)
        {
            return false;
        }

        // 1. 直接读取 prop 中已由 CharacterBase 更新好的攻击范围世界坐标
        int rangeStartX = prop.atkRangeMin.x;                                              // 攻击矩形最小横坐标。
        int rangeStartY = prop.atkRangeMin.y;                                              // 攻击矩形最小纵坐标。
        int rangeEndX = prop.atkRangeMax.x;                                                // 攻击矩形最大横坐标。
        int rangeEndY = prop.atkRangeMax.y;                                                // 攻击矩形最大纵坐标。

        // 2. 计算目标的占用矩形
        _targetBasePos.x = (int)(prop.target.transform.position.x - targetProp.occupySpace.x / 2f + 0.5f);
        _targetBasePos.y = (int)(prop.target.transform.position.y - targetProp.occupySpace.y / 2f + 0.5f);

        int targetEndX = _targetBasePos.x + targetProp.occupySpace.x - 1;                  // 目标占用矩形最大横坐标。
        int targetEndY = _targetBasePos.y + targetProp.occupySpace.y - 1;                  // 目标占用矩形最大纵坐标。

        // 3. 矩形重叠判定 (AABB 碰撞检测)
        bool isOverlapX = !(rangeEndX < _targetBasePos.x || rangeStartX > targetEndX);     // 两个矩形在横轴上是否重叠。
        bool isOverlapY = !(rangeEndY < _targetBasePos.y || rangeStartY > targetEndY);     // 两个矩形在纵轴上是否重叠。

        if (isOverlapX && isOverlapY)
        {
            // TODO: 在此执行实际的攻击逻辑
            // Debug.Log($"[Attack] 目标 {prop.target.name} 进入攻击范围！");
            prop.isAttack = true;
            return true;
        }

        return false;
    }
    #endregion

    // public void ShootProjectile()
    // {
    //     if (_prop == null || string.IsNullOrEmpty(_prop.atkObj) || _self == null) return;

    //     GameObject atkPrefab = ResourceManager.Instance.GetGameObject(_prop.atkObj);
    //     if (atkPrefab == null) return;

    //     GameObject projectile = GameObjectPool.Instance.Get(atkPrefab);
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
