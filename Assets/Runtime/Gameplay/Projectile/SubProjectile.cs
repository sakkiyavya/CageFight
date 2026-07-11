using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SubProjectile : MonoBehaviour
{
    [ResourceKey(typeof(GameObject))]
    public string subProjectilePrefab;

    public virtual void TriggleBehaviour()
    {
        
    }
}
