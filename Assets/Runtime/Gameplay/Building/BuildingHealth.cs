using Unity.Mathematics;
using UnityEngine;

public class BuildingHealth : MonoBehaviour, ICollide
{
    public GameObject HpBarUp;
    public GameObject HpBarBottom;
    public float barSustainTime = 2f;
    [SerializeField]
    [Header("防御")]
    private int defen = 10;public int Defen => defen;
    [SerializeField]
    [Header("魔法防御")]
    private int magicDefen = 5;public int MagicDefen => magicDefen;
    [SerializeField]
    [Header("最大生命值")]
    private int MaxHp = 100;public int MaxHP => MaxHp;
    private int hp;public int HP => hp;
    

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
        hp = Mathf.RoundToInt(MaxHp * Mathf.Clamp01(percent));
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
        int finalDamage = 0;


        Damage result = new Damage();
        result.initialDamage = damage.initialDamage;
        result.finalDamage = finalDamage;
        result.type = damage.type;

        return result;
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
            float scaleX = MaxHp > 0 ? (float)hp / MaxHp : 0f;
            HpBarUp.transform.localScale = new Vector3(scaleX, 1f, 1f);
        }
    }

    // 临时显示血条。
    private void ShowBarTemporarily()
    {
        SetBarActive(true);
        hideTime = Time.time + barSustainTime;
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

    // 处理外部碰撞。
    public void OnCollide(Collider2D other)
    {
        
    }
}
