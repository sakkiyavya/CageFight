using UnityEngine;

public class BuildingAI : MonoBehaviour, ILevelComponent
{
    #region ILevelComponent实现
    public System.Type DataType => typeof(BuildingAIData);

    public ComponentData ExtractData()
    {
        return new BuildingAIData
        {
        };
    }

    public void ApplyData(ComponentData data)
    {
        if (data is BuildingAIData aiData)
        {
        }
    }
    #endregion

    // TODO: 实现在此处处理建筑的 AI 逻辑。

    protected virtual void AIBehaviour()
    {
        
    }

    void Update()
    {
        AIBehaviour();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var otherSide = other.GetComponent<GameObjectSide>();
        var mySide = GetComponent<GameObjectSide>();

        if (otherSide == null || mySide == null || otherSide.Side == mySide.Side)
        {
            if (otherSide != null && mySide != null)
                Debug.Log($"[BuildingAI] 发现相同阵营对象: {other.name}，忽略碰撞。");
            return;
        }

        Debug.Log($"[BuildingAI] 发现敌方对象: {other.name}，准备触发防御或反击！");
        // TODO: 在此处处理建筑的自动反应逻辑
    }
}
