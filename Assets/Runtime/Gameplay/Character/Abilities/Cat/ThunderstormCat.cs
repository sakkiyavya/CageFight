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
public class ThunderstormCat : MonoBehaviour
{
    [Header("雷霆模式")]
    [SerializeField, Min(0.1f)] private float thunderDuration = 7f;         // 雷霆模式持续秒。
    [SerializeField, Range(0.01f, 1f)] private float shieldHpPercent = 1f;  // 护盾 = 最大生命比例（100%）。
    [SerializeField, Min(0f)] private float counterDamageMultiplier = 1f;   // 电球伤害 = 本次实际受伤 × 该倍率。
    [SerializeField, Min(0.1f)] private float selfParalysisSeconds = 3f;    // 模式结束时自身麻痹秒数。

    [Header("电球")]
    [SerializeField, Tooltip("电球预制体（Bullet2 AP 第四个素材，Bullet-Thunderstorm）")]
    private GameObject ballPrefab;
    [SerializeField, Min(0.1f)] private float ballSpeed = 10f;              // 电球飞行速度。
    [SerializeField, Min(0.05f)] private float ballHitDistance = 0.3f;      // 电球命中判定的接近距离。

    [Header("雷盾视觉（呼吸灯）")]
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

    private Phase _phase;                               // 当前阶段。
    private float _thunderEndTime;                      // 雷霆模式结束时间。
    private float _paralysisEndTime;                    // 自身麻痹结束时间。
    private int _shieldHp;                              // 当前护盾值。
    private bool _immobilized;                          // 是否处于雷霆定身中。
    private float _moveSpeedBefore;                     // 定身前的移动速度（退出时恢复）。
    private float _antiRepelBefore;                     // 定身前的击退抗性（退出时恢复）。

    private GameObject _shieldVisual;                   // 呼吸灯护盾子对象。
    private SpriteRenderer _shieldRenderer;             // 呼吸灯护盾渲染器。
    private static Sprite _generatedShieldSprite;       // 复用生成的圆形护盾贴图。

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _paralysis = gameObject.AddComponent<ParalysisDebuff>();
        _selfParalysis = gameObject.AddComponent<ThunderstormParalysisDebuff>();
        _selfParalysis.SetDuration(selfParalysisSeconds);
        CreateShieldVisual();
    }

    private void OnEnable()
    {
        _prop.OnAtt += HandleAttack;
        _prop.OnHitted += HandleHitted;

        _phase = Phase.Idle;
        _shieldHp = 0;
        _immobilized = false;
        SetShieldVisual(false);
    }

    private void OnDisable()
    {
        _prop.OnAtt -= HandleAttack;
        _prop.OnHitted -= HandleHitted;

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

                UpdateShieldBreathing();
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

    /// <summary>被攻击：释放电球反击伤害来源，随后护盾吸收本次伤害。</summary>
    private void HandleHitted(Damage damage)
    {
        if (_phase != Phase.Thunder || _prop == null)
            return;

        Damage calculated = DamageComputor.DamageCompute(damage);
        int taken = calculated.missed ? 0 : calculated.finalDamage;

        // 反击：对伤害来源释放电球（未命中不反击）。
        if (taken > 0 && damage.source != null)
        {
            int counter = Mathf.Max(1, Mathf.RoundToInt(taken * counterDamageMultiplier));
            ReleaseBall(damage.source, counter);
        }

        // 护盾吸收：预补血，随后的 TakeDamage 会把实际伤害扣掉。
        if (_shieldHp > 0)
        {
            int absorbed = Mathf.Min(_shieldHp, taken);
            if (absorbed > 0)
            {
                _shieldHp -= absorbed;
                _prop.currentHp += absorbed;
            }

            if (_shieldHp <= 0)
                EndThunderMode();
        }
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

        _selfParalysis.ApplyBuff(_prop);
        if (!_prop.currentDebuff.Contains(_selfParalysis))
            _prop.currentDebuff.Add(_selfParalysis);
    }

    /// <summary>
    /// 从对象池取电球并发射：电球飞向伤害来源，命中后结算反击伤害并附带一次麻痹。
    /// </summary>
    private void ReleaseBall(GameObject attacker, int counterDamage)
    {
        if (ballPrefab == null || attacker == null)
            return;

        GameObject ball = GameObjectPool.Instance.Get(ballPrefab);
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
    /// 创建护盾视觉：单位 Y 轴上方 shieldVisualHeight 处的圆形呼吸灯渲染器，
    /// 初始隐藏，雷霆模式期间显示并呼吸。
    /// </summary>
    private void CreateShieldVisual()
    {
        _shieldVisual = new GameObject("ThunderShieldVisual");
        _shieldVisual.transform.SetParent(transform, false);
        _shieldVisual.transform.localPosition = new Vector3(0f, shieldVisualHeight, 0f);
        _shieldVisual.transform.localScale = Vector3.one * shieldVisualScale;

        _shieldRenderer = _shieldVisual.AddComponent<SpriteRenderer>();
        _shieldRenderer.sprite = GetGeneratedShieldSprite();
        _shieldRenderer.sortingLayerID = 495858691;   // OnMap 层，盖在单位身体（order 0）之上。
        _shieldRenderer.sortingOrder = 1;
        _shieldRenderer.color = new Color(1f, 1f, 1f, breathMaxAlpha);

        _shieldVisual.SetActive(false);
    }

    private void SetShieldVisual(bool visible)
    {
        if (_shieldVisual != null)
            _shieldVisual.SetActive(visible);
    }

    /// <summary>驱动呼吸灯：透明度在最低与最高之间按正弦波动。</summary>
    private void UpdateShieldBreathing()
    {
        if (_shieldRenderer == null || _shieldVisual == null || !_shieldVisual.activeSelf)
            return;

        float t = 0.5f + 0.5f * Mathf.Sin(Time.time * breathSpeed * Mathf.PI * 2f);
        Color color = _shieldRenderer.color;
        color.a = Mathf.Lerp(breathMinAlpha, breathMaxAlpha, t);
        _shieldRenderer.color = color;
    }

    /// <summary>生成并缓存蓝白色圆形护盾贴图（边缘亮、内部半透明）。</summary>
    private static Sprite GetGeneratedShieldSprite()
    {
        if (_generatedShieldSprite != null)
            return _generatedShieldSprite;

        const int size = 128;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "ThunderShieldCircle";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.47f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = 0f;
                if (distance <= 1f)
                    alpha = distance >= 0.8f ? 0.9f : 0.15f;

                texture.SetPixel(x, y, new Color(0.35f, 0.78f, 1f, alpha));
            }
        }

        texture.Apply();

        _generatedShieldSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _generatedShieldSprite.name = "ThunderShieldCircleSprite";
        return _generatedShieldSprite;
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
