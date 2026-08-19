using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterAI : MonoBehaviour
{
    public List<BehaviourBase> Behaviours = new List<BehaviourBase>();                  // 按优先顺序执行的 AI 行为组件。
    [SerializeField] private Transform shootPoint;                                       // 投射物生成位置；未配置时自动查找 ShootPoint 子节点。
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
    /// 沿二次贝塞尔曲线完成击退，落点仍使用原有击退距离。
    /// </summary>
    protected virtual void Repel()
    {
        if (!_prop.repelInitialized)
            _prop.StartRepel(transform.position, _prop.repelDistance);

        _prop.repelElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_prop.repelElapsed / _prop.repelDuration);
        float inv = 1f - t;
        transform.position = inv * inv * _prop.repelStart +
            2f * inv * t * _prop.repelControl + t * t * _prop.repelEnd;
        _prop.repelDistance = (_prop.repelEnd.x - _prop.repelStart.x) * (1f - t);
        if (t >= 1f)
        {
            _prop.isRepel = false;
            _prop.repelInitialized = false;
        }
    }

    /// <summary>
    /// 按顺序执行 AI 行为，首个返回成功的行为会阻止后续行为；
    /// 畏惧（FearState 激活）时跳过全部索敌/寻路/攻击行为，改为随机乱跑；
    /// 完成后将攻击状态同步到 Animator。
    /// </summary>
    protected virtual void AIBehaviour()
    {
        FearState fear = GetComponent<FearState>();
        if (fear != null && fear.IsActive)
        {
            // 畏惧：停止索敌与攻击，随机乱跑直到状态结束。
            fear.DoFearMove(gameObject, _prop, _health);
        }
        else
        {
            foreach (var behaviour in Behaviours)
            {
                if(behaviour.AIBehaviour(gameObject, _prop, _health))
                    break;
            }
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

            // 若生成的对象是召唤单位（实现 ISummonedUnit），把攻击者注入为创造者。
            ISummonedUnit summoned = projectile.GetComponent<ISummonedUnit>();
            if (summoned != null)
                summoned.SetCreator(gameObject);
        }

        _prop.OnAtt?.Invoke();

    }

    /// <summary>
    /// 清除角色攻击状态，通常由攻击动画事件在一次攻击结束时调用。
    /// </summary>
    public void StopShoot() => _prop.isAttack = false;
    #endregion
    
}
