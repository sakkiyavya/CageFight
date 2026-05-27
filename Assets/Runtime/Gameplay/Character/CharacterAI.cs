using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterAI : MonoBehaviour
{
    public List<BehaviourBase> Behaviours = new List<BehaviourBase>();
    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private Animator _animator;
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
        _animator = GetComponent<Animator>();
    }

    void Start()
    {
        foreach(var behaviour in Behaviours)
        {
            if (behaviour != null)
            {
                behaviour.Init(gameObject, _prop, _health);
            }
        }
    }

    public float MoveSpeed => _prop.moveSpeed;

    protected virtual void AIBehaviour()
    {
        foreach (var behaviour in Behaviours)
        {
            if(behaviour.AIBehaviour(gameObject, _prop, _health))
                break;
        }
        _animator.SetBool("IsAtt", _prop.isAttack);
    }

    protected virtual void Repel()
    {
        if (_prop.repelDistance > 0.1f)
        {
            transform.position += (_prop.isFacingLeft ? Vector3.right : Vector3.left) * _prop.repelDistance * 0.02f;
            _prop.repelDistance *= 0.98f;
        }
    }

    void Update()
    {
        AIBehaviour();
        Repel();
    }

    public void ShootProjectile()
    {
        if (_prop == null || _prop.atkObj == null) return;

        GameObject projectile = GameObjectPool.Instance.Get(_prop.atkObj);
        if (projectile != null)
        {
            // print(name + "  ShootProjectile");
            projectile.transform.position = transform.position;
            DamageSource ds = projectile.GetComponent<DamageSource>();
            if (ds != null)
            {
                ds.damage.initialDamage = _prop.atk;
                ds.damage.source = gameObject;
                ds.damage.side = _prop.side;
                ds.damage.repel = _prop.repel;
                // ds.damage.target = _prop.target;
                ds.target = _prop.target;
                ds.damage.type = DamageType.normal;
            }
        }
    }

}
