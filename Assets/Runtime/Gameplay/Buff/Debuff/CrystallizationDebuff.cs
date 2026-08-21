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

class CrystallizationState : MonoBehaviour
{
    private GameObjectProperty prop;
    private CharacterHealth health;
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
        health = GetComponent<CharacterHealth>();

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

        // 晶化期间登记入伤修正器（减伤 70%），解除/禁用时对称注销。
        if (health != null)
            health.RegisterDamageModifier(ReduceDamage);

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
        {
            // 预制体已预配置 UnitVisualFollower（正式池化表现模块）；缺失时归还并安全失败。
            GameObjectPool.Instance.Release(go);
            return;
        }

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
    /// 入伤修正器（经 CharacterHealth 统一扩展点登记）：晶化期间非魔法伤害减免 70%，
    /// 在正式扣血前按已结算伤害修正，不预先回血抵消。
    /// </summary>
    private int ReduceDamage(Damage damage)
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
        if (health != null)
            health.UnregisterDamageModifier(ReduceDamage);
        Restore();

        if (source != null && health != null)
            health.RemoveBuff(source);

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
        if (health != null)
            health.UnregisterDamageModifier(ReduceDamage);

        if (!active)
            return;

        active = false;
        Restore();
    }
}
