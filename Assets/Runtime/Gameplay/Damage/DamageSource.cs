using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageSource : MonoBehaviour
{
    
    public Damage damage = Damage.DefaultDamage;
    void Start()
    {
        if(damage.source == null)
            damage.source = gameObject;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        ICollide c = collision.GetComponent<ICollide>();
        if(c == null)
            return;

        c.OnCollide(damage);
    }
}
