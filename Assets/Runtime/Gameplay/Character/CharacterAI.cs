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
            if(behaviour.AIBehaviour(gameObject, _prop, _health))
                break;
        }
    }

    void Update()
    {
        AIBehaviour();
    }

}
