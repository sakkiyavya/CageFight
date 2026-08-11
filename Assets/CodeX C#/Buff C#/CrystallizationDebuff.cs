using UnityEngine;

public class CrystallizationDebuff : BuffBase
{
    [SerializeField] private float duration = 5f;

    [Header("水晶图片")]
    [SerializeField] private Sprite crystalTexture;

    [SerializeField, Min(0.01f)]
    private float crystalScale = 1f;

    [SerializeField]
    private Vector2 crystalOffset = Vector2.zero;

    public override float buffSustainTime => duration;
    public override bool isDeBuff => true;

    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        CrystallizationState state =
            prop.GetComponent<CrystallizationState>();

        if (state == null)
        {
            state =
                prop.gameObject.AddComponent<
                    CrystallizationState>();
        }

        state.Apply(
            this,
            duration,
            crystalTexture,
            crystalScale,
            crystalOffset
        );

        return true;
    }

    public override bool CancelBuff(GameObjectProperty prop)
    {
        CrystallizationState state =
            prop != null
                ? prop.GetComponent<
                    CrystallizationState>()
                : null;

        if (state == null)
            return false;

        state.Remove();
        return true;
    }
}

class CrystallizationState : MonoBehaviour
{
    private GameObjectProperty prop;
    private SpriteRenderer body;
    private SpriteRenderer crystal;
    private CrystallizationDebuff source;

    private GameObjectType originalType;
    private Color originalColor;

    private float expireTime;
    private bool active;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();

        body =
            GetComponentInChildren<SpriteRenderer>(true);

        if (body != null)
            originalColor = body.color;
    }

    public void Apply(
        CrystallizationDebuff buff,
        float duration,
        Sprite texture,
        float scale,
        Vector2 offset)
    {
        source = buff;
        expireTime = Time.time + duration;

        // 重复施加只刷新时间
        if (active)
            return;

        active = true;
        originalType = prop.objectType;

        prop.objectType |= GameObjectType.Building;
        prop.moveSpeed *= 0.5f;
        prop.OnHitted += ReduceDamage;

        if (body == null)
            return;

        // 本体变为70%蓝紫色
        body.color =
            Color.Lerp(
                originalColor,
                new Color(
                    0.35f,
                    0.2f,
                    1f,
                    originalColor.a
                ),
                0.7f
            );

        if (texture != null)
        {
            CreateCrystal(
                texture,
                scale,
                offset
            );
        }
    }

    private void CreateCrystal(
        Sprite texture,
        float scale,
        Vector2 offset)
    {
        GameObject child =
            new GameObject("Crystal");

        child.transform.SetParent(
            body.transform,
            false
        );

        // 保持原比例，不再自动拉伸
        child.transform.localScale =
            Vector3.one * scale;

        // 调整上下左右位置
        child.transform.localPosition =
            new Vector3(
                offset.x,
                offset.y,
                0f
            );

        crystal =
            child.AddComponent<SpriteRenderer>();

        crystal.sprite = texture;
        crystal.sortingLayerID =
            body.sortingLayerID;

        crystal.sortingOrder =
            body.sortingOrder + 1;
    }

    private void Update()
    {
        if (Time.time >= expireTime)
        {
            Remove();
            return;
        }

        if (crystal == null)
            return;

        // 水晶图片呼吸
        Color color =
            new Color(
                0.6f,
                0.4f,
                1f,
                0.5f
            );

        color.a =
            0.4f +
            Mathf.Sin(Time.time * 3f) *
            0.15f;

        crystal.color = color;
    }

    private void ReduceDamage(Damage damage)
    {
        // 魔法伤害不享受减伤
        if (damage.type == DamageType.magic)
            return;

        prop.currentHp +=
            Mathf.RoundToInt(
                Mathf.Max(
                    0,
                    damage.initialDamage
                ) * 0.7f
            );
    }

    public void Remove()
    {
        if (!active)
            return;

        active = false;
        Restore();

        if (source != null)
        {
            while (prop.currentDebuff.Remove(source))
            {
            }
        }

        Destroy(this);
    }

    private void Restore()
    {
        prop.objectType = originalType;
        prop.moveSpeed /= 0.5f;
        prop.OnHitted -= ReduceDamage;

        if (body != null)
            body.color = originalColor;

        if (crystal != null)
            Destroy(crystal.gameObject);
    }

    private void OnDisable()
    {
        if (!active)
            return;

        active = false;
        Restore();
    }
}
