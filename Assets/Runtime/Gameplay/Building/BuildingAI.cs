using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class BuildingAI : MonoBehaviour
{
    protected GameObjectProperty _prop;    // 建筑的运行时属性和 AI 状态（供 BuildingTowerAI 等子类复用，避免重名字段）。

    #region 初始化回调
    /// <summary>
    /// 缓存同一对象上的建筑属性组件。
    /// </summary>
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }
    #endregion

    // TODO: 实现在此处处理建筑的 AI 逻辑。

    #region AI 行为扩展
    /// <summary>
    /// 为具体建筑提供每帧 AI 行为扩展点；基类不执行任何决策。
    /// </summary>
    protected virtual void AIBehaviour()
    {
        
    }
    #endregion

    #region 帧更新回调
    /// <summary>
    /// 每帧调用可重写的建筑 AI 行为。
    /// </summary>
    void Update()
    {
        AIBehaviour();
    }
    #endregion

}
