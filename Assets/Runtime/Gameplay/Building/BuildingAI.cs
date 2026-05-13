using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class BuildingAI : MonoBehaviour
{
    private GameObjectProperty _prop;

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }

    // TODO: 实现在此处处理建筑的 AI 逻辑。

    protected virtual void AIBehaviour()
    {
        
    }

    void Update()
    {
        AIBehaviour();
    }

}
