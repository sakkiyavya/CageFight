using UnityEngine;

/// <summary>
/// Thunderstorm cat（雷暴猫）机制：
/// 1. 攻击时进入雷霆模式 thunderDuration 秒（默认 7），并获得护盾
///    （盾量 = shieldHpPercent × 最大生命，默认 100%）。
///    模式内持续攻击会刷新剩余时间；护盾被击破或时间结束，任一情况都结束
///    雷霆模式，并给自己施加 selfParalysisSeconds（默认 3）秒麻痹。
///    雷霆模式期间定身：无法移动且免疫击退位移（移动速度清零 + 击退抗性拉满），
///    直到雷盾被击破或时长结束才恢复移动。
/// 2. 雷霆模式内被攻击：释放一枚电球飞向伤害来源，命中后对其造成
///    “本次实际受伤 × counterDamageMultiplier（默认 1）”的伤害，并附带一次
///    麻痹（ParalysisDebuff，期间无法移动/行动）。未命中的攻击不反击。
/// 3. 护盾在 OnHitted 阶段吸收伤害（预补血方式，与 AttackShieldUnit 相同）。
/// 4. 视觉：雷霆模式期间，单位 Y 轴上方 shieldVisualHeight（默认 1）单位处
///    显示一个圆形呼吸灯护盾（透明度正弦呼吸，模式结束隐藏）。
/// 电球使用 Bullet-Thunderstorm 预制体（Bullet2 AP 第 4 个素材），经 GameObjectPool 复用。
/// 模式入口采用电平触发（攻击状态为真即进入），不依赖攻击状态的边沿变化，
/// 兼容攻击动画缺少 StopShoot 事件导致 isAttack 不回落的情况。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class ThunderstormCat : MonoBehaviour, IIncomingDamageModifier
{
    [Header("雷霆模式")]
    [SerializeField, Min(0.1f)] private float thunderDuration = 7f;         // 雷霆模式持续秒。
    [SerializeField, Range(0.01f, 1f)] private float shieldHpPercent = 1f;  // 护盾 = 最大生命比例（100%）。
    [SerializeField, Min(0f)] private float counterDamageMultiplier = 1f;   // 电球伤害 = 本次实际受伤 × 该倍率。
    [SerializeField, Min(0.1f)] private float selfParalysisSeconds = 3f;    // 模式结束时自身麻痹秒数。

    [Header("电球")]
    [SerializeField, ResourceKey(typeof(GameObject)), Tooltip("电球预制体资源键（Bullet-Thunderstorm）")]
    private string ballPrefabKey = "Bullet-Thunderstorm";
    [SerializeField, Min(0.1f)] private float ballSpeed = 10f;              // 电球飞行速度。
    [SerializeField, Min(0.05f)] private float ballHitDistance = 0.3f;      // 电球命中判定的接近距离。

    [Header("雷盾视觉（呼吸灯）")]
    [SerializeField, ResourceKey(typeof(GameObject))]
    private string shieldVisualPrefabKey = "UnitVisualFollower"; // 雷盾视觉预制体资源键（池化生成）。
    [SerializeField, ResourceKey(typeof(Sprite))]
    private string shieldVisualSpriteKey = "Bullet3 AP_0";       // 雷盾视觉贴图资源键。
    [SerializeField, Min(0f)] private float shieldVisualHeight = 1f;        // 护盾相对单位位置的 Y 轴高度（1 单位）。
    [SerializeField, Min(0.05f)] private float shieldVisualScale = 1.6f;    // 护盾缩放。
    [SerializeField, Min(0.1f)] private float breathSpeed = 2.5f;           // 呼吸频率（每秒周期数）。
    [SerializeField, Range(0f, 1f)] private float breathMinAlpha = 0.25f;   // 呼吸最低透明度。
    [SerializeField, Range(0f, 1f)] private float breathMaxAlpha = 0.85f;   // 呼吸最高透明度。

    /// <summary>雷霆模式阶段：空闲 → 雷霆 → 结束麻痹 → 空闲。</summary>
    private enum Phase { Idle, Thunder, SelfParalyzed }

    private const float ImmobilizedAntiRepel = 1000000f;  // 定身期间的击退抗性（击退位移 = repel / antiRepel ≈ 0）。

    private GameObjectProperty _prop;
    private ParalysisDebuff _paralysis;                 // 电球命中的标准麻痹。
    private ThunderstormParalysisDebuff _selfParalysis; // 模式结束后的自身长时麻痹。
    private GameObject _ballPrefab;                     // 经 ResourceManager 解析的电球预制体缓存。

    private Phase _phase;                               // 当前阶段。
    private float _thunderEndTime;                      // 雷霆模式结束时间。
    private float _paralysisEndTime;                    // 自身麻痹结束时间。
    private int _shieldHp;                              // 当前护盾值。
    private bool _immobilized;                          // 是否处于雷霆定身中。
    private float _moveSpeedBefore;                     // 定身前的移动速度（退出时恢复）。
    private float _antiRepelBefore;                     // 定身前的击退抗性（退出时恢复）。

    private UnitVisualFollower _shieldFollower;         // 雷盾视觉（池化跟随对象）。
    private GameObject _shieldVisualPrefab;             // 视觉预制体缓存（按资源键解析）。
    private Sprite _shieldSprite;                       // 视觉贴图缓存（按资源键解析）。

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _paralysis = gameObject.AddComponent<ParalysisDebuff>();
        _selfParalysis = gameObject.AddComponent<ThunderstormParalysisDebuff>();
        _selfParalysis.SetDuration(selfParalysisSeconds);
    }

    private void OnEnable()
    {
        _prop.OnAtt += HandleAttack;

        _phase = Phase.Idle;
        _shieldHp = 0;
        _immobilized = false;
        SetShieldVisual(false);
    }

    private void OnDisable()
    {
        _prop.OnAtt -= HandleAttack;

        // 死亡/回收时直接结束模式，恢复移动，不再给死者叠麻痹。
        Immobilize(false);
        _phase = Phase.Idle;
        _shieldHp = 0;
        SetShieldVisual(false);
    }

    private void Update()
    {
        if (_prop == null || _prop.isDead)
            return;

        switch (_phase)
        {
            case Phase.Thunder:
                // 攻击中持续刷新模式时间（电平触发，不依赖攻击状态边沿）；
                // 停止攻击后超时则结束模式。
                if (_prop.isAttack)
                    _thunderEndTime = Time.time + thunderDuration;
                else if (Time.time >= _thunderEndTime)
                    EndThunderMode();
                break;

            case Phase.SelfParalyzed:
                if (Time.time >= _paralysisEndTime)
                    _phase = Phase.Idle;
                break;

            default:
                // 空闲且处于攻击状态 → 进入雷霆模式。
                if (_prop.isAttack)
                    EnterThunderMode();
                break;
        }
    }

    /// <summary>攻击事件（远程单位走 OnAtt）：空闲时进入雷霆模式。</summary>
    private void HandleAttack()
    {
        if (_phase == Phase.Idle)
            EnterThunderMode();
    }

    /// <summary>进入雷霆模式：刷新持续时间、补满护盾、点亮护盾视觉并定身。</summary>
    private void EnterThunderMode()
    {
        if (_prop == null || _prop.isDead)
            return;

        _phase = Phase.Thunder;
        _thunderEndTime = Time.time + thunderDuration;
        _shieldHp = Mathf.Max(1, Mathf.RoundToInt(_prop.maxHp * shieldHpPercent));
        SetShieldVisual(true);
        Immobilize(true);
    }

    /// <summary>
    /// 雷霆定身：移动速度清零（Move 每帧按 moveSpeed 移动）并把击退抗性拉满
    /// （击退位移 = repel / antiRepel，近似为 0），使单位原地不动且不被击退；
    /// 退出时恢复进入前的数值。
    /// </summary>
    private void Immobilize(bool active)
    {
        if (_prop == null)
            return;

        if (active)
        {
            if (!_immobilized)
            {
                _moveSpeedBefore = _prop.moveSpeed;
                _antiRepelBefore = _prop.antiRepel;
                _immobilized = true;
            }

            _prop.moveSpeed = 0f;
            _prop.antiRepel = ImmobilizedAntiRepel;
        }
        else if (_immobilized)
        {
            _immobilized = false;
            _prop.moveSpeed = _moveSpeedBefore;
            _prop.antiRepel = _antiRepelBefore;
        }
    }

    /// <summary>
    /// 统一入伤修正（IIncomingDamageModifier）：在正式扣血前处理雷霆模式的反击与护盾吸收。
    /// 伤害已由 CharacterHealth 完成唯一一次结算，本方法只取最终值并返回剩余伤害，
    /// 不重复计算、不预先回血抵消。
    /// </summary>
    public int ModifyIncomingDamage(Damage damage)
    {
        if (_phase != Phase.Thunder || _prop == null || damage.finalDamage <= 0)
            return damage.finalDamage;

        // 反击：对伤害来源释放电球（未命中时 finalDamage 为 0，不会进入这里）。
        if (damage.source != null)
        {
            int counter = Mathf.Max(1, Mathf.RoundToInt(damage.finalDamage * counterDamageMultiplier));
            ReleaseBall(damage.source, counter);
        }

        // 护盾吸收：护盾耗尽即结束雷霆模式（触发自麻痹）。
        if (_shieldHp > 0)
        {
            int absorbed = Mathf.Min(_shieldHp, damage.finalDamage);
            _shieldHp -= absorbed;

            if (_shieldHp <= 0)
                EndThunderMode();

            return damage.finalDamage - absorbed;
        }

        return damage.finalDamage;
    }

    /// <summary>
    /// 结束雷霆模式（护盾击破或时间结束）：隐藏护盾视觉、恢复移动，
    /// 并给自己施加一段麻痹。
    /// </summary>
    private void EndThunderMode()
    {
        if (_phase != Phase.Thunder)
            return;

        _phase = Phase.SelfParalyzed;
        _paralysisEndTime = Time.time + selfParalysisSeconds;
        _shieldHp = 0;
        SetShieldVisual(false);
        Immobilize(false);

        if (_prop == null || _prop.isDead)
            return;

        _prop.ApplyStatus(_selfParalysis);
    }

    /// <summary>
    /// 从对象池取电球并发射：电球飞向伤害来源，命中后结算反击伤害并附带一次麻痹。
    /// 电球预制体经 ResourceManager 按资源键解析并缓存。
    /// </summary>
    private void ReleaseBall(GameObject attacker, int counterDamage)
    {
        if (string.IsNullOrEmpty(ballPrefabKey) || attacker == null)
            return;

        // 延迟补齐：资源就绪前跳过本次反击（键已进公共预载列表，正常对局首次攻击后即就绪）。
        if (_ballPrefab == null && ResourceManager.Instance != null)
            _ballPrefab = ResourceManager.Instance.GetGameObject(ballPrefabKey);

        if (_ballPrefab == null)
            return;

        GameObject ball = GameObjectPool.Instance.Get(_ballPrefab);
        if (ball == null)
            return;

        ThunderBoltRuntime runtime = ball.GetComponent<ThunderBoltRuntime>();
        if (runtime == null)
            runtime = ball.AddComponent<ThunderBoltRuntime>();

        Damage damage = Damage.DefaultDamage;
        damage.side = _prop.side;
        damage.source = gameObject;
        damage.target = attacker;
        damage.initialDamage = counterDamage;
        damage.repel = 0f;
        damage.type = DamageType.normal;
        damage.buffs = new BuffBase[] { _paralysis };

        runtime.Launch(transform.position, attacker, damage, ballSpeed, ballHitDistance);
    }

    #region 护盾视觉
    /// <summary>
    /// 生成雷盾视觉：按资源键解析池化预制体与贴图，经 GameObjectPool 生成
    /// UnitVisualFollower 绑定自身（Y 轴上方 shieldVisualHeight 处呼吸），
    /// 跟随、呼吸与回收由该组件统一管理，不再运行时生成贴图/临时对象。
    /// </summary>
    private void TrySpawnShieldVisual()
    {
        if (_shieldFollower != null)
        {
            if (_shieldFollower.IsActive)
                return;
            _shieldFollower = null;
        }

        if (ResourceManager.Instance == null)
            return;

        // 延迟补齐：资源就绪前本次跳过，后续进入雷霆模式时重试。
        if (_shieldVisualPrefab == null && !string.IsNullOrEmpty(shieldVisualPrefabKey))
            _shieldVisualPrefab = ResourceManager.Instance.GetGameObject(shieldVisualPrefabKey);
        if (_shieldSprite == null && !string.IsNullOrEmpty(shieldVisualSpriteKey))
            _shieldSprite = ResourceManager.Instance.GetSprite(shieldVisualSpriteKey);

        if (_shieldVisualPrefab == null || _shieldSprite == null)
            return;

        GameObject go = GameObjectPool.Instance.Get(_shieldVisualPrefab);
        if (go == null)
            return;

        UnitVisualFollower follower = go.GetComponent<UnitVisualFollower>();
        if (follower == null)
            follower = go.AddComponent<UnitVisualFollower>();

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = _shieldSprite;
            renderer.sortingLayerID = 495858691;    // OnMap 层，盖在单位身体（order 0）之上。
            renderer.sortingOrder = 1;
            renderer.color = new Color(0.55f, 0.8f, 1f, breathMaxAlpha);
        }

        go.transform.localScale = Vector3.one * shieldVisualScale;
        follower.Init(gameObject, new Vector3(0f, shieldVisualHeight, 0f),
            breathSpeed, breathMinAlpha, breathMaxAlpha);
        _shieldFollower = follower;
    }

    /// <summary>显示或隐藏雷盾视觉。</summary>
    private void SetShieldVisual(bool visible)
    {
        if (!visible)
        {
            if (_shieldFollower != null)
            {
                _shieldFollower.Finish();
                _shieldFollower = null;
            }
            return;
        }

        TrySpawnShieldVisual();
    }
    #endregion
}

/// <summary>
/// 电球运行时：朝伤害来源直线飞行，命中后结算反击伤害与麻痹并回收。
/// 目标死亡/离场后电球飞向最后位置并直接回收，不结算伤害。
/// </summary>
internal class ThunderBoltRuntime : MonoBehaviour
{
    private GameObject _target;              // 反击目标（伤害来源）。
    private GameObjectProperty _targetProp;  // 反击目标属性（发射时缓存，避免每帧 GetComponent）。
    private Damage _damage;                  // 命中时结算的伤害（含麻痹 Buff）。
    private float _speed;                    // 飞行速度。
    private float _hitDistance;              // 命中接近距离。
    private Vector3 _aimPosition;            // 当前瞄准位置。
    private bool _flying;                    // 是否在飞行中。

    public void Launch(Vector3 start, GameObject target, Damage damage, float speed, float hitDistance)
    {
        _target = target;
        _targetProp = target != null ? target.GetComponent<GameObjectProperty>() : null;
        _damage = damage;
        _speed = Mathf.Max(0.1f, speed);
        _hitDistance = Mathf.Max(0.05f, hitDistance);
        _aimPosition = target != null ? target.transform.position : start;
        transform.position = start;
        _flying = true;
    }

    private void OnEnable()
    {
        _flying = false;
    }

    private void Update()
    {
        if (!_flying)
            return;

        if (IsValidTarget())
            _aimPosition = _target.transform.position;

        Vector3 offset = _aimPosition - transform.position;
        float distance = offset.magnitude;

        if (distance <= _hitDistance)
        {
            Impact();
            return;
        }

        float step = Mathf.Min(_speed * Time.deltaTime, distance);
        transform.position += offset / distance * step;
        transform.Rotate(0f, 0f, 720f * Time.deltaTime);
    }

    /// <summary>目标是否仍存活在场（死亡或被回收即失效）。</summary>
    private bool IsValidTarget()
    {
        if (_target == null || !_target.activeInHierarchy)
            return false;

        return _targetProp == null || !_targetProp.isDead;
    }

    private void Impact()
    {
        _flying = false;

        if (IsValidTarget() && _damage.target != null)
        {
            ICollide collide = _target.GetComponent<ICollide>();
            if (collide != null)
                collide.OnCollide(_damage);
        }

        GameObjectPool.Instance.Release(gameObject);
    }

    private void OnDisable()
    {
        _flying = false;
    }
}
