using System.Collections;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterHealth : MonoBehaviour, ICollide
{
    private struct HitColorTarget
    {
        public Renderer renderer;
        public int colorProperty;
        public Color originalColor;
    }

    public int HP => _prop != null ? _prop.currentHp : 0;
    // IStageComponent removed; data handled by GameObjectProperty
    public GameObject HpBarUp;
    public GameObject HpBarBottom;
    private float hitFlashDuration = 0.3f;
    private float jellyDuration = 0.6f;
    private float jellyFrequency = 2f;
    private float jellyAmplitude = 0.1f;
    
    private float deathAngularSpeed = 1440f;
    private float deathParabolaAcceleration = -50f;
    private Vector2 deathInitialVelocity = new Vector2(4f, 20f);
    private float deathEffectDuration = 1f;
    private GameObjectProperty _prop;
    private float hideTime = -1f;
    private Coroutine _hitEffectCoroutine;
    private Coroutine _deathEffectCoroutine;
    private Vector3 _hitEffectBasePosition;
    private Vector3 _hitEffectBaseScale;
    private bool _hasHitEffectState;
    private readonly System.Collections.Generic.List<HitColorTarget> _hitColorTargets =
        new System.Collections.Generic.List<HitColorTarget>();
    private MaterialPropertyBlock _hitPropertyBlock;

    private void OnEnable()
    {
        _hitEffectCoroutine = null;
        _deathEffectCoroutine = null;
        _hasHitEffectState = false;
        hideTime = -1f;
        ApplyBarVisual();
        SetBarActive(false);
    }

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        _hitPropertyBlock = new MaterialPropertyBlock();
        CacheHitColorTargets();
    }

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

    #region ICollide实现
    public bool IsFriendly(Damage damage)
    {
        // 简单示例：如果双方属于同一阵营，则视为友好
        return damage.side == _prop.side;
    }
    public Damage OnCollide(Damage damage)
    {   
        if (_prop.isDead)
            return damage;

        if(damage.buffs != null && damage.buffs.Count() > 0)
            foreach(var buff in damage.buffs)
            {
                if(!buff.ApplyBuff(_prop))
                    continue;
                buff.buffApplyTime = Time.time;
                if(buff.isDeBuff)
                    _prop.currentDebuff.Add(buff);
                else 
                    _prop.currentBuff.Add(buff);
            }

        
        _prop.OnHitted?.Invoke();
        return TakeDamage(damage);
    }
    #endregion

    private void Start()
    {
        ApplyBarVisual();
        SetBarActive(false);
    }

    private void Update()
    {
        if (hideTime >= 0f && Time.time >= hideTime)
        {
            SetBarActive(false);
            hideTime = -1f;
        }
    }

    public void SetPercentHp(float percent)
    {
        _prop.currentHp = Mathf.RoundToInt(_prop.maxHp * Mathf.Clamp01(percent));
        _prop.isDead = _prop.currentHp <= 0;
        ApplyBarVisual();
        ShowBarTemporarily();
    }

    public void SetHpbar()
    {
        ApplyBarVisual();
        ShowBarTemporarily();
    }

    public Damage TakeDamage(Damage damage)
    {
        Damage d = DamageComputor.DamageCompute(damage);
        if (_prop.isDead)
            return d;

        _prop.currentHp = Mathf.Max(_prop.currentHp - d.finalDamage, 0);
        RestartHitEffect();
        ShowBarTemporarily();
        _prop.repelDistance = d.repel / _prop.antiRepel * d.collideDir;
        _prop.isRepel = true;
        if(_prop.currentHp <= 0) Die(d);
        DamageTextPool.Instance.ShowDamage(d, transform.position + Vector3.up);
        return d;
    }

    public void Heal(int value) 
    { 
        if (_prop.isDead)
            return;

        _prop.currentHp = Mathf.Min(_prop.currentHp + value, _prop.maxHp);
        ShowBarTemporarily();

        DamageTextPool.Instance.ShowHeal(value, transform.position + Vector3.up * 1.5f);
    }
    public void RestoreFullHp()
    {
        _prop.currentHp = _prop.maxHp;
        _prop.isDead = false;
        ApplyBarVisual();
    }

    public void ReduceToZero()
    {
        _prop.currentHp = 0;
        Die();
    }

    public void Die()
    {
        Die(Damage.DefaultDamage);
    }

    private void Die(Damage damage)
    {
        if (_prop.isDead && _deathEffectCoroutine != null)
            return;

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

    public void Revive()
    {
        _prop.currentHp = _prop.maxHp;
        _prop.isDead = false;
        ApplyBarVisual();
    }

    public bool IsDead() => _prop == null || _prop.isDead;
    public float GetHpPercent() => _prop.maxHp > 0 ? (float)_prop.currentHp / _prop.maxHp : 0f;
    public void SetHp(int value)
    {
        _prop.currentHp = Mathf.Clamp(value, 0, _prop.maxHp);
        _prop.isDead = _prop.currentHp <= 0;
        ApplyBarVisual();
    }

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
                transform.position = _hitEffectBasePosition + Vector3.up * (pulse * jellyAmplitude);
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
        RespawnAfterDeath(startPosition, startRotation, startScale);
    }

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

    private void RespawnAfterDeath(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        GameObjectPool pool = GameObjectPool.Instance;
        if (pool == null)
            return;

        GameObject prefab = pool.GetPrefab(gameObject);
        if (prefab == null)
            return;

        GameObject respawned = pool.Get(prefab);
        if (respawned != null)
        {
            respawned.transform.position = position;
            respawned.transform.rotation = rotation;
            respawned.transform.localScale = scale;
        }

        pool.Release(gameObject);
    }

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

    private void RestoreHitTransform()
    {
        if (!_hasHitEffectState)
            return;
        transform.position = _hitEffectBasePosition;
        transform.localScale = _hitEffectBaseScale;
        _hasHitEffectState = false;
    }

    private void ApplyBarVisual()
    {
        if (HpBarUp != null)
        {
            float scaleX = _prop.maxHp > 0 ? (float)_prop.currentHp / _prop.maxHp : 0f;
            HpBarUp.transform.localScale = new Vector3(scaleX, 1f, 1f);
        }
    }

    private void ShowBarTemporarily()
    {
        SetBarActive(true);
        ApplyBarVisual();
        hideTime = Time.time + _prop.barSustainTime;
    }

    private void SetBarActive(bool active)
    {
        if (HpBarUp != null) HpBarUp.SetActive(active);
        if (HpBarBottom != null) HpBarBottom.SetActive(active);
    }


}
