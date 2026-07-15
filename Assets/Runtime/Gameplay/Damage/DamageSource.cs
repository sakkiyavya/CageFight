using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class DamageSource : MonoBehaviour
{
    public Damage damage = Damage.DefaultDamage;
    public GameObject target;
    public float sustainTime = 0.2f;
    public int collideTimes = 5;
    public bool hasSubProjectile = false;
    float _remainTime = 0f;
    int _remainCollideTime = 0;
    protected virtual void Start()
    {
        if(damage.source == null)
            damage.source = gameObject;
    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if(hasSubProjectile)
            return;
            
        if(_remainCollideTime <= 0)
            return;

        ICollide c = collision.GetComponent<ICollide>();
        if(c == null)
            return;

        if(c.IsFriendly(damage))
            return;

        damage.collideDir = transform.position.x < collision.transform.position.x? 1 : -1;
        damage.target = collision.gameObject;
        c.OnCollide(damage);
        DamageTextPool.Instance.ShowDamage(damage, collision.transform.position + Vector3.up);
        _remainCollideTime--;
        if(_remainCollideTime <= 0)
            GameObjectPool.Instance.Release(gameObject);
    }

    public void Init()
    {
        _remainTime = sustainTime;
        _remainCollideTime = collideTimes;
    }

    protected virtual void Update()
    {
        TimeUpdate();
    }

    protected virtual void TimeUpdate()
    {
        _remainTime -= Time.deltaTime;
        if(_remainTime <= 0)
        {
            GameObjectPool.Instance.Release(gameObject);
        }
    }

    protected virtual void OnEnable()
    {
        Init();
    }
}
