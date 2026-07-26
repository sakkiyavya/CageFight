using System.Collections;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterHealth : MonoBehaviour, ICollide
{
    public int HP => _prop != null ? _prop.currentHp : 0;
    // ILevelComponent removed; data handled by GameObjectProperty
    public GameObject HpBarUp;
    public GameObject HpBarBottom;
    private GameObjectProperty _prop;
    private float hideTime = -1f;
    private Coroutine _deathCoroutine;

    private void OnEnable()
    {
        _deathCoroutine = null;
        hideTime = -1f;
        ApplyBarVisual();
        SetBarActive(false);
    }

    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
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
        ShowBarTemporarily();
        _prop.repelDistance = d.repel / _prop.antiRepel * d.collideDir;
        _prop.isRepel = true;
        if(_prop.currentHp <= 0) Die();
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
        if (_prop.isDead && _deathCoroutine != null)
            return;

        _prop.currentHp = 0;
        _prop.isDead = true;
        _prop.isAttack = false;
        _prop.target = null;
        if (_deathCoroutine == null)
            _deathCoroutine = StartCoroutine(DeathBounceAndRelease());
    }

    public void Revive()
    {
        if (_deathCoroutine != null)
        {
            StopCoroutine(_deathCoroutine);
            _deathCoroutine = null;
        }
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

    private IEnumerator DeathBounceAndRelease()
    {
        Vector3 originalPosition = transform.position;
        Vector3 originalScale = transform.localScale;
        const int bounceCount = 3;
        const float bounceDuration = 0.24f;
        const float firstHeight = 0.45f;

        for (int bounce = 0; bounce < bounceCount; bounce++)
        {
            float elapsed = 0f;
            float height = firstHeight * (1f - bounce * 0.25f);
            while (elapsed < bounceDuration)
            {
                float t = elapsed / bounceDuration;
                float pulse = Mathf.Sin(Mathf.PI * t);
                float jelly = Mathf.Sin(Mathf.PI * 2f * t);
                transform.position = originalPosition + Vector3.up * (pulse * height);
                transform.localScale = new Vector3(
                    originalScale.x * (1f - jelly * 0.14f),
                    originalScale.y * (1f + jelly * 0.22f),
                    originalScale.z);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        transform.position = originalPosition;
        transform.localScale = originalScale;
        _deathCoroutine = null;
        GameObjectPool.Instance.Release(gameObject);
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
