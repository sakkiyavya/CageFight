using UnityEngine;

public class CharacterHealth : MonoBehaviour, ILevelComponent, ICollide
{
    #region ILevelComponent实现
    public System.Type DataType => typeof(CharacterHealthData);

    public ComponentData ExtractData()
    {
        return new CharacterHealthData
        {
            barSustainTime = this.barSustainTime,
            defen = this.defen,
            magicDefen = this.magicDefen,
            maxHp = this.MaxHp
        };
    }

    public void ApplyData(ComponentData data)
    {
        if (data is CharacterHealthData hData)
        {
            this.barSustainTime = hData.barSustainTime;
            this.defen = hData.defen;
            this.magicDefen = hData.magicDefen;
            this.MaxHp = hData.maxHp;
        }
    }
    #endregion
    #region ICollide实现
    public Damage OnCollide(Damage damage)
    {
        print(damage.Source.name);
        return TakeDamage(damage);
    }
    #endregion
    public GameObject HpBarUp;
    public GameObject HpBarBottom;
    public float barSustainTime = 2f;

    [SerializeField]
    [Header("防御")]
    private int defen = 10; public int Defen => defen;

    [SerializeField]
    [Header("魔法防御")]
    private int magicDefen = 5; public int MagicDefen => magicDefen;

    [SerializeField]
    [Header("最大生命值")]
    private int MaxHp = 100; public int MaxHP => MaxHp;

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
        hp = Mathf.RoundToInt(MaxHp * Mathf.Clamp01(percent));
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
        return DamageComputor.DamageCompute(damage);
    }

    public void Heal(int amount) { throw new System.NotImplementedException(); }
    public void RestoreFullHp() { throw new System.NotImplementedException(); }
    public void ReduceToZero() { throw new System.NotImplementedException(); }
    public void Die() { throw new System.NotImplementedException(); }
    public void Revive() { throw new System.NotImplementedException(); }
    public bool IsDead() { throw new System.NotImplementedException(); }
    public float GetHpPercent() { throw new System.NotImplementedException(); }
    public void SetHp(int value) { throw new System.NotImplementedException(); }

    private void ApplyBarVisual()
    {
        if (HpBarUp != null)
        {
            float scaleX = MaxHp > 0 ? (float)hp / MaxHp : 0f;
            HpBarUp.transform.localScale = new Vector3(scaleX, 1f, 1f);
        }
    }

    private void ShowBarTemporarily()
    {
        SetBarActive(true);
        hideTime = Time.time + barSustainTime;
    }

    private void SetBarActive(bool active)
    {
        if (HpBarUp != null) HpBarUp.SetActive(active);
        if (HpBarBottom != null) HpBarBottom.SetActive(active);
    }


}
