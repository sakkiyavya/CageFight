using UnityEngine;

public class Move : BehaviourBase
{
    private Vector3 _targetWorldPos;                     // 路径中下一格中心对应的世界坐标。
    private Vector3 _lastPos;                            // 移动前的位置，用于判断水平朝向。
    private Vector2Int _nextCell;                        // 当前路径的第一个待到达网格。
    private SpriteRenderer _spr;                         // 执行者子级的精灵渲染器缓存。
    private GameObjectProperty _prop;                    // 执行者的移动和状态属性。
    Transform _transform;                                // 执行者的变换组件。

    #region 公开接口
    /// <summary>
    /// 缓存执行者的变换、属性和子级精灵渲染器。
    /// </summary>
    /// <param name="self">执行移动行为的角色对象。</param>
    /// <param name="prop">包含路径、速度和朝向状态的角色属性。</param>
    /// <param name="health">角色生命组件；当前移动逻辑不使用。</param>
    public override void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        _spr = self.GetComponentInChildren<SpriteRenderer>();
        _transform = self.transform;
        _prop = prop;
    }


    /// <summary>
    /// 沿路径首个网格中心移动角色，更新水平朝向和显示翻转；
    /// 到达当前格后移除该路径点，并发布移动事件。
    /// </summary>
    /// <param name="self">需要移动的角色对象。</param>
    /// <param name="prop">提供路径、速度、击退和朝向状态的角色属性。</param>
    /// <param name="health">角色生命组件；当前移动逻辑不使用。</param>
    /// <returns>存在路径并执行了移动步骤时返回 <see langword="true"/>。</returns>
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
        float step = prop.moveSpeed * Time.deltaTime;    // 当前帧允许移动的距离。
        _lastPos = self.transform.position;
        self.transform.position = Vector3.MoveTowards(self.transform.position, _targetWorldPos, step);

        // 更新朝向逻辑：素材默认朝右
        if (self.transform.position.x < _lastPos.x)
        {
            prop.isFacingLeft = true;
        }
        else if (self.transform.position.x > _lastPos.x)
        {
            prop.isFacingLeft = false;
        }

        if(_prop.isRepel)
            return true;
        if(_transform)
        {
            _transform.localScale = new Vector3(prop.isFacingLeft ? -1 : 1, 1, 1);
        }

        // 如果足够接近目标格点，则从路径中移除该点，准备前往下一个点
        if (Vector3.Distance(self.transform.position, _targetWorldPos) < 0.05f)
        {
            prop.path.RemoveAt(0);
        }

        _prop.OnMove?.Invoke();
        return true;
    }
    #endregion
}
