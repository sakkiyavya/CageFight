using UnityEngine;

public class CharacterAI : MonoBehaviour, ILevelComponent
{
    #region ILevelComponent实现
    public System.Type DataType => typeof(CharacterAIData);

    public ComponentData ExtractData()
    {
        return new CharacterAIData
        {
            moveSpeed = this.moveSpeed
        };
    }

    public void ApplyData(ComponentData data)
    {
        if (data is CharacterAIData aiData)
        {
            this.moveSpeed = aiData.moveSpeed;
        }
    }
    #endregion

    [SerializeField]
    [Header("移动速度")]
    private float moveSpeed = 3f; public float MoveSpeed => moveSpeed;

    // TODO: 可以在此处实现基础的 AI 逻辑（如寻路、状态机等）
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
                Debug.Log($"[CharacterAI] 发现相同阵营对象: {other.name}，忽略碰撞。");
            return;
        }

        Debug.Log($"[CharacterAI] 发现敌方对象: {other.name}，准备攻击！");
        // TODO: 在此处触发攻击或交互逻辑
    }
}
