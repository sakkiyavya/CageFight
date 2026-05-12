using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterHealth : MonoBehaviour, ICollide
{
    // ILevelComponent removed; data handled by GameObjectProperty
    private GameObjectProperty _prop;
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        hp = _prop.maxHp;
    }
    #region ICollide实现
    public Damage OnCollide(Damage damage)
    {
        if(damage.source)
            print(damage.source.name);
        
        return TakeDamage(damage);
    }
    #endregion
    public GameObject HpBarUp;
    public GameObject HpBarBottom;
    private int hp; public int HP => hp;
    private float hideTime = -1f;

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
        hp = Mathf.RoundToInt(_prop.maxHp * Mathf.Clamp01(percent));
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
        hp -= damage.initialDamage;
        hp = Mathf.Max(hp, 0);
        ShowBarTemporarily();
        if(hp <= 0) Die();
        return DamageComputor.DamageCompute(damage);
    }

    public void Heal(int amount) { throw new System.NotImplementedException(); }
    public void RestoreFullHp() { throw new System.NotImplementedException(); }
    public void ReduceToZero() { throw new System.NotImplementedException(); }
    public void Die() { print(name + " Die!"); }
    public void Revive() { throw new System.NotImplementedException(); }
    public bool IsDead() { throw new System.NotImplementedException(); }
    public float GetHpPercent() { throw new System.NotImplementedException(); }
    public void SetHp(int value) { throw new System.NotImplementedException(); }

    private void ApplyBarVisual()
    {
        if (HpBarUp != null)
        {
            float scaleX = _prop.maxHp > 0 ? (float)hp / _prop.maxHp : 0f;
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
