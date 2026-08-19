using UnityEngine;

public class CrystallizationDebuff : BuffBase
{
    [SerializeField] private float duration = 5f;

    [Header("水晶图片")]
    [SerializeField, ResourceKey(typeof(Sprite))]
    private string crystalTextureKey = "Buff1 AP_0";

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
            ResolveCrystalSprite(),
            crystalScale,
            crystalOffset
        );

        return true;
    }

    /// <summary>按资源键解析水晶贴图（延迟补齐，成功一次后不再查找）。</summary>
    private Sprite ResolveCrystalSprite()
    {
        if (_crystalSprite == null && ResourceManager.Instance != null &&
            !string.IsNullOrEmpty(crystalTextureKey))
            _crystalSprite = ResourceManager.Instance.GetSprite(crystalTextureKey);

        return _crystalSprite;
    }

    private Sprite _crystalSprite;  // 经 ResourceManager 解析的水晶贴图缓存。

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

class CrystallizationState : MonoBehaviour, IIncomingDamageModifier
{
    private GameObjectProperty prop;
    private SpriteRenderer body;
    private const string CrystalVisualPrefabKey = "UnitVisualFollower"; // 水晶视觉预制体资源键（池化生成）。
    private UnitVisualFollower crystalFollower;
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

        if (body == null)
            return;

        

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
        GameObject prefab = ResourceManager.Instance.GetGameObject(CrystalVisualPrefabKey);
        if (prefab == null)
            return;

        GameObject go = GameObjectPool.Instance.Get(prefab);
        if (go == null)
            return;

        UnitVisualFollower follower = go.GetComponent<UnitVisualFollower>();
        if (follower == null)
            follower = go.AddComponent<UnitVisualFollower>();

        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.sprite = texture;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + 1;
            renderer.color = new Color(0.6f, 0.4f, 1f, 1f);
        }

        go.transform.localScale = Vector3.one * scale;
        follower.Init(gameObject, new Vector3(offset.x, offset.y, 0f),
            0.48f, 0.25f, 0.55f);
        crystalFollower = follower;
    }

    private void Update()
    {
        if (Time.time >= expireTime)
        {
            Remove();
        }
    }

    /// <summary>
    /// 统一入伤修正（IIncomingDamageModifier）：晶化期间非魔法伤害减免 70%，
    /// 在正式扣血前按已结算伤害修正，不预先回血抵消。
    /// </summary>
    public int ModifyIncomingDamage(Damage damage)
    {
        if (!active || damage.type == DamageType.magic || damage.finalDamage <= 0)
            return damage.finalDamage;

        return Mathf.Max(0, Mathf.RoundToInt(damage.finalDamage * 0.3f));
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

        if (body != null)
            body.color = originalColor;

        if (crystalFollower != null)
        {
            crystalFollower.Finish();
            crystalFollower = null;
        }
    }

    private void OnDisable()
    {
        if (!active)
            return;

        active = false;
        Restore();
    }
}
