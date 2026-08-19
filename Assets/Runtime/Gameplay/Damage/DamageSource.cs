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

    [Header("命中受击音效")]
    [SerializeField, ResourceKey(typeof(AudioClip))]
    private string hitSoundKey = "Hit";                     // 命中时在目标单位位置播放的音频资源键。
    [SerializeField]
    private AudioSource hitAudio;                           // 播放受击音效的音频源；未配置时自动创建一次。
    [SerializeField, Range(0f, 1f)]
    private float hitVolume = 1f;                           // 受击音效音量。
    [SerializeField, Range(0, 256)]
    private int hitPriority = 32;                           // 受击音效优先级（数值越小越优先）。
    private bool _warnedMissingHitSound;                    // 是否已输出过受击音效缺失警告（一次性）。

    #region 生命周期与回调
    /// <summary>
    /// 在伤害来源尚未指定时，将当前游戏对象登记为伤害来源，并解析受击音效音频源。
    /// </summary>
    protected virtual void Awake()
    {
        ResolveHitAudio();
    }

    /// <summary>
    /// 解析受击音效音频源：优先 Inspector 配置，其次复用对象上的 AudioSource，
    /// 都没有时新建一个（池化实例仅在首次生成时创建一次，不属于热路径）。
    /// </summary>
    private void ResolveHitAudio()
    {
        if (hitAudio != null)
            return;

        hitAudio = GetComponent<AudioSource>();
        if (hitAudio == null)
        {
            hitAudio = gameObject.AddComponent<AudioSource>();
            hitAudio.playOnAwake = false;
            hitAudio.spatialBlend = 0f;
        }
    }

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
    /// 写入命中方向与目标后发送伤害，并在目标位置播放受击音效，
    /// 命中次数耗尽时回收当前对象。
    /// </summary>
    /// <param name="collision">进入触发器的二维碰撞体。</param>
    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if(hasSubProjectile)
            return;
            
        if(_remainCollideTime <= 0)
            return;

        // 同一目标去重：单位的多个碰撞体（Box/Circle）会各自触发一次本回调，
        // 只允许同一目标结算一次，避免“一瞬间两次伤害”的叠伤。
        if (!_hitTargets.Add(collision.gameObject))
            return;

        ICollide c = collision.GetComponent<ICollide>();    // 碰撞对象上的伤害接收接口。
        if(c == null)
            return;

        if(c.IsFriendly(damage))
            return;

        damage.collideDir = transform.position.x < collision.transform.position.x? 1 : -1;
        damage.target = collision.gameObject;
        c.OnCollide(damage);
        // 弹幕命中通知：被命中的目标实现 IProjectileImpactHandler 时回调
        // （如 General Cat 每次受到弹幕攻击获得一层护甲）。
        IProjectileImpactHandler impact = collision.GetComponent<IProjectileImpactHandler>();
        if (impact != null)
            impact.OnProjectileDamageTriggered(collision.transform.position);
        PlayHitSound(collision.transform);
        // DamageTextPool.Instance.ShowDamage(damage, collision.transform.position + Vector3.up);
        _remainCollideTime--;
        
        if(_remainCollideTime <= 0)
            GameObjectPool.Instance.Release(gameObject);
    }

    /// <summary>
    /// 命中敌方单位时，以目标单位位置为声源播放受击音效。
    /// 仅弹幕命中触发；Buff 持续伤害与直接调用 ICollide 的伤害不经过本入口，不会播放。
    /// 音频键或片段缺失时输出一次性警告，避免静默失败。
    /// </summary>
    /// <param name="targetTransform">被命中单位的位置，音效在此处发声。</param>
    protected virtual void PlayHitSound(Transform targetTransform)
    {
        if (hitAudio == null ||
            targetTransform == null ||
            AudioManager.Instance == null ||
            ResourceManager.Instance == null ||
            string.IsNullOrEmpty(hitSoundKey))
            return;

        AudioClip clip = ResourceManager.Instance.GetAudio(hitSoundKey);
        if (clip == null)
        {
            if (!_warnedMissingHitSound)
            {
                _warnedMissingHitSound = true;
                Debug.LogWarning($"[DamageSource] 音频资源 {hitSoundKey} 未加载，命中音效无法播放。", this);
            }
            return;
        }

        hitAudio.clip = clip;
        hitAudio.volume = hitVolume;
        hitAudio.priority = hitPriority;
        AudioManager.Instance.PlayEffectAt(
            hitAudio,
            (uint)hitPriority,
            targetTransform);
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
