using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterAI : MonoBehaviour
{
    public List<BehaviourBase> Behaviours = new List<BehaviourBase>();                  // 按优先顺序执行的 AI 行为组件。
    [SerializeField]private Transform shootPoint;                                       // 投射物生成位置；未配置时自动查找 ShootPoint 子节点。
    private GameObjectProperty _prop;                                                   // 角色运行时属性和 AI 状态。
    private CharacterHealth _health;                                                    // 角色生命组件。
    private Animator _animator;                                                         // 同步攻击状态的动画组件。
    #region 生命周期与回调
    /// <summary>
    /// 缓存角色依赖组件，并在未配置时查找投射物发射点。
    /// </summary>
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _health = GetComponent<CharacterHealth>();
        _animator = GetComponent<Animator>();
        if(!shootPoint)
            shootPoint = transform.Find("ShootPoint");
    }

    /// <summary>
    /// 按配置顺序初始化所有有效 AI 行为组件。
    /// </summary>
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
    #endregion

    public float MoveSpeed => _prop.moveSpeed;                                          // 当前角色的移动速度。

    #region 受击与 AI 行为
    /// <summary>
    /// 按剩余击退距离的固定比例逐帧移动角色并衰减距离，接近零时结束击退状态。
    /// </summary>
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

    /// <summary>
    /// 按顺序执行 AI 行为，首个返回成功的行为会阻止后续行为；
    /// 完成后将攻击状态同步到 Animator。
    /// </summary>
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
    #endregion

    #region 帧更新回调
    /// <summary>
    /// 活着时每帧先处理击退位移，再执行一轮 AI 行为决策。
    /// </summary>
    void Update()
    {
        if (_prop.isDead)
            return;

        if(_prop.isRepel)
            Repel();
        AIBehaviour();
    }
    #endregion

    #region 投射物攻击
    /// <summary>
    /// 从资源缓存和对象池取得攻击预制体，在发射点生成投射物，
    /// 写入攻击力、阵营、击退、来源和目标后发布攻击事件。
    /// </summary>
    public void ShootProjectile()
    {
        if (_prop == null || string.IsNullOrEmpty(_prop.atkObj)) return;

        GameObject atkPrefab = ResourceManager.Instance.GetGameObject(_prop.atkObj);    // 当前角色配置的攻击预制体。
        if (atkPrefab == null) return;

        GameObject projectile = GameObjectPool.Instance.Get(atkPrefab);                 // 从对象池取得的投射物实例。
        if (projectile != null)
        {
            // print(name + "  ShootProjectile");
            if(shootPoint)
                projectile.transform.position = shootPoint.transform.position;
            else
                projectile.transform.position = transform.position;

            projectile.transform.right = _prop.isFacingLeft ? Vector3.left : Vector3.right;
            DamageSource ds = projectile.GetComponent<DamageSource>();                  // 接收本次攻击数据的伤害源组件。
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

        _prop.OnAtt?.Invoke();

    }

    /// <summary>
    /// 清除角色攻击状态，通常由攻击动画事件在一次攻击结束时调用。
    /// </summary>
    public void StopShoot() => _prop.isAttack = false;
    #endregion
    
}
