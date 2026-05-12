// using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(GameObjectProperty))]
public class BuildingHealth : MonoBehaviour, ICollide
{
    // ICollide implementation retained
    public Damage OnCollide(Damage damage)
    {
        print(damage.Source.name);
        return TakeDamage(damage);
    }

    private GameObjectProperty _prop;
    private void Awake()
    {
        _prop = GetComponent<GameObjectProperty>();
    }
    public GameObject HpBarUp;
    public GameObject HpBarBottom;
    private int hp; public int HP => hp;
    

    private float hideTime = -1f;

    // 初始化血条显示。
    private void Start()
    {
        ApplyBarVisual();
        SetBarActive(false);
    }

    // 控制血条自动隐藏。
    private void Update()
    {
        if (hideTime >= 0f && Time.time >= hideTime)
        {
            SetBarActive(false);
            hideTime = -1f;
        }
    }

    // 按百分比设置血量。
    public void SetPercentHp(float percent)
    {
        hp = Mathf.RoundToInt(_prop.maxHp * Mathf.Clamp01(percent));
        ApplyBarVisual();
        ShowBarTemporarily();
    }

    // 刷新血条显示。
    public void SetHpbar()
    {
        ApplyBarVisual();
        ShowBarTemporarily();
    }

    // 处理受到的伤害。
    public Damage TakeDamage(Damage damage)
    {
        return DamageComputor.DamageCompute(damage);
    }

    // 恢复指定生命值。
    public void Heal(int amount)
    {
        // TODO: Implement heal logic.
        throw new System.NotImplementedException();
    }

    // 恢复满血状态。
    public void RestoreFullHp()
    {
        // TODO: Implement full HP restore logic.
        throw new System.NotImplementedException();
    }

    // 将血量降到零。
    public void ReduceToZero()
    {
        // TODO: Implement HP depletion logic.
        throw new System.NotImplementedException();
    }

    // 处理死亡逻辑。
    public void Die()
    {
        // TODO: Implement death logic.
        throw new System.NotImplementedException();
    }

    // 处理复活逻辑。
    public void Revive()
    {
        // TODO: Implement revive logic.
        throw new System.NotImplementedException();
    }

    // 判断是否已死亡。
    public bool IsDead()
    {
        // TODO: Implement death state check.
        throw new System.NotImplementedException();
    }

    // 获取当前血量百分比。
    public float GetHpPercent()
    {
        // TODO: Implement HP percent query.
        throw new System.NotImplementedException();
    }

    // 直接设置当前血量。
    public void SetHp(int value)
    {
        // TODO: Implement direct HP assignment logic.
        throw new System.NotImplementedException();
    }

    // 刷新血条缩放。
    private void ApplyBarVisual()
    {
        if (HpBarUp != null)
        {
            float scaleX = _prop.maxHp > 0 ? (float)hp / _prop.maxHp : 0f;
            HpBarUp.transform.localScale = new Vector3(scaleX, 1f, 1f);
        }
    }

    // 临时显示血条。
    private void ShowBarTemporarily()
    {
        SetBarActive(true);
        hideTime = Time.time + _prop.barSustainTime;
    }

    // 统一控制血条显隐。
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
