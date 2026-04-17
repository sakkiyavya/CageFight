using UnityEngine;

public class BuildingHealth : MonoBehaviour
{
    public GameObject HpBarUp;
    public GameObject HpBarBottom;
    public float barSustainTime = 2f;

    private const int MaxHp = 100;

    private int hp;
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

    public void TakeDamage(int damage)
    {
        // TODO: Implement damage logic.
        throw new System.NotImplementedException();
    }

    public void Heal(int amount)
    {
        // TODO: Implement heal logic.
        throw new System.NotImplementedException();
    }

    public void RestoreFullHp()
    {
        // TODO: Implement full HP restore logic.
        throw new System.NotImplementedException();
    }

    public void ReduceToZero()
    {
        // TODO: Implement HP depletion logic.
        throw new System.NotImplementedException();
    }

    public void Die()
    {
        // TODO: Implement death logic.
        throw new System.NotImplementedException();
    }

    public void Revive()
    {
        // TODO: Implement revive logic.
        throw new System.NotImplementedException();
    }

    public bool IsDead()
    {
        // TODO: Implement death state check.
        throw new System.NotImplementedException();
    }

    public float GetHpPercent()
    {
        // TODO: Implement HP percent query.
        throw new System.NotImplementedException();
    }

    public void SetHp(int value)
    {
        // TODO: Implement direct HP assignment logic.
        throw new System.NotImplementedException();
    }

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
        if (HpBarUp != null)
        {
            HpBarUp.SetActive(active);
        }

        if (HpBarBottom != null)
        {
            HpBarBottom.SetActive(active);
        }
    }
}
