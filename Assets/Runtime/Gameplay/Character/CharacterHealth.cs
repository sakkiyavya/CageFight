using System;
using System.Collections;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterHealth : MonoBehaviour, ICollide
{
    private struct HitColorTarget
    {
        public Renderer renderer;                                                                       // 需要参与受击闪色的渲染器。
        public int colorProperty;                                                                       // 材质使用的颜色属性 ID。
        public Color originalColor;                                                                     // 受击前需要恢复的原始颜色。
    }

    public int HP => _prop != null ? _prop.currentHp : 0;                                               // 当前生命值的安全只读访问器。
    // IStageComponent removed; data handled by GameObjectProperty
    /// <summary>单位死亡时触发，参数为死亡单位；供死亡表现、掉落等扩展订阅。</summary>
    public event Action<GameObject> Died;
    public GameObject HpBarUp;                                                                          // 通过横向缩放显示剩余生命的前景条。
    public GameObject HpBarBottom;                                                                      // 血条背景对象。
    private float hitFlashDuration = 0.3f;                                                              // 受击红色闪烁持续时间。
    private float jellyDuration = 0.6f;                                                                 // 受击果冻形变持续时间。
    private float jellyFrequency = 2f;                                                                  // 受击形变的振荡频率。
    private float jellyAmplitude = 0.1f;                                                                // 受击时向上弹动的最大幅度。
    
    private float deathAngularSpeed = 1440f;                                                            // 死亡抛飞期间的旋转角速度。
    private float deathParabolaAcceleration = -50f;                                                     // 死亡抛物线的纵向加速度。
    private Vector2 deathInitialVelocity = new Vector2(4f, 20f);                                        // 死亡抛飞的水平和纵向初速度。
    private float deathEffectDuration = 1f;                                                             // 死亡抛飞效果持续时间。
    private GameObjectProperty _prop;                                                                   // 提供生命、阵营、击退和状态数据的角色属性。
    private float hideTime = -1f;                                                                       // 血条自动隐藏时间，负数表示未计时。
    private Coroutine _hitEffectCoroutine;                                                              // 当前受击闪色与形变协程。
    private Coroutine _deathEffectCoroutine;                                                            // 当前死亡抛飞协程。
    private Vector3 _hitEffectBasePosition;                                                             // 受击形变开始前的世界坐标。
    private Vector3 _hitEffectBaseScale;                                                                // 受击形变开始前的局部缩放。
    private bool _hasHitEffectState;                                                                    // 是否保存了需要恢复的受击变换状态。
    private readonly System.Collections.Generic.List<HitColorTarget> _hitColorTargets =
        new System.Collections.Generic.List<HitColorTarget>();                                          // 可参与受击闪色的渲染目标。
    private MaterialPropertyBlock _hitPropertyBlock;                                                    // 不实例化材质即可修改颜色的属性块。

    #region 生命周期与碰撞回调
    /// <summary>
    /// 对象从池中启用时清除旧协程和受击状态，并重置血条隐藏计时与显示状态。
    /// </summary>
    private void OnEnable()
    {
        _hitEffectCoroutine = null;
        _deathEffectCoroutine = null;
        _hasHitEffectState = false;
        hideTime = -1f;
        ApplyBarVisual();
        SetBarActive(false);
    }

    /// <summary>
    /// 缓存角色属性组件，创建材质属性块，并收集支持颜色修改的子级渲染器。
    /// </summary>
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _hitPropertyBlock = new MaterialPropertyBlock();
        CacheHitColorTargets();
    }

    /// <summary>
    /// 对象停用时停止受击和死亡协程，并恢复所有被特效修改的颜色与变换。
    /// </summary>
    private void OnDisable()
    {
        if (_hitEffectCoroutine != null)
        {
            StopCoroutine(_hitEffectCoroutine);
            _hitEffectCoroutine = null;
        }
        if (_deathEffectCoroutine != null)
        {
            StopCoroutine(_deathEffectCoroutine);
            _deathEffectCoroutine = null;
        }
        RestoreOriginalColors();
        RestoreHitTransform();
    }

    /// <summary>
    /// 比较伤害来源阵营与角色阵营，判断是否应忽略友方碰撞。
    /// </summary>
    /// <param name="damage">包含伤害来源阵营的数据。</param>
    /// <returns>来源阵营与角色阵营相同时返回 <see langword="true"/>。</returns>
    public bool IsFriendly(Damage damage)
    {
        // 简单示例：如果双方属于同一阵营，则视为友好
        return damage.side == _prop.side;
    }

    /// <summary>
    /// 处理敌方伤害碰撞：向角色挂载伤害携带的 Buff，发布受击事件，
    /// 再执行扣血、击退、跳字和死亡处理。
    /// </summary>
    /// <param name="damage">碰撞源携带的伤害、阵营、击退和 Buff 数据。</param>
    /// <returns>经过伤害计算器处理后的伤害结果；角色已死亡时原样返回。</returns>
    public Damage OnCollide(Damage damage)
    {   
        if (_prop.isDead)
            return damage;

        if(damage.buffs != null && damage.buffs.Length > 0)
        {
            IDebuffConverter converter = GetComponent<IDebuffConverter>();
            IBuffImmunity immunity = GetComponent<IBuffImmunity>();
            foreach(var buff in damage.buffs)
            {
                // 目标免疫该 Buff 时直接忽略。
                if (immunity != null && immunity.IsImmuneTo(buff))
                    continue;

                // 目标实现了减益转化器时，减益交由转化器处理（如转化为自身增益），原减益不再施加。
                if (buff.isDeBuff && converter != null && converter.ConvertDebuff(buff))
                    continue;

                // 统一状态入口：执行 ApplyBuff 并登记到 currentBuff/currentDebuff。
                _prop.ApplyStatus(buff, damage);
            }
        }

        
        _prop.OnHitted?.Invoke(damage);
        return TakeDamage(damage);
    }

    /// <summary>
    /// 初始化血条填充比例，并在角色开始时隐藏血条。
    /// </summary>
    private void Start()
    {
        ApplyBarVisual();
        SetBarActive(false);
    }

    /// <summary>
    /// 到达预定隐藏时间时关闭血条并清除计时状态；
    /// 生物单位血量降到 30% 以下时自动获得“重伤”状态（状态组件不存在时创建，
    /// 效果与解除由重伤状态自身管理；阈值与 HeavyWoundState 的 lowHpPercent 默认一致）。
    /// </summary>
    private void Update()
    {
        if (hideTime >= 0f && Time.time >= hideTime)
        {
            SetBarActive(false);
            hideTime = -1f;
        }

        if (_prop != null && _prop.maxHp > 0 &&
            _prop.currentHp <= _prop.maxHp * 0.3f &&
            GetComponent<HeavyWoundState>() == null)
        {
            gameObject.AddComponent<HeavyWoundState>();
        }
    }
    #endregion

    #region 生命值与游戏逻辑
    /// <summary>
    /// 将百分比限制在 0 到 1 后换算为当前生命，更新死亡标记并刷新血条。
    /// </summary>
    /// <param name="percent">相对于最大生命值的目标比例。</param>
    public void SetPercentHp(float percent)
    {
        _prop.currentHp = Mathf.RoundToInt(_prop.maxHp * Mathf.Clamp01(percent));
        _prop.isDead = _prop.currentHp <= 0;
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

    /// <summary>
    /// 计算并扣除最终伤害，启动受击效果和血条显示，写入击退状态；
    /// 生命归零时进入死亡流程，并显示伤害跳字。
    /// </summary>
    /// <param name="damage">需要应用到角色的原始伤害数据。</param>
    /// <returns>写入最终伤害值后的计算结果。</returns>
    public Damage TakeDamage(Damage damage)
    {
        Damage d = DamageComputor.DamageCompute(damage);
        if (_prop.isDead)
            return d;

        // 统一入伤修正（护盾吸收等）：在正式扣血前修正本次已结算伤害，
        // 保证同一次攻击只结算一遍伤害，不允许实现方预先回血抵消。
        IIncomingDamageModifier damageModifier = GetComponent<IIncomingDamageModifier>();
        if (damageModifier != null)
            d.finalDamage = damageModifier.ModifyIncomingDamage(d);

        _prop.currentHp = Mathf.Max(_prop.currentHp - d.finalDamage, 0);
        RestartHitEffect();
        ShowBarTemporarily();
        if (_prop.blockHits > 0)
        {
            // 坚毅格挡：本次伤害已被降为 1 点，免疫击退，并消耗一层格挡。
            _prop.blockHits--;
        }
        else
        {
            // 重伤等倍率修正：受击退倍率（默认 1，重伤时 1.3）在最终击退位移上乘算。
            _prop.StartRepel(transform.position,
                d.repel / _prop.antiRepel * d.collideDir * _prop.repelTakenMultiplier);
        }
        if(_prop.currentHp <= 0)
        {
            // 死亡前询问死亡复活器：接管则跳过常规死亡流程（如晶化复活）。
            IDeathReviver reviver = GetComponent<IDeathReviver>();
            if (reviver == null || !reviver.TryRevive(gameObject, d))
                Die(d);
        }
        if (d.missed)
            DamageTextPool.Instance.ShowMiss(transform.position + Vector3.up);
        else
            DamageTextPool.Instance.ShowDamage(d, transform.position + Vector3.up);
        return d;
    }

    /// <summary>
    /// 为存活角色恢复生命，限制为最大生命值，并显示血条和治疗跳字。
    /// 受到治疗倍率（重伤时 0.5）作用于本次治疗量。
    /// </summary>
    /// <param name="value">计划恢复的生命值。</param>
    public void Heal(int value) 
    { 
        if (_prop.isDead)
            return;

        int healValue = Mathf.Max(0, Mathf.RoundToInt(value * _prop.healTakenMultiplier));
        _prop.currentHp = Mathf.Min(_prop.currentHp + healValue, _prop.maxHp);
        ShowBarTemporarily();

        DamageTextPool.Instance.ShowHeal(healValue, transform.position + Vector3.up * 1.5f);
    }

    /// <summary>
    /// 将角色恢复到最大生命值、清除死亡标记并刷新血条。
    /// </summary>
    public void RestoreFullHp()
    {
        _prop.currentHp = _prop.maxHp;
        _prop.isDead = false;
        ApplyBarVisual();
    }

    /// <summary>
    /// 将当前生命直接设为零，并使用默认伤害数据进入死亡流程。
    /// </summary>
    public void ReduceToZero()
    {
        _prop.currentHp = 0;
        Die();
    }

    /// <summary>
    /// 使用默认伤害方向触发角色死亡流程。
    /// </summary>
    public void Die()
    {
        Die(Damage.DefaultDamage);
    }

    /// <summary>
    /// 将角色标记为死亡，清除攻击和目标状态，停止受击效果，
    /// 并根据致死伤害启动死亡抛飞协程。
    /// </summary>
    /// <param name="damage">用于确定死亡抛飞方向的致死伤害数据。</param>
    private void Die(Damage damage)
    {
        if (_prop.isDead && _deathEffectCoroutine != null)
            return;

        Died?.Invoke(gameObject);

        _prop.currentHp = 0;
        _prop.isDead = true;
        _prop.isAttack = false;
        _prop.target = null;
        if (_hitEffectCoroutine != null)
        {
            StopCoroutine(_hitEffectCoroutine);
            _hitEffectCoroutine = null;
            RestoreOriginalColors();
            RestoreHitTransform();
        }
        _deathEffectCoroutine = StartCoroutine(DeathEffectCoroutine(damage));
    }

    /// <summary>
    /// 将角色恢复到最大生命值、清除死亡标记并刷新血条。
    /// </summary>
    public void Revive()
    {
        _prop.currentHp = _prop.maxHp;
        _prop.isDead = false;
        ApplyBarVisual();
    }

    /// <summary>
    /// 查询角色属性是否缺失或已经标记为死亡。
    /// </summary>
    /// <returns>角色无法提供有效生命状态或已经死亡时返回 <see langword="true"/>。</returns>
    public bool IsDead() => _prop == null || _prop.isDead;
    /// <summary>
    /// 计算当前生命值占最大生命值的比例。
    /// </summary>
    /// <returns>0 到 1 之间的生命比例；最大生命值无效时返回 0。</returns>
    public float GetHpPercent() => _prop.maxHp > 0 ? (float)_prop.currentHp / _prop.maxHp : 0f;

    /// <summary>
    /// 将生命值限制在有效范围内，更新死亡标记并刷新血条。
    /// </summary>
    /// <param name="value">计划设置的当前生命值。</param>
    public void SetHp(int value)
    {
        _prop.currentHp = Mathf.Clamp(value, 0, _prop.maxHp);
        _prop.isDead = _prop.currentHp <= 0;
        ApplyBarVisual();
    }
    #endregion

    #region 特效与协程
    /// <summary>
    /// 扫描所有子级渲染器，记录支持 _BaseColor 或 _Color 属性的材质及其原始颜色。
    /// </summary>
    private void CacheHitColorTargets()
    {
        _hitColorTargets.Clear();
        var renderers = GetComponentsInChildren<Renderer>(true);
        foreach (var renderer in renderers)
        {
            if (renderer.sharedMaterial == null)
                continue;

            int colorProperty = renderer.sharedMaterial.HasProperty("_BaseColor")
                ? Shader.PropertyToID("_BaseColor")
                : renderer.sharedMaterial.HasProperty("_Color")
                    ? Shader.PropertyToID("_Color")
                    : -1;
            if (colorProperty == -1)
                continue;

            _hitColorTargets.Add(new HitColorTarget
            {
                renderer = renderer,
                colorProperty = colorProperty,
                originalColor = renderer.sharedMaterial.GetColor(colorProperty)
            });
        }
    }

    /// <summary>
    /// 停止并恢复旧受击效果，保存当前变换状态，然后重新启动闪色与果冻形变协程。
    /// </summary>
    private void RestartHitEffect()
    {
        if (_hitEffectCoroutine != null)
        {
            StopCoroutine(_hitEffectCoroutine);
            RestoreOriginalColors();
            RestoreHitTransform();
        }

        _hitEffectBasePosition = transform.position;
        _hitEffectBaseScale = transform.localScale;
        _hasHitEffectState = true;
        _hitEffectCoroutine = StartCoroutine(HitEffectCoroutine());
    }

    /// <summary>
    /// 在受击期间将可用材质短暂变红，并用衰减正弦波驱动角色上弹和果冻形变；
    /// 效果结束后恢复原始颜色、位置和缩放。
    /// </summary>
    /// <returns>逐帧更新受击视觉效果直到结束的协程。</returns>
    private IEnumerator HitEffectCoroutine()
    {
        float elapsed = 0f;
        bool colorRestored = false;
        float effectDuration = Mathf.Max(hitFlashDuration, jellyDuration);

        SetHitColor(Color.red);
        while (elapsed < effectDuration)
        {
            if (elapsed < jellyDuration)
            {
                float jellyTime = elapsed / jellyDuration;
                float damping = 1f - jellyTime;
                float pulse = Mathf.Abs(Mathf.Sin(Mathf.PI * jellyFrequency * jellyTime)) * damping;
                float jelly = Mathf.Sin(Mathf.PI * 2f * jellyFrequency * jellyTime) * damping;
                // 果冻只改变纵向视觉偏移，保留当前 X 坐标，让 CharacterAI.Repel 的位移生效。
                if (!_prop.isRepel)
                    transform.position = new Vector3(
                        transform.position.x,
                        _hitEffectBasePosition.y + pulse * jellyAmplitude,
                        transform.position.z);
                transform.localScale = new Vector3(
                    _hitEffectBaseScale.x * (1f - jelly * 0.14f),
                    _hitEffectBaseScale.y * (1f + jelly * 0.22f),
                    _hitEffectBaseScale.z);
            }
            if (!colorRestored && elapsed >= hitFlashDuration)
            {
                RestoreOriginalColors();
                colorRestored = true;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        RestoreOriginalColors();
        RestoreHitTransform();
        _hitEffectCoroutine = null;
    }

    /// <summary>
    /// 按致死方向驱动角色做带旋转的抛物线飞出效果；
    /// 结束后恢复初始变换，并将死亡实例回收进对象池（不再自动重生）。
    /// </summary>
    /// <param name="damage">用于决定水平抛飞方向的致死伤害。</param>
    /// <returns>逐帧更新死亡抛飞效果直到结束的协程。</returns>
    private IEnumerator DeathEffectCoroutine(Damage damage)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startScale = transform.localScale;
        int direction = GetDeathDirection(damage);
        float horizontalSpeed = Mathf.Abs(deathInitialVelocity.x) * direction;
        float elapsed = 0f;

        while (elapsed < deathEffectDuration)
        {
            float time = elapsed;
            transform.position = startPosition + new Vector3(
                horizontalSpeed * time,
                deathInitialVelocity.y * time + 0.5f * deathParabolaAcceleration * time * time,
                0f);
            transform.Rotate(Vector3.forward, deathAngularSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = startPosition;
        transform.rotation = startRotation;
        transform.localScale = startScale;
        _deathEffectCoroutine = null;
        ReleaseAfterDeath();
    }

    /// <summary>
    /// 优先根据伤害来源与角色的水平相对位置确定抛飞方向；
    /// 来源无效或重合时退回使用碰撞方向。
    /// </summary>
    /// <param name="damage">包含来源对象和碰撞方向的伤害数据。</param>
    /// <returns>-1 表示向左抛飞，1 表示向右抛飞。</returns>
    private int GetDeathDirection(Damage damage)
    {
        if (damage.source != null)
        {
            float sourceToCharacter = transform.position.x - damage.source.transform.position.x;
            if (Mathf.Abs(sourceToCharacter) > 0.001f)
                return sourceToCharacter > 0f ? 1 : -1;
        }

        return damage.collideDir == 0 ? 1 : (damage.collideDir > 0 ? 1 : -1);
    }

    /// <summary>
    /// 死亡后不再自动重生：仅将当前死亡实例回收进对象池
    /// （池化实例停用回收；非池化实例由对象池销毁）。
    /// </summary>
    private void ReleaseAfterDeath()
    {
        GameObjectPool pool = GameObjectPool.Instance;
        if (pool == null)
            return;

        pool.Release(gameObject);
    }

    /// <summary>
    /// 通过材质属性块将所有受击渲染目标临时设置为指定颜色。
    /// </summary>
    /// <param name="color">受击期间需要显示的颜色。</param>
    private void SetHitColor(Color color)
    {
        foreach (var target in _hitColorTargets)
        {
            if (target.renderer == null)
                continue;
            target.renderer.GetPropertyBlock(_hitPropertyBlock);
            _hitPropertyBlock.SetColor(target.colorProperty, color);
            target.renderer.SetPropertyBlock(_hitPropertyBlock);
        }
    }

    /// <summary>
    /// 将所有受击渲染目标恢复为缓存的原始材质颜色。
    /// </summary>
    private void RestoreOriginalColors()
    {
        if (_hitPropertyBlock == null)
            return;

        foreach (var target in _hitColorTargets)
        {
            if (target.renderer == null)
                continue;
            target.renderer.GetPropertyBlock(_hitPropertyBlock);
            _hitPropertyBlock.SetColor(target.colorProperty, target.originalColor);
            target.renderer.SetPropertyBlock(_hitPropertyBlock);
        }
    }

    /// <summary>
    /// 在保存过受击状态时恢复角色位置和缩放，并清除恢复标记。
    /// </summary>
    private void RestoreHitTransform()
    {
        if (!_hasHitEffectState)
            return;
        // 不回滚 X 坐标，避免覆盖受击期间 CharacterAI.Repel 已产生的位移。
        if (!_prop.isRepel)
            transform.position = new Vector3(
                transform.position.x,
                _hitEffectBasePosition.y,
                transform.position.z);
        transform.localScale = _hitEffectBaseScale;
        _hasHitEffectState = false;
    }

    /// <summary>
    /// 根据当前生命比例更新前景血条的横向缩放。
    /// </summary>
    private void ApplyBarVisual()
    {
        if (HpBarUp != null)
        {
            float scaleX = _prop.maxHp > 0 ? (float)_prop.currentHp / _prop.maxHp : 0f;
            HpBarUp.transform.localScale = new Vector3(scaleX, 1f, 1f);
        }
    }

    /// <summary>
    /// 显示并刷新血条，然后按角色配置设置自动隐藏时间。
    /// </summary>
    private void ShowBarTemporarily()
    {
        SetBarActive(true);
        ApplyBarVisual();
        hideTime = Time.time + _prop.barSustainTime;
    }

    /// <summary>
    /// 同时设置血条前景和背景对象的激活状态。
    /// </summary>
    /// <param name="active">是否显示整组血条对象。</param>
    private void SetBarActive(bool active)
    {
        if (HpBarUp != null) HpBarUp.SetActive(active);
        if (HpBarBottom != null) HpBarBottom.SetActive(active);
    }
    #endregion


}
