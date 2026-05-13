using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterAI : MonoBehaviour
{
    public List<BehaviourBase> Behaviours = new List<BehaviourBase>();
    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
    }

    public float MoveSpeed => _prop.moveSpeed;

    protected virtual void AIBehaviour()
    {
        foreach (var behaviour in Behaviours)
        {
            behaviour.AIBehaviour(gameObject, _prop, _health);
        }
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
