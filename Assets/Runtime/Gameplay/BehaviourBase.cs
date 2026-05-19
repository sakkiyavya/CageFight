using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BehaviourBase : MonoBehaviour
{
    public virtual bool AIBehaviour(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        return false;
    }

    public virtual void Init(GameObject self, GameObjectProperty prop, CharacterHealth health)
    {
        
    }
}
