using System.Linq;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class CharacterHealth : MonoBehaviour, ICollide
{
    private int hp; public int HP => hp;
    // ILevelComponent removed; data handled by GameObjectProperty
    public GameObject HpBarUp;
    public GameObject HpBarBottom;
    private GameObjectProperty _prop;
    private float hideTime = -1f;
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
        hp = _prop.maxHp;
    }
    #region ICollide实现
    public bool IsFriendly(Damage damage)
    {
        // 简单示例：如果双方属于同一阵营，则视为友好
        return damage.side == _prop.side;
    }
    public Damage OnCollide(Damage damage)
    {   
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
        _prop.repelDistance = damage.repel / _prop.antiRepel * damage.collideDir;
        _prop.isRepel = true;
        if(hp <= 0) Die();
        return DamageComputor.DamageCompute(damage);
    }

    public void Heal(int value) 
    { 
        hp += value;
        hp = Mathf.Min(hp, _prop.maxHp);
        ShowBarTemporarily();

        DamageTextPool.Instance.ShowHeal(value, transform.position + Vector3.up * 1.5f);
    }
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
