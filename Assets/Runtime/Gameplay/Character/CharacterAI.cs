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
}
