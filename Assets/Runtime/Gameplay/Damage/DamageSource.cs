using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageSource : MonoBehaviour
{
    public Damage damage = Damage.DefaultDamage;
    public float sustainTime = 0.2f;
    float _remainTime = 0f;

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

        damage.target = collision.gameObject;
        c.OnCollide(damage);
    }

    public void Init()
    {
        _remainTime = sustainTime;
    }

    void Update()
    {
        _remainTime -= Time.deltaTime;
        if(_remainTime <= 0)
        {
            GameObjectPool.Instance.Release(gameObject);
        }
    }
}
