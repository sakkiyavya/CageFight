using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Connect Master（连接大师）机制：
/// 1. 攻击（攻击动画事件 ConnectMasterAttack）时：直接对当前目标造成一次伤害
///    （prop.atk），并在目标脚下放置一个椭圆形半透明法阵（素材 magicCircleSprite
///    由用户在 Inspector 拖入；未拖入时法阵不可见但逻辑照常）。
/// 2. 法阵留场 staySeconds（默认 10）秒后渐变透明消失。
/// 3. 相连引爆：场上的同类法阵按“阵营 + 类型”全局共享计数——任意友方 Connect Master
///    放置的法阵都计入；当本方场上法阵达到 connectCount（默认 6）个时立即触发，
///    本方所有法阵一起引爆：全部变为不透明、同时升起 riseHeight 并边升起边渐变透明，
///    升到顶点时对法阵周围 burstRadius 内的敌人各造成“记录的攻击力 × burstDamageMultiplier
///    （默认 200%）”伤害，随后回收。
/// 法阵经 GameObjectPool 复用（ConnectMasterCircle 预制体，Map 层 order 1，不遮挡单位），
/// 仅新增本脚本即可生效。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class ConnectMaster : MonoBehaviour
{
    [Header("法阵外观")]
    [SerializeField, Tooltip("法阵素材（椭圆形，由用户拖入）")]
    private Sprite magicCircleSprite;
    [SerializeField, Tooltip("法阵预制体（ConnectMasterCircle）")]
    private GameObject circlePrefab;
    [SerializeField]
    private Vector2 circleScale = new Vector2(0.8f, 0.55f); // 法阵缩放（椭圆可调 x/y）。
    [SerializeField, Range(0f, 1f)]
    private float idleAlpha = 0.45f;                        // 留场期半透明。
    [SerializeField, Min(0.1f)]
    private float staySeconds = 10f;                        // 法阵留场时长。
    [SerializeField, Min(0.05f)]
    private float fadeSeconds = 0.5f;                       // 留场到期渐变消失时长。

    [Header("相连引爆")]
    [SerializeField, Min(2)]
    private int connectCount = 6;                           // 本方场上法阵达到该数量触发引爆。
    [SerializeField, Min(0f)]
    private float burstDamageMultiplier = 2f;               // 引爆伤害 = 记录攻击力 × 该倍率（200%）。
    [SerializeField, Min(0.1f)]
    private float burstRadius = 1.6f;                       // 引爆伤害判定半径。
    [SerializeField, Min(0.05f)]
    private float riseHeight = 1.68f;                       // 引爆升起高度（1.2 × 1.4）。
    [SerializeField, Min(0.05f)]
    private float riseSeconds = 0.5f;                       // 升起耗时（期间同步渐变透明）。

    /// <summary>场上全部法阵（全阵营共享，按阵营分组计数）。</summary>
    internal static readonly List<ConnectCircleRuntime> ActiveCircles =
        new List<ConnectCircleRuntime>();
    internal static readonly Collider2D[] HitsBuffer = new Collider2D[64];              // 引爆判定复用缓冲。
    internal static readonly HashSet<GameObjectProperty> BurstHitSet = new HashSet<GameObjectProperty>(); // 引爆去重。

    private static readonly ConnectCircleRuntime[] sideCircles = new ConnectCircleRuntime[256]; // 本方场上法阵暂存。

    private GameObjectProperty _prop;

    #region 供法阵读取的配置
    internal Sprite MagicCircleSprite => magicCircleSprite;
    internal Vector3 CircleScale => new Vector3(circleScale.x, circleScale.y, 1f);
    internal float IdleAlpha => idleAlpha;
    internal float StaySeconds => staySeconds;
    internal float FadeSeconds => fadeSeconds;
    internal float RiseHeight => riseHeight;
    internal float RiseSeconds => riseSeconds;
    internal float BurstRadius => burstRadius;
    internal float BurstDamageMultiplier => burstDamageMultiplier;
    internal int Side => _prop != null ? _prop.side : -1;
    #endregion

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }

    /// <summary>
    /// 攻击动画事件：对当前目标造成一次伤害，并在其脚下放置法阵，随后检查相连引爆。
    /// </summary>
    public void ConnectMasterAttack()
    {
        if (_prop == null || _prop.isDead || _prop.target == null)
            return;

        GameObjectProperty targetProp = _prop.target.GetComponent<GameObjectProperty>();
        if (targetProp == null || targetProp.isDead)
            return;

        ICollide collide = targetProp.GetComponent<ICollide>();
        if (collide == null)
            return;

        // 造成一次伤害。
        Damage damage = Damage.DefaultDamage;
        damage.side = _prop.side;
        damage.source = gameObject;
        damage.target = _prop.target;
        damage.initialDamage = _prop.atk;
        damage.repel = _prop.repel;
        collide.OnCollide(damage);

        // 在目标脚下放置法阵并检查相连。
        PlaceCircle(targetProp.transform.position, _prop.atk);
        CheckConnection();
    }

    /// <summary>从对象池取法阵并放置在指定位置（法阵自行登记到全局列表）。</summary>
    private void PlaceCircle(Vector3 position, int attackDamage)
    {
        if (circlePrefab == null)
            return;

        GameObject go = GameObjectPool.Instance.Get(circlePrefab);
        if (go == null)
            return;

        ConnectCircleRuntime runtime = go.GetComponent<ConnectCircleRuntime>();
        if (runtime == null)
            runtime = go.AddComponent<ConnectCircleRuntime>();

        runtime.Place(position, this, attackDamage);
    }

    /// <summary>
    /// 检查本方场上法阵数量：达到 connectCount 时引爆本方全部法阵。
    /// 计数按“阵营 + 类型”全局共享——任意友方 Connect Master 放置的法阵都计入
    /// （如 6 个 Connect Master 一起释放即触发）。仅在新法阵放置时调用。
    /// </summary>
    private void CheckConnection()
    {
        int side = Side;
        if (side < 0)
            return;

        // 清理失效项并统计本方在场法阵。
        int n = 0;
        for (int i = ActiveCircles.Count - 1; i >= 0; i--)
        {
            ConnectCircleRuntime circle = ActiveCircles[i];
            if (circle == null || !circle.IsOnField)
            {
                ActiveCircles.RemoveAt(i);
                continue;
            }

            if (circle.Side == side && n < sideCircles.Length)
                sideCircles[n++] = circle;
        }

        if (n < connectCount)
            return;

        TriggerAllFriendlyCircles(side);
    }

    /// <summary>引爆本方场上全部法阵（各自变为不透明、升起、结算伤害、渐变消失）。</summary>
    private void TriggerAllFriendlyCircles(int side)
    {
        for (int i = ActiveCircles.Count - 1; i >= 0; i--)
        {
            ConnectCircleRuntime circle = ActiveCircles[i];
            if (circle != null && circle.IsOnField && circle.Side == side)
                circle.TriggerBurst();
        }
    }
}

/// <summary>
/// 法阵运行时：留场半透明 → 到期渐变消失；或受相连引爆：变不透明 → 升起 →
/// 顶点结算一次伤害 → 渐变透明 → 回收。经 GameObjectPool 复用。
/// 放置时快照全部配置，放置者死亡后法阵仍可独立完成留场/引爆流程。
/// </summary>
internal class ConnectCircleRuntime : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private GameObject _source;              // 引爆伤害来源对象（放置者）。
    private int _side;                       // 引爆伤害阵营。
    private int _storedDamage;               // 放置时记录的攻击力（引爆伤害基数）。
    private float _startAlpha;               // 留场期起始透明度。
    private float _stayUntil;                // 留场到期时间。
    private float _fadeSeconds;              // 到期渐变时长。
    private float _riseHeight;               // 引爆升起高度。
    private float _riseSeconds;              // 升起耗时（期间同步渐变透明）。
    private float _burstRadius;              // 引爆伤害半径。
    private float _burstDamageMultiplier;    // 引爆伤害倍率。

    private bool _waiting;                   // 是否处于留场期。
    private bool _bursting;                  // 是否处于引爆动画。
    private bool _burstDamaged;              // 引爆伤害是否已结算。
    private Vector3 _basePosition;           // 升起起点。
    private float _burstStartTime;           // 引爆动画开始时间。
    private Coroutine _fadeRoutine;          // 到期渐变协程。

    /// <summary>是否仍在场上参与引爆计数。</summary>
    public bool IsOnField => _waiting && !_bursting;

    /// <summary>本方阵营，供全局计数分组。</summary>
    public int Side => _side;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    /// <summary>池化对象重新启用时重置本轮状态（放置前不残留上一轮法阵状态）。</summary>
    private void OnEnable()
    {
        _waiting = false;
        _bursting = false;
        _burstDamaged = false;
        ConnectMaster.ActiveCircles.Remove(this);
    }

    /// <summary>初始化法阵：快照配置与外观（半透明），开始留场计时并登记全局列表。</summary>
    public void Place(Vector3 position, ConnectMaster owner, int attackDamage)
    {
        _source = owner.gameObject;
        _side = owner.Side;
        _storedDamage = Mathf.Max(1, attackDamage);
        _startAlpha = owner.IdleAlpha;
        _stayUntil = Time.time + owner.StaySeconds;
        _fadeSeconds = owner.FadeSeconds;
        _riseHeight = owner.RiseHeight;
        _riseSeconds = owner.RiseSeconds;
        _burstRadius = owner.BurstRadius;
        _burstDamageMultiplier = owner.BurstDamageMultiplier;

        _waiting = true;
        _bursting = false;
        _burstDamaged = false;
        transform.position = position;
        transform.localScale = owner.CircleScale;

        if (_renderer != null)
        {
            _renderer.sprite = owner.MagicCircleSprite;
            _renderer.enabled = owner.MagicCircleSprite != null;
            Color color = _renderer.color;
            color.a = _startAlpha;
            _renderer.color = color;
        }

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        ConnectMaster.ActiveCircles.Remove(this);   // 池化复用防重复登记。
        ConnectMaster.ActiveCircles.Add(this);
    }

    /// <summary>开始引爆动画：变不透明、升起、顶点结算伤害、渐变消失。</summary>
    public void TriggerBurst()
    {
        if (!IsOnField)
            return;

        _bursting = true;
        _basePosition = transform.position;
        _burstStartTime = Time.time;

        if (_renderer != null)
        {
            _renderer.enabled = _renderer.sprite != null;
            Color color = _renderer.color;
            color.a = 1f;   // 变不透明。
            _renderer.color = color;
        }
    }

    private void Update()
    {
        if (!_waiting)
            return;

        if (_bursting)
        {
            UpdateBurst();
            return;
        }

        // 留场到期 → 渐变消失。
        if (Time.time >= _stayUntil)
        {
            _waiting = false;
            _fadeRoutine = StartCoroutine(FadeOutRoutine(_fadeSeconds));
        }
    }

    private void UpdateBurst()
    {
        float elapsed = Time.time - _burstStartTime;

        // 升起进度。
        float t = Mathf.Clamp01(elapsed / _riseSeconds);

        // 升起的同时渐变透明（1 → 0）。
        transform.position = _basePosition + Vector3.up * (_riseHeight * t);
        if (_renderer != null)
        {
            Color color = _renderer.color;
            color.a = Mathf.Lerp(1f, 0f, t);
            _renderer.color = color;
        }

        // 顶点结算一次伤害（放置者已离场则跳过伤害，仅完成视觉）。
        if (!_burstDamaged && t >= 1f)
        {
            _burstDamaged = true;
            DealBurstDamage();
            Finish();
        }
    }

    /// <summary>引爆伤害：对法阵周围敌人造成“记录攻击力 × 倍率”伤害。</summary>
    private void DealBurstDamage()
    {
        // 放置者已死亡/回收时不再结算伤害。
        if (_source == null || !_source.activeInHierarchy || _side < 0)
            return;

        int damageValue = Mathf.Max(1,
            Mathf.RoundToInt(_storedDamage * _burstDamageMultiplier));

        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, _burstRadius, ConnectMaster.HitsBuffer);
        ConnectMaster.BurstHitSet.Clear();

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = ConnectMaster.HitsBuffer[i];
            if (hit == null)
                continue;

            GameObjectProperty target = hit.GetComponentInParent<GameObjectProperty>();
            if (target == null || target.isDead ||
                target.side == _side ||
                !ConnectMaster.BurstHitSet.Add(target))
                continue;

            ICollide collide = target.GetComponent<ICollide>();
            if (collide == null)
                continue;

            Damage damage = Damage.DefaultDamage;
            damage.side = _side;
            damage.source = _source;
            damage.target = target.gameObject;
            damage.initialDamage = damageValue;
            damage.repel = 0f;
            collide.OnCollide(damage);
        }
    }

    /// <summary>留场到期：透明度渐变到 0 后回收。</summary>
    private IEnumerator FadeOutRoutine(float seconds)
    {
        float startTime = Time.time;
        while (Time.time - startTime < seconds)
        {
            if (_renderer != null)
            {
                Color color = _renderer.color;
                color.a = Mathf.Lerp(_startAlpha, 0f,
                    Mathf.Clamp01((Time.time - startTime) / seconds));
                _renderer.color = color;
            }

            yield return null;
        }

        Finish();
    }

    /// <summary>结束流程：从全局列表移除并回收对象池。</summary>
    private void Finish()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        _waiting = false;
        _bursting = false;
        ConnectMaster.ActiveCircles.Remove(this);
        GameObjectPool.Instance.Release(gameObject);
    }

    private void OnDisable()
    {
        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        _waiting = false;
        _bursting = false;
        ConnectMaster.ActiveCircles.Remove(this);
    }
}
