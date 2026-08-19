using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可复用的哨塔类建筑 AI：复用 BuildingAI 的逐帧 AIBehaviour 扩展点，
/// 在攻击范围内通过 MapCells 的边界占用查询索敌，命中后从对象池发射配置的弹幕，
/// 开火时播放攻击音效并执行一次摇头式震动作为攻击反馈。
/// 数据全部取自 GameObjectProperty（atk/atkRate/atkRange/atkObj/side/repel），
/// 不自行维护第二套战斗属性；atkRate 视为每秒射击次数。
/// 任何种族的防御塔预制体只需挂载本组件并配置以下内容即可工作，无需新增代码：
/// GameObjectProperty 的 atk/atkRange/atkObj/atkRate/side，伤害类型 damageType，攻击音效键 attackSoundKey。
/// </summary>
[RequireComponent(typeof(GameObjectProperty))]
public class BuildingTowerAI : BuildingAI
{
    [Header("发射点")]
    [SerializeField, Tooltip("弹幕生成位置；留空时按 Shoot point / ShootPoint 名称查找")]
    private Transform shootPoint;

    [Header("攻击伤害")]
    [SerializeField, Tooltip("弹幕的伤害类型；物理塔用 normal，法系塔可改为 magic")]
    private DamageType damageType = DamageType.normal;

    [Header("攻击音效")]
    [SerializeField, Tooltip("每次开火播放的音频资源键；留空则不开火音效")]
    [ResourceKey(typeof(AudioClip))]
    private string attackSoundKey = "Arrow-Shoot";
    [SerializeField, Tooltip("播放攻击音效的音频源；留空时复用对象上的 AudioSource 或自动创建")]
    private AudioSource attackAudio;

    [Header("攻击反馈：摇头震动")]
    [SerializeField, Min(0f)] private float shakeAngle = 4f;          // 震动最大偏转角（度）。
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.25f; // 震动持续秒。
    [SerializeField, Range(1f, 10f)] private float shakeWaves = 2.5f; // 震动往返波数。

    private GameObjectProperty _prop;                                 // 建筑的战斗属性数据。
    private BuildingBase _base;                                       // 建筑基类，用于放置预览状态判定。

    private readonly HashSet<GameObject> _boundsCache =
        new HashSet<GameObject>();                                    // 复用的边界占用查询缓冲，避免每帧分配。
    private readonly List<GameObjectProperty> _enemyCache =
        new List<GameObjectProperty>();                               // 复用的敌方候选缓冲。

    private float _attackTimer;                                       // 距上次射击的剩余冷却秒。
    private Quaternion _baseRotation;                                 // 震动前缓存的基准旋转。
    private bool _shaking;                                            // 当前是否正在播放摇头震动。
    private float _shakeElapsed;                                      // 震动已播放秒数。

    private bool _warnedMissingAtkObj;                                // 是否已输出过弹幕键缺失警告（一次性）。
    private bool _warnedMissingBullet;                                // 是否已输出过弹幕未加载警告（一次性）。
    private bool _warnedMissingPool;                                  // 是否已输出过对象池缺失警告（一次性）。
    private bool _warnedMissingSound;                                 // 是否已输出过攻击音效缺失警告（一次性）。

    #region 生命周期与回调
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _base = GetComponent<BuildingBase>();
        _baseRotation = transform.rotation;
        ResolveShootPoint();
        ResolveAttackAudio();
    }

    /// <summary>
    /// 解析攻击音效音频源：优先 Inspector 配置，其次复用对象上已有的 AudioSource（例如 BuildUP 创建的），
    /// 都没有时新建一个（仅在 Awake 执行一次，不属于热路径）。
    /// </summary>
    private void ResolveAttackAudio()
    {
        if (attackAudio != null)
            return;

        attackAudio = GetComponent<AudioSource>();
        if (attackAudio == null)
        {
            attackAudio = gameObject.AddComponent<AudioSource>();
            attackAudio.playOnAwake = false;
            attackAudio.spatialBlend = 0f;
        }
    }

    private void OnDisable()
    {
        // 停用时立即复位旋转，避免震动残留在池化或场景切换后的建筑上。
        _shaking = false;
        transform.rotation = _baseRotation;
    }

    /// <summary>
    /// 解析弹幕发射点：优先使用 Inspector 配置，未配置时按 Shoot point / ShootPoint 名称查找，
    /// 均未找到时退回使用建筑自身位置。
    /// </summary>
    private void ResolveShootPoint()
    {
        if (shootPoint != null)
            return;

        shootPoint = transform.Find("Shoot point");
        if (shootPoint == null)
            shootPoint = transform.Find("ShootPoint");
    }
    #endregion

    #region AI 行为
    /// <summary>
    /// 每帧驱动哨塔：存活、不在拖拽预览、且施工完成时，先推进摇头震动，再维护目标与开火计时。
    /// 目标缺失或失效时通过 MapCells 边界查询重新索敌，开火后按 atkRate 进入冷却。
    /// 注意：建筑死亡判定使用 GameObjectProperty.isDead（BuildingHealth.IsDead 尚未实现，禁止调用）。
    /// 拖拽预览与施工动画阶段只展示放置/建造表现，不参与战斗。
    /// </summary>
    protected override void AIBehaviour()
    {
        if (_prop == null || _prop.isDead)
        {
            UpdateShake();
            return;
        }

        // 拖拽放置预览中的建筑只显示能否建造，不执行索敌与开火。
        if (BuildingPlace.Instance != null && _base != null &&
            BuildingPlace.Instance.IsBuildingInPreview(_base))
        {
            UpdateShake();
            return;
        }

        // 施工动画期间（BuildRoutine 未完成）不参与战斗。
        if (_base != null && !_base.IsCompleted)
        {
            UpdateShake();
            return;
        }

        UpdateShake();

        if (_prop.target == null || !IsTargetValid(_prop.target))
            AcquireTarget();

        _attackTimer -= Time.deltaTime;
        if (_attackTimer > 0f || _prop.target == null)
            return;

        FireAt(_prop.target);
        _attackTimer = GetFireInterval();
    }

    /// <summary>
    /// 按攻击范围计算塔身为中心的矩形，从 MapCells 收集该边界内的占用对象，
    /// 筛选出敌方可战斗对象后锁定最近的一个作为目标。
    /// </summary>
    private void AcquireTarget()
    {
        MapCells map = MapCells.Instance;
        if (map == null)
            return;

        int halfX = Mathf.Max(1, _prop.atkRange.x / 2);
        int halfY = Mathf.Max(1, _prop.atkRange.y / 2);
        Vector2Int center = new Vector2Int(
            (int)transform.position.x,
            (int)transform.position.y);

        _boundsCache.Clear();
        map.CollectOccupiersInBounds(
            center - new Vector2Int(halfX, halfY),
            center + new Vector2Int(halfX, halfY),
            _boundsCache);

        _enemyCache.Clear();
        foreach (GameObject obj in _boundsCache)
        {
            if (obj == null)
                continue;

            GameObjectProperty p = obj.GetComponent<GameObjectProperty>();
            if (p == null || p == _prop || p.isDead || p.isUntargetable ||
                p.side == _prop.side ||
                p.GetComponent<ICollide>() == null)
                continue;

            _enemyCache.Add(p);
        }

        if (_enemyCache.Count == 0)
        {
            _prop.target = null;
            return;
        }

        GameObjectProperty best = _enemyCache[0];
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _enemyCache.Count; i++)
        {
            float sqr = (_enemyCache[i].transform.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = _enemyCache[i];
            }
        }

        _prop.target = best.gameObject;
    }

    /// <summary>
    /// 判断目标是否仍然有效且位于攻击范围内。
    /// </summary>
    private bool IsTargetValid(GameObject target)
    {
        if (target == null)
            return false;

        GameObjectProperty p = target.GetComponent<GameObjectProperty>();
        if (p == null || p.isDead || p.isUntargetable)
            return false;

        return IsTargetInRange(target);
    }

    /// <summary>
    /// 判断目标中心是否位于塔身为中心的矩形攻击范围内。
    /// </summary>
    private bool IsTargetInRange(GameObject target)
    {
        int halfX = Mathf.Max(1, _prop.atkRange.x / 2);
        int halfY = Mathf.Max(1, _prop.atkRange.y / 2);
        Vector2Int center = new Vector2Int(
            (int)transform.position.x,
            (int)transform.position.y);
        Vector2Int pos = new Vector2Int(
            (int)target.transform.position.x,
            (int)target.transform.position.y);

        return Mathf.Abs(pos.x - center.x) <= halfX &&
               Mathf.Abs(pos.y - center.y) <= halfY;
    }
    #endregion

    #region 射击
    /// <summary>
    /// 从对象池取得配置弹幕，在发射点生成并朝向目标，写入伤害与归属后发布攻击事件。
    /// 与 CharacterAI.ShootProjectile 保持同一套生成与伤害写入流程。
    /// 弹幕键、弹幕加载或对象池缺失时给出一次性警告，避免静默失败。
    /// </summary>
    private void FireAt(GameObject target)
    {
        if (string.IsNullOrEmpty(_prop.atkObj))
        {
            if (!_warnedMissingAtkObj)
            {
                _warnedMissingAtkObj = true;
                Debug.LogWarning($"[BuildingTowerAI] {name} 未配置 atkObj，哨塔无法攻击。", this);
            }
            return;
        }

        GameObject bulletPrefab = ResourceManager.Instance != null
            ? ResourceManager.Instance.GetGameObject(_prop.atkObj)
            : null;
        if (bulletPrefab == null)
        {
            if (!_warnedMissingBullet)
            {
                _warnedMissingBullet = true;
                Debug.LogWarning(
                    $"[BuildingTowerAI] 弹幕资源 {_prop.atkObj} 未加载（ResourceManager 缓存缺失），哨塔无法攻击。",
                    this);
            }
            return;
        }

        if (GameObjectPool.Instance == null)
        {
            if (!_warnedMissingPool)
            {
                _warnedMissingPool = true;
                Debug.LogWarning("[BuildingTowerAI] 场景缺少 GameObjectPool，哨塔无法生成弹幕。", this);
            }
            return;
        }

        GameObject bullet = GameObjectPool.Instance.Get(bulletPrefab);
        if (bullet == null)
            return;

        Transform spawn = shootPoint != null ? shootPoint : transform;
        bullet.transform.position = spawn.position;

        Vector3 direction = target.transform.position - spawn.position;
        if (direction.sqrMagnitude > 0.001f)
            bullet.transform.right = direction.normalized;

        DamageSource ds = bullet.GetComponent<DamageSource>();
        if (ds != null)
        {
            ds.damage.initialDamage = _prop.atk;
            ds.damage.source = gameObject;
            ds.damage.side = _prop.side;
            ds.damage.repel = _prop.repel;
            ds.damage.type = damageType;
            ds.target = target;
        }

        _prop.OnAtt?.Invoke();
        PlayAttackSound();
        StartShake();
    }

    /// <summary>
    /// 每次开火播放配置的攻击音效；资源键或音频片段缺失时输出一次性警告，避免静默失败。
    /// 距离与优先级处理交给 AudioManager，与 BuildUP 的建筑音效流程保持一致。
    /// </summary>
    private void PlayAttackSound()
    {
        if (string.IsNullOrEmpty(attackSoundKey) ||
            attackAudio == null ||
            ResourceManager.Instance == null ||
            AudioManager.Instance == null)
            return;

        AudioClip clip = ResourceManager.Instance.GetAudio(attackSoundKey);
        if (clip == null)
        {
            if (!_warnedMissingSound)
            {
                _warnedMissingSound = true;
                Debug.LogWarning($"[BuildingTowerAI] 音频资源 {attackSoundKey} 未加载，攻击音效无法播放。", this);
            }
            return;
        }

        attackAudio.clip = clip;
        attackAudio.volume = 1f;
        attackAudio.priority = 32;
        AudioManager.Instance.PlayEffectAt(attackAudio, (uint)attackAudio.priority, transform);
    }

    /// <summary>
    /// 由攻击频率换算的射击冷却秒；atkRate 视为每秒射击次数，至少 0.1 秒一发。
    /// </summary>
    private float GetFireInterval()
    {
        return 1f / Mathf.Max(0.01f, _prop.atkRate);
    }
    #endregion

    #region 摇头震动
    /// <summary>
    /// 开始一次摇头式震动：以正弦衰减在基准旋转两侧摆动 shakeAngle 度。
    /// </summary>
    private void StartShake()
    {
        _shaking = true;
        _shakeElapsed = 0f;
    }

    /// <summary>
    /// 逐帧推进摇头震动，结束后恢复基准旋转。
    /// </summary>
    private void UpdateShake()
    {
        if (!_shaking)
            return;

        _shakeElapsed += Time.deltaTime;
        if (_shakeElapsed >= shakeDuration)
        {
            _shaking = false;
            transform.rotation = _baseRotation;
            return;
        }

        float t = _shakeElapsed / shakeDuration;
        float wave = Mathf.Sin(t * shakeWaves * Mathf.PI * 2f) * (1f - t);
        transform.rotation = _baseRotation * Quaternion.Euler(0f, 0f, wave * shakeAngle);
    }
    #endregion
}
