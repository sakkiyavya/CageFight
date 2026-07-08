using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterAI : MonoBehaviour
{
    public List<BehaviourBase> Behaviours = new List<BehaviourBase>();
    [SerializeField]private Transform shootPoint;
    private GameObjectProperty _prop;
    private CharacterHealth _health;
    private Animator _animator;
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
        _animator = GetComponent<Animator>();
        if(!shootPoint)
            shootPoint = transform.Find("ShootPoint");
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

    protected virtual void Repel()
    {
        if (Mathf.Abs(_prop.repelDistance) > 0.1f)
        {
            transform.position += Vector3.right * _prop.repelDistance * 0.1f;
            _prop.repelDistance *= 0.9f;
        }
        else
        {
            _prop.isRepel = false;
        }
    }

    protected virtual void AIBehaviour()
    {
        foreach (var behaviour in Behaviours)
        {
            if(behaviour.AIBehaviour(gameObject, _prop, _health))
                break;
        }
        if(_animator)
            _animator.SetBool("IsAtt", _prop.isAttack);
    }

    void Update()
    {
        if(_prop.isRepel)
            Repel();
        AIBehaviour();
    }

    public void ShootProjectile()
    {
        if (_prop == null || string.IsNullOrEmpty(_prop.atkObj)) return;

        GameObject atkPrefab = ResourceManager.Instance.GetGameObject(_prop.atkObj);
        if (atkPrefab == null) return;

        GameObject projectile = GameObjectPool.Instance.Get(atkPrefab);
        if (projectile != null)
        {
            // print(name + "  ShootProjectile");
            if(shootPoint)
                projectile.transform.position = shootPoint.transform.position;
            else
                projectile.transform.position = transform.position;

            projectile.transform.right = _prop.isFacingLeft ? Vector3.left : Vector3.right;
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


        // #region 临时测试声音
        // AudioPlayer ap = GetComponent<AudioPlayer>();
        // if(ap)
        //     ap.PlayEffect(Random.Range(0, 2));
        // #endregion
    }

}
