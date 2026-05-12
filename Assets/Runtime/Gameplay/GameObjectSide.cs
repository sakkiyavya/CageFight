using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class GameObjectSide : MonoBehaviour
{
    private GameObjectProperty _prop;
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }

    public int Side => _prop != null ? _prop.side : 0;
}

