// using Unity.Mathematics;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class BuildingHealth : MonoBehaviour, ICollide
{
    private int hp; public int HP => hp;                                      // 当前建筑生命值及其只读访问器。
    private float hideTime = -1f;                                             // 血条自动隐藏的游戏时间，负数表示未计时。

    [Header("受击表现")]
    [SerializeField] private float hitFlashDuration = 0.3f;                    // 受击闪红持续时长。
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.4f, 0.4f, 1f);  // 受击闪红颜色（与角色受击色一致）。
    [SerializeField, Min(0f)] private float shakeDuration = 0.5f;              // 左右晃动持续时长。
    [SerializeField, Min(0f)] private float shakeAmplitude = 0.3f;             // 晃动幅度（世界单位，建议小于半格）。
    [SerializeField, Min(0f)] private float shakeFrequency = 30f;              // 晃动频率（每秒振荡次数）。
    [ResourceKey(typeof(AudioClip))]
    [SerializeField] private string hitSoundKey = "Construct-Hit";             // 受击音效资源键。

    [Header("死亡表现")]
    [SerializeField, Tooltip("死亡掉落期间的旋转角速度（度/秒，与兵种一致）")]
    private float deathAngularSpeed = 180f;                                     // 死亡掉落期间的旋转角速度。
    [SerializeField] private float deathParabolaAcceleration = -50f;            // 死亡抛物线的纵向加速度。
    [SerializeField] private Vector2 deathInitialVelocity = new Vector2(4f, 20f); // 死亡抛飞的水平和纵向初速度。
    [SerializeField] private float deathEffectDuration = 1f;                    // 死亡掉落效果持续时间。
    [SerializeField, Tooltip("死亡掉落结束时的透明度（半透明）")]
    private float deathFadeAlpha = 0.5f;                                        // 死亡掉落结束时的透明度。
    [SerializeField, Tooltip("死亡掉落结束时的变黑系数（0.4 = 亮度降到 40%）")]
    private float deathDarkFactor = 0.4f;                                       // 死亡掉落结束时的变暗系数。

    private SpriteRenderer _bodyRenderer;                                     // 建筑本体渲染器（闪红用）。
    private AudioSource _hitAudio;                                            // 受击音效的缓存音频源。
    private BuildingBase _buildingBase;                                       // 建筑生命周期组件（拖拽预览判定）。
    private Coroutine _hitEffectCoroutine;                                    // 当前受击闪红与晃动协程。
    private Color _flashOriginalColor;                                        // 闪红前建筑本体的原始颜色。
    private bool _flashActive;                                                // 闪红是否生效中。
    private Vector3 _shakeBasePos;                                            // 晃动前建筑的世界坐标。
    private bool _shakeActive;                                                // 晃动是否生效中。
    private Coroutine _deathEffectCoroutine;                                  // 当前死亡掉落协程。
    private SpriteRenderer[] _deathRenderers;                                 // 死亡渐隐的目标渲染器（建筑本体等）。
    private Color[] _deathOriginalColors;                                     // 死亡渐隐开始前的原始颜色。
    private bool _deathColorsActive;                                          // 死亡渐隐是否生效中。

    private GameObjectProperty _prop;                                         // 提供阵营、最大生命和血条持续时间的建筑属性。
    public GameObject HpBarUp;                                                // 通过横向缩放显示剩余生命的前景条。
    public GameObject HpBarBottom;                                            // 血条背景对象。

    // ICollide implementation retained
    #region 阵营判定
    /// <summary>
    /// 比较伤害来源阵营与建筑阵营，判断是否应忽略友方碰撞。
    /// </summary>
    /// <param name="damage">包含来源阵营的伤害数据。</param>
    /// <returns>伤害阵营与建筑阵营相同时返回 <see langword="true"/>。</returns>
    public bool IsFriendly(Damage damage)
    {
        GameObjectProperty prop = EnsureProp();
        return prop != null && damage.side == prop.side;
    }
    #endregion
    #region 碰撞与生命周期回调
    /// <summary>
    /// 接收敌方碰撞伤害，记录来源名称并将伤害交给建筑伤害计算入口。
    /// </summary>
    /// <param name="damage">碰撞源携带的伤害数据。</param>
    /// <returns>建筑伤害计算后的结果。</returns>
    public Damage OnCollide(Damage damage)
    {
        return TakeDamage(damage);
    }
    
    /// <summary>
    /// 缓存同一对象上的建筑属性组件；同对象缺失时向上级物体兜底查找
    /// （防御预制体把 BuildingHealth 挂在子物体、GameObjectProperty 在根物体的配置）。
    /// 同时缓存建筑本体渲染器与受击音效音频源（与 BuildUP 同做法）。
    /// </summary>
    private void Awake()
    {
        EnsureProp();
        _bodyRenderer = GetComponent<SpriteRenderer>();
        _buildingBase = GetComponent<BuildingBase>();
        _hitAudio = GetComponent<AudioSource>();
        if (!_hitAudio)
        {
            _hitAudio = gameObject.AddComponent<AudioSource>();
            _hitAudio.playOnAwake = false;
            _hitAudio.spatialBlend = 0f;
        }
    }

    private void OnEnable()
    {
        _deathEffectCoroutine = null;   // 池化复用：清除旧死亡协程引用。

        // 预载受击音效（已缓存则跳过，避免每个建筑重复发起加载）。
        if (ResourceManager.Instance != null && !string.IsNullOrEmpty(hitSoundKey) &&
            ResourceManager.Instance.GetAudio(hitSoundKey) == null)
        {
            ResourceManager.Instance.LoadExtraResourceAsync<AudioClip>(hitSoundKey);
        }
    }

    private void OnDisable()
    {
        // 池化回收/建筑失效：停止受击表现并恢复颜色与位置。
        StopHitEffect();
        if (_deathEffectCoroutine != null)
        {
            StopCoroutine(_deathEffectCoroutine);
            _deathEffectCoroutine = null;
        }
        RestoreDeathColors();
    }

    /// <summary>
    /// 懒解析 GameObjectProperty：优先同物体，其次父级物体。
    /// 同时解决 Awake 执行顺序不确定导致的空引用（BuildUP.Awake 可能先于本组件 Awake 调用 SetMaxHp）。
    /// </summary>
    private GameObjectProperty EnsureProp()
    {
        if (_prop == null)
        {
            _prop = GetComponent<GameObjectProperty>();
            if (_prop == null)
                _prop = GetComponentInParent<GameObjectProperty>();
        }
        return _prop;
    }


    /// <summary>
    /// 初始化血条缩放，并在建筑开始时隐藏血条。
    /// </summary>
    private void Start()
    {
        ApplyBarVisual();
        SetBarActive(false);
    }

    /// <summary>
    /// 达到预定隐藏时间时关闭血条并清除计时状态。
    /// </summary>
    private void Update()
    {
        if (hideTime >= 0f && Time.time >= hideTime)
        {
            SetBarActive(false);
            hideTime = -1f;
        }
    }
    #endregion

    #region 血条控制
    /// <summary>
    /// 将百分比限制在 0 到 1 后换算为建筑生命值，并刷新和临时显示血条。
    /// </summary>
    /// <param name="percent">相对于最大生命值的目标比例。</param>
    public void SetPercentHp(float percent)
    {
        GameObjectProperty prop = EnsureProp();
        if (prop == null)
            return;

        hp = Mathf.RoundToInt(prop.maxHp * Mathf.Clamp01(percent));
        ApplyBarVisual();
        ShowBarTemporarily();
    }

    /// <summary>
    /// 按当前生命值刷新血条，并重新开始自动隐藏计时。
    /// </summary>
    public void SetHpbar()
    {
        ApplyBarVisual();
        ShowBarTemporarily();
    }
    #endregion

    #region 伤害与治疗
    /// <summary>
    /// 调用全局伤害计算器生成伤害结果，并把最终伤害扣除到建筑生命值，
    /// 刷新血条并在生命归零时标记建筑死亡。
    /// </summary>
    /// <param name="damage">需要计算的原始伤害数据。</param>
    /// <returns>写入最终伤害值后的伤害结果。</returns>
    public Damage TakeDamage(Damage damage)
    {
        Damage d = DamageComputor.DamageCompute(damage);
        if (IsDead() || _deathEffectCoroutine != null)
            return d;

        hp = Mathf.Max(0, hp - d.finalDamage);
        ApplyBarVisual();
        ShowBarTemporarily();
        if (_prop != null)
            _prop.isDead = hp <= 0;

        // 未命中（目盲等）同样弹出 miss 跳字。
        if (d.missed)
            DamageTextPool.Instance.ShowMiss(transform.position + Vector3.up);
        else
            StartHitEffect();                      // 受击表现：闪红 + 左右剧烈晃动 + Construct-Hit 音效。

        // HP ≤ 0 的死亡规则：与兵种一致（死亡标记 + 掉落死亡动画 + 对象池回收）。
        if (hp <= 0)
            Die(d);

        return d;
    }

    /// <summary>
    /// 建筑治疗入口；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    /// <param name="amount">计划恢复的生命值。</param>
    public void Heal(int amount)
    {
        // TODO: Implement heal logic.
        throw new System.NotImplementedException();
    }
    #endregion

    #region 生命值快捷操作
    /// <summary>
    /// 恢复满生命入口；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    public void RestoreFullHp()
    {
        // TODO: Implement full HP restore logic.
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// 将生命降为零并按 HP ≤ 0 的死亡规则进入死亡流程。
    /// </summary>
    public void ReduceToZero()
    {
        hp = 0;
        if (_prop != null)
            _prop.isDead = true;
        Die();
    }
    #endregion

    #region 死亡与复活
    /// <summary>
    /// 建筑死亡入口（无来源伤害）：按 HP ≤ 0 的死亡规则播放与兵种相同的掉落死亡动画。
    /// </summary>
    public void Die()
    {
        Die(Damage.DefaultDamage);
    }

    /// <summary>
    /// 将建筑标记为死亡、停止受击表现，并按致死方向播放与兵种相同的
    /// 掉落死亡动画（抛物线 + 旋转 + 半透明变黑），结束后回收进对象池。
    /// </summary>
    /// <param name="damage">用于确定死亡掉落方向的致死伤害数据。</param>
    private void Die(Damage damage)
    {
        if (_deathEffectCoroutine != null)
            return;

        if (_prop != null)
        {
            _prop.isDead = true;
            _prop.isAttack = false;
            _prop.target = null;
        }

        StopHitEffect();

        _deathEffectCoroutine = StartCoroutine(DeathEffectCoroutine(damage));
    }

    /// <summary>
    /// 建筑复活入口；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    public void Revive()
    {
        // TODO: Implement revive logic.
        throw new System.NotImplementedException();
    }
    #endregion

    #region 生命状态查询与设置
    /// <summary>
    /// 查询建筑是否死亡（生命值归零）。
    /// </summary>
    /// <returns>生命值小于等于 0 时返回 <see langword="true"/>。</returns>
    public bool IsDead()
    {
        return hp <= 0;
    }

    /// <summary>
    /// 查询当前生命比例；当前尚未实现，调用时会抛出 <see cref="System.NotImplementedException"/>。
    /// </summary>
    /// <returns>实现后应返回当前生命值除以最大生命值的比例。</returns>
    public float GetHpPercent()
    {
        // TODO: Implement HP percent query.
        throw new System.NotImplementedException();
    }

    /// <summary>
    /// 直接设置建筑生命值，限制在有效范围内并刷新血条。
    /// </summary>
    /// <param name="value">计划设置的生命值。</param>
    public void SetHp(int value)
    {
        GameObjectProperty prop = EnsureProp();
        if (prop == null)
            return;

        hp = Mathf.Clamp(value, 0, prop.maxHp);
        ApplyBarVisual();
    }

    /// <summary>
    /// 设置建筑最大生命并保持当前生命不超过新上限（建筑升级等受控系统专用；
    /// 业务脚本不得直接写 maxHp）。
    /// </summary>
    /// <param name="value">新的最大生命值。</param>
    public void SetMaxHp(int value)
    {
        GameObjectProperty prop = EnsureProp();
        if (prop == null)
        {
            Debug.LogError("[BuildingHealth] 未找到 GameObjectProperty（请把本组件与 GameObjectProperty 放在同一物体或根物体上），无法设置最大生命。", this);
            return;
        }

        prop.maxHp = Mathf.Max(1, value);
        hp = Mathf.Clamp(hp, 0, prop.maxHp);
        ApplyBarVisual();
    }
    #endregion

    #region 受击表现
    /// <summary>
    /// 启动受击表现：播放 Construct-Hit 音效、建筑本体闪红并左右剧烈晃动。
    /// 连续受击时重开协程（刷新基准位置与计时）；拖拽预览中的建筑不晃动。
    /// </summary>
    private void StartHitEffect()
    {
        // 音效：经缓存音频源走 AudioManager 统一播放入口。
        if (!string.IsNullOrEmpty(hitSoundKey) && ResourceManager.Instance != null &&
            AudioManager.Instance != null && _hitAudio != null)
        {
            AudioClip clip = ResourceManager.Instance.GetAudio(hitSoundKey);
            if (clip != null)
            {
                _hitAudio.clip = clip;
                _hitAudio.volume = 1f;
                _hitAudio.priority = 32;
                AudioManager.Instance.PlayEffectAt(_hitAudio, (uint)_hitAudio.priority, transform);
            }
        }

        // 闪红：记录受击前的原始颜色（不覆盖升级蓝/拆除红等既有着色），随后置为闪红色。
        if (_bodyRenderer != null)
        {
            if (!_flashActive)
            {
                _flashOriginalColor = _bodyRenderer.color;
                _flashActive = true;
            }
            _bodyRenderer.color = hitFlashColor;
        }

        // 晃动与闪红共用一个协程；连续受击时重开以刷新计时。
        if (_hitEffectCoroutine != null)
            StopCoroutine(_hitEffectCoroutine);
        _hitEffectCoroutine = StartCoroutine(HitEffectCoroutine());
    }

    /// <summary>
    /// 受击表现协程：衰减正弦驱动建筑左右晃动，闪红到时长后恢复原色，结束复位坐标。
    /// </summary>
    private IEnumerator HitEffectCoroutine()
    {
        _shakeBasePos = transform.position;
        _shakeActive = true;
        float elapsed = 0f;
        float total = Mathf.Max(hitFlashDuration, shakeDuration);

        while (elapsed < total)
        {
            elapsed += Time.deltaTime;

            // 左右剧烈晃动（拖拽预览中跳过，避免与放置系统抢位移）。
            if (_shakeActive && elapsed < shakeDuration &&
                !(BuildingPlace.Instance != null && _buildingBase != null &&
                  BuildingPlace.Instance.IsBuildingInPreview(_buildingBase)))
            {
                float t = elapsed / shakeDuration;
                float damping = 1f - t;
                float offsetX = Mathf.Sin(elapsed * shakeFrequency * 2f * Mathf.PI) *
                                shakeAmplitude * damping;
                transform.position = new Vector3(
                    _shakeBasePos.x + offsetX, _shakeBasePos.y, _shakeBasePos.z);
            }
            else if (_shakeActive && elapsed >= shakeDuration)
            {
                transform.position = _shakeBasePos;
                _shakeActive = false;
            }

            // 闪红结束恢复原始颜色。
            if (_flashActive && elapsed >= hitFlashDuration)
            {
                if (_bodyRenderer != null)
                    _bodyRenderer.color = _flashOriginalColor;
                _flashActive = false;
            }

            yield return null;
        }

        StopHitEffectVisuals();
        _hitEffectCoroutine = null;
    }

    /// <summary>恢复受击表现修改过的颜色与位置，并清除状态标记。</summary>
    private void StopHitEffect()
    {
        if (_hitEffectCoroutine != null)
        {
            StopCoroutine(_hitEffectCoroutine);
            _hitEffectCoroutine = null;
        }
        StopHitEffectVisuals();
    }

    private void StopHitEffectVisuals()
    {
        if (_shakeActive)
        {
            transform.position = _shakeBasePos;
            _shakeActive = false;
        }
        if (_flashActive && _bodyRenderer != null)
        {
            _bodyRenderer.color = _flashOriginalColor;
            _flashActive = false;
        }
    }
    #endregion

    #region 死亡表现
    /// <summary>
    /// 与兵种相同的掉落死亡动画：按致死方向做抛物线飞出并旋转，
    /// 同时渐隐为半透明 + 变黑；结束后恢复变换与颜色并回收进对象池。
    /// </summary>
    /// <param name="damage">用于确定死亡掉落方向的致死伤害数据。</param>
    /// <returns>逐帧更新死亡掉落效果直到结束的协程。</returns>
    private IEnumerator DeathEffectCoroutine(Damage damage)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startScale = transform.localScale;
        int direction = GetDeathDirection(damage);
        float horizontalSpeed = Mathf.Abs(deathInitialVelocity.x) * direction;
        float elapsed = 0f;

        CaptureDeathColors();

        while (elapsed < deathEffectDuration)
        {
            float time = elapsed;
            transform.position = startPosition + new Vector3(
                horizontalSpeed * time,
                deathInitialVelocity.y * time + 0.5f * deathParabolaAcceleration * time * time,
                0f);
            transform.Rotate(Vector3.forward, deathAngularSpeed * Time.deltaTime);
            SetDeathColor(Mathf.Clamp01(elapsed / deathEffectDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreDeathColors();
        transform.position = startPosition;
        transform.rotation = startRotation;
        transform.localScale = startScale;
        _deathEffectCoroutine = null;
        ReleaseAfterDeath();
    }

    /// <summary>死亡渐隐开始前缓存全部建筑渲染器的当前颜色（含建筑本体与子级部件）。</summary>
    private void CaptureDeathColors()
    {
        if (_deathRenderers == null)
            _deathRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        _deathOriginalColors = new Color[_deathRenderers.Length];
        for (int i = 0; i < _deathRenderers.Length; i++)
        {
            if (_deathRenderers[i] != null)
                _deathOriginalColors[i] = _deathRenderers[i].color;
        }

        _deathColorsActive = true;
    }

    /// <summary>
    /// 按死亡进度把建筑渲染器渐变为“半透明 + 变黑”（t=0 原色，t=1 最终暗淡状态）。
    /// </summary>
    /// <param name="t">死亡效果进度（0-1）。</param>
    private void SetDeathColor(float t)
    {
        for (int i = 0; i < _deathRenderers.Length; i++)
        {
            SpriteRenderer renderer = _deathRenderers[i];
            if (renderer == null)
                continue;

            Color original = _deathOriginalColors[i];
            Color dark = new Color(
                original.r * deathDarkFactor,
                original.g * deathDarkFactor,
                original.b * deathDarkFactor,
                original.a * deathFadeAlpha);
            renderer.color = Color.Lerp(original, dark, t);
        }
    }

    /// <summary>恢复死亡渐隐修改过的全部渲染器颜色，并清除生效标记。</summary>
    private void RestoreDeathColors()
    {
        if (!_deathColorsActive || _deathRenderers == null)
            return;

        for (int i = 0; i < _deathRenderers.Length; i++)
        {
            if (_deathRenderers[i] != null)
                _deathRenderers[i].color = _deathOriginalColors[i];
        }

        _deathColorsActive = false;
    }

    /// <summary>
    /// 优先根据伤害来源与建筑的相对位置确定掉落方向；
    /// 来源无效或重合时退回使用碰撞方向。
    /// </summary>
    /// <param name="damage">致死伤害数据。</param>
    /// <returns>-1 表示向左掉落，1 表示向右掉落。</returns>
    private int GetDeathDirection(Damage damage)
    {
        if (damage.source != null)
        {
            float sourceToBuilding = transform.position.x - damage.source.transform.position.x;
            if (Mathf.Abs(sourceToBuilding) > 0.001f)
                return sourceToBuilding > 0f ? 1 : -1;
        }

        return damage.collideDir == 0 ? 1 : (damage.collideDir > 0 ? 1 : -1);
    }

    /// <summary>
    /// 死亡动画结束后将建筑回收进对象池（与兵种死亡、建筑拆除同一回收路径；
    /// 停用时会自动释放地图占用，池化复用时自动回到 1 级初始状态）。
    /// </summary>
    private void ReleaseAfterDeath()
    {
        GameObjectPool pool = GameObjectPool.Instance;
        if (pool == null)
            return;

        pool.Release(gameObject);
    }
    #endregion

    #region 血条显示辅助
    /// <summary>
    /// 根据当前生命值占最大生命值的比例更新前景血条横向缩放。
    /// </summary>
    private void ApplyBarVisual()
    {
        if (HpBarUp == null)
            return;

        GameObjectProperty prop = EnsureProp();
        if (prop == null)
            return;

        // 原尺寸规则：血条贴图重心在左中间，前景条 X 轴直接取血量比例（满血 = 1 = 贴图原尺寸）。
        // 血条常挂在建筑节点下，该节点可能带整体缩放：按父级累计缩放取绝对值补偿，
        // 让血条始终以贴图原尺寸渲染，不随建筑缩放被压短。
        Vector3 parentLossy = HpBarUp.transform.parent != null
            ? HpBarUp.transform.parent.lossyScale
            : Vector3.one;
        float invX = Mathf.Abs(parentLossy.x) > 0.0001f ? 1f / Mathf.Abs(parentLossy.x) : 1f;
        float invY = Mathf.Abs(parentLossy.y) > 0.0001f ? 1f / Mathf.Abs(parentLossy.y) : 1f;

        float scaleX = prop.maxHp > 0 ? (float)hp / prop.maxHp : 0f;    // 血条横向填充比例。
        HpBarUp.transform.localScale = new Vector3(scaleX * invX, invY, 1f);

        if (HpBarBottom != null)
            HpBarBottom.transform.localScale = new Vector3(invX, invY, 1f);
    }

    /// <summary>
    /// 显示血条，并按建筑配置设置下一次自动隐藏时间。
    /// </summary>
    private void ShowBarTemporarily()
    {
        GameObjectProperty prop = EnsureProp();
        if (prop == null)
            return;

        SetBarActive(true);
        hideTime = Time.time + prop.barSustainTime;
    }

    /// <summary>
    /// 同时设置血条前景和背景对象的激活状态。
    /// </summary>
    /// <param name="active">是否显示整组血条对象。</param>
    private void SetBarActive(bool active)
    {
        if (HpBarUp != null)
        {
            HpBarUp.SetActive(active);
        }

        if (HpBarBottom != null)
        {
            HpBarBottom.SetActive(active);
        }
    }
    #endregion
}
