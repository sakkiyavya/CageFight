using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public interface IProjectileImpactHandler
{
    void OnProjectileDamageTriggered(Vector3 impactPosition);
}

public class DamageSource : MonoBehaviour
{
    public Damage damage = Damage.DefaultDamage;            // 碰撞时发送给目标的伤害数据。
    public GameObject target;                               // 投射物预先指定的追踪目标。
    public float sustainTime = 0.2f;                        // 每次启用后允许存在的最长时间。
    public int collideTimes = 5;                            // 每次启用后允许命中的敌方目标次数。
    public bool hasSubProjectile = false;                   // 是否由子投射物自行处理命中，跳过当前触发器逻辑。
    float _remainTime = 0f;                                 // 当前启用周期的剩余存活时间。
    int _remainCollideTime = 0;                             // 当前启用周期的剩余命中次数。
    readonly System.Collections.Generic.HashSet<GameObject> _hitTargets =
        new System.Collections.Generic.HashSet<GameObject>(); // 本次启用周期已结算的目标（防止同一目标的多个碰撞体/反复进出导致叠伤）。

    #region 生命周期与回调
    /// <summary>
    /// 在伤害来源尚未指定时，将当前游戏对象登记为伤害来源。
    /// </summary>
    protected virtual void Start()
    {
        if(damage.source == null)
            damage.source = gameObject;
    }

    /// <summary>
    /// 处理二维触发器命中：忽略子投射物模式、无碰撞接口和友方目标，
    /// 同一目标每次启用周期只结算一次（目标通常有多个触发器碰撞体，
    /// 不排除会在一次接触中重复结算造成叠伤），
    /// 写入命中方向与目标后发送伤害，命中次数耗尽时回收当前对象。
    /// </summary>
    /// <param name="collision">进入触发器的二维碰撞体。</param>
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if(hasSubProjectile)
            return;
            
        if(_remainCollideTime <= 0)
            return;

        // ICollide 可能挂在碰撞体的父物体上（碰撞体在子节点、生命组件在根节点）：
        // 命中结算与去重都以“承载 ICollide 的对象”为准，避免子碰撞体命中被判无效，
        // 导致弹体贴着目标却永远不结算、追着目标转（贴脚旋转的根因之一）。
        ICollide c = collision.GetComponent<ICollide>();
        if (c == null)
            c = collision.GetComponentInParent<ICollide>();
        if (c == null)
            return;

        GameObject hitObject = (c as Component) != null
            ? ((Component)c).gameObject
            : collision.gameObject;

        // 同一目标去重：单位的多个碰撞体（Box/Circle）会各自触发一次本回调，
        // 只允许同一目标结算一次，避免“一瞬间两次伤害”的叠伤。
        if (!_hitTargets.Add(hitObject))
            return;

        if(c.IsFriendly(damage))
            return;

        damage.collideDir = transform.position.x < collision.transform.position.x? 1 : -1;
        damage.target = hitObject;
        c.OnCollide(damage);
        // 弹幕命中通知：被命中的目标实现 IProjectileImpactHandler 时回调
        // （如 General Cat 每次受到弹幕攻击获得一层护甲）。
        IProjectileImpactHandler impact = collision.GetComponent<IProjectileImpactHandler>();
        if (impact != null)
            impact.OnProjectileDamageTriggered(collision.transform.position);
        // DamageTextPool.Instance.ShowDamage(damage, collision.transform.position + Vector3.up);
        _remainCollideTime--;
        
        if(_remainCollideTime <= 0)
            GameObjectPool.Instance.Release(gameObject);
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 按配置重置当前启用周期的剩余存活时间和可命中次数，并清空目标去重集合。
    /// </summary>
    public void Init()
    {
        _remainTime = sustainTime;
        _remainCollideTime = collideTimes;
        _hitTargets.Clear();
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 每帧推进伤害源的生存计时。
    /// </summary>
    protected virtual void Update()
    {
        TimeUpdate();
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 扣减剩余存活时间，并在时间耗尽时将当前对象归还对象池。
    /// </summary>
    protected virtual void TimeUpdate()
    {
        _remainTime -= Time.deltaTime;
        if(_remainTime <= 0)
        {
            GameObjectPool.Instance.Release(gameObject);
        }
    }
    #endregion

    #region 生命周期与回调
    /// <summary>
    /// 对象从池中启用时重置存活时间和命中次数。
    /// </summary>
    protected virtual void OnEnable()
    {
        Init();
    }
    #endregion
}
