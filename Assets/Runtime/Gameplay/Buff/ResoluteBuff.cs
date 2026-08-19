using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 坚毅：格挡类增益 Buff。每层代表一次格挡机会（可无限叠加），
/// 目标受到伤害时消耗一层：本次伤害变为 1 点且完全免疫击退。
/// 层默认不被时间消耗（duration 为 0 时永久存在，直到被伤害消耗殆尽）；
/// 配置 duration &gt; 0 时层也会随时间逐层到期。
/// 无等级成长、无音效。
/// 视觉：目标图像重心 y 轴上方 1 格处出现一个棱形透明白色呼吸护盾
/// （需配置 shieldSprite，如菱形贴图），每叠加一层护盾图像按配置比例变大且累加。
/// 仅新增本脚本即可生效，不改动任何既有脚本。
/// </summary>
public class ResoluteBuff : BuffBase
{
    [Header("坚毅数值")]
    [SerializeField, Min(0f)]
    private float duration = 0f;        // 每层持续秒；0 = 永久（只被伤害消耗）。

    [Header("棱形护盾视觉")]
    [SerializeField, Tooltip("棱形护盾贴图（直接拖入，优先使用；为空时回退 shieldSpriteKey 按资源键解析）")]
    private Sprite shieldSprite;
    [SerializeField, ResourceKey(typeof(Sprite)), Tooltip("棱形护盾贴图资源键（State1 AP 第 33 个子精灵 = State1 AP_32），直接贴图为空时使用")]
    private string shieldSpriteKey = "State1 AP_32";
    [SerializeField, ResourceKey(typeof(GameObject)), Tooltip("护盾视觉预制体资源键（UnitVisualFollower，池化生成）")]
    private string shieldVisualPrefabKey = "UnitVisualFollower";
    [SerializeField, Min(0f)]
    private float shieldHeight = 1f;    // 护盾相对图像重心 y 轴上的偏移（默认 1 格）。
    [SerializeField, Min(0.01f)]
    private float baseScale = 1f;       // 单层护盾的基础缩放。
    [SerializeField, Min(0f)]
    private float growPerLayer = 0.2f;  // 每多叠加一层，护盾缩放的累加增幅（如 2 层 = 1 + 0.2）。
    [SerializeField, Min(0f)]
    private float breathSpeed = 2f;     // 护盾透明度呼吸频率（每秒周期数）。
    [SerializeField, Range(0f, 1f)]
    private float breathMinAlpha = 0.35f; // 呼吸透明度下限。
    [SerializeField, Range(0f, 1f)]
    private float breathMaxAlpha = 0.65f; // 呼吸透明度上限。

    public override float buffSustainTime => duration > 0f ? duration : 9999f;
    public override bool isDeBuff => false;

    /// <summary>每层持续秒（0 = 永久），供层管理器读取。</summary>
    public float Duration => duration;
    /// <summary>护盾视觉预制体资源键，供层管理器读取。</summary>
    public string ShieldVisualPrefabKey => shieldVisualPrefabKey;

    private Sprite _resolvedSprite;     // 按资源键解析的运行时缓存（不写回序列化字段，避免污染预制体资产）。
    private bool shieldSpriteWarned;    // 是否已输出过“贴图尚未加载”的一次性警告。

    /// <summary>供运行时创建/配置实例时设置每层持续秒（0 = 永久，只被伤害消耗）。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0f, seconds);
    }

    /// <summary>
    /// 护盾贴图：优先使用 Inspector 直接拖入的贴图；为空时按资源键经 ResourceManager
    /// 解析（未就绪时返回 null，由层管理器在每次施加坚毅时重试补齐，能自愈时序问题）。
    /// 解析成功或持续缺失时输出一次性日志，便于在 Console 定位真实资源键。
    /// </summary>
    public Sprite ShieldSprite
    {
        get
        {
            if (shieldSprite != null)
                return shieldSprite;

            if (_resolvedSprite == null && ResourceManager.Instance != null &&
                !string.IsNullOrEmpty(shieldSpriteKey))
            {
                _resolvedSprite = ResourceManager.Instance.GetSprite(shieldSpriteKey);

                if (_resolvedSprite == null && !shieldSpriteWarned)
                {
                    shieldSpriteWarned = true;
                    Debug.LogWarning($"[ResoluteBuff] 坚毅护盾贴图尚未加载：key={shieldSpriteKey}，将在后续施放时自动重试补齐。", this);
                }
            }

            return _resolvedSprite;
        }
    }
    /// <summary>护盾 y 偏移，供层管理器读取。</summary>
    public float ShieldHeight => shieldHeight;    /// <summary>单层护盾基础缩放，供层管理器读取。</summary>
    public float BaseScale => baseScale;
    /// <summary>每层护盾缩放增幅，供层管理器读取。</summary>
    public float GrowPerLayer => growPerLayer;
    /// <summary>呼吸频率，供层管理器读取。</summary>
    public float BreathSpeed => breathSpeed;
    /// <summary>呼吸透明度下限，供层管理器读取。</summary>
    public float BreathMinAlpha => breathMinAlpha;
    /// <summary>呼吸透明度上限，供层管理器读取。</summary>
    public float BreathMaxAlpha => breathMaxAlpha;

    /// <summary>供运行时创建/配置实例时设置护盾贴图。</summary>
    public void SetShieldSprite(Sprite sprite)
    {
        shieldSprite = sprite;
    }

    /// <summary>供运行时创建/配置实例时设置护盾贴图资源键。</summary>
    public void SetShieldSpriteKey(string key)
    {
        shieldSpriteKey = key;
        _resolvedSprite = null;
        shieldSpriteWarned = false;
    }

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层坚毅；不设层数上限。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        ResoluteState state = prop.GetComponent<ResoluteState>();
        if (state == null)
            state = prop.gameObject.AddComponent<ResoluteState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层坚毅。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        ResoluteState state = prop.GetComponent<ResoluteState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion
}

/// <summary>
/// 目标身上的坚毅层管理器：层数即剩余格挡次数（写入 prop.blockHits，
/// 由 CharacterHealth 受伤时消耗），驱动棱形透明白色呼吸护盾视觉，
/// 护盾缩放随层数累加变大。
/// </summary>
internal class ResoluteState : MonoBehaviour
{
    /// <summary>单层坚毅快照。</summary>
    private class Layer
    {
        public ResoluteBuff source;     // 施加该层的实例，用于取消时匹配。
        public float expireTime;        // 到期时间（duration 为 0 时不会到期）。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private UnitVisualFollower shieldFollower;      // 棱形护盾视觉（池化跟随对象）。
    private float shieldHeight = 1f;
    private float baseScale = 1f;
    private float growPerLayer = 0.2f;
    private float breathSpeed = 2f;
    private float breathMinAlpha = 0.35f;
    private float breathMaxAlpha = 0.65f;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    /// <summary>
    /// 无限叠加一层坚毅：格挡次数 +1。每次施加都核对/创建护盾并刷新贴图——
    /// 资源晚加载或贴图被替换时，在下一次施加即自愈，不会永久停留在旧图/空图。
    /// </summary>
    public bool AddLayer(ResoluteBuff source)
    {
        if (source == null || prop == null)
            return false;

        bool isFirstLayer = layers.Count == 0;
        if (isFirstLayer)
        {
            shieldHeight = source.ShieldHeight;
            baseScale = source.BaseScale;
            growPerLayer = source.GrowPerLayer;
            breathSpeed = source.BreathSpeed;
            breathMinAlpha = source.BreathMinAlpha;
            breathMaxAlpha = source.BreathMaxAlpha;
        }

        layers.Add(new Layer
        {
            source = source,
            expireTime = Time.time + source.Duration,
        });

        prop.blockHits++;
        EnsureShield(source);
        UpdateShieldScale();
        return true;
    }

    /// <summary>
    /// 移除由指定实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(ResoluteBuff source)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].source != source)
                continue;

            RemoveAt(i, true);
            return true;
        }

        return false;
    }

    private void Update()
    {
        // 配置了时长时逐层到期清理；否则层只被伤害消耗。
        if (layers.Count > 0)
        {
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                if (layers[i].source.Duration > 0f && Time.time >= layers[i].expireTime)
                    RemoveAt(i, true);
            }
        }

        // 与 CharacterHealth 的消耗同步：受伤后 blockHits 已减少，移除最旧的一层对应
        // （blockHits 由伤害流程扣减，这里只移除层，不再重复扣减）。
        while (layers.Count > prop.blockHits)
            RemoveAt(0, false);
    }

    /// <summary>
    /// 核对护盾视觉：不存在则按资源键经对象池生成（UnitVisualFollower），
    /// 贴图变化（首次解析成功/换图）时更新；跟随、呼吸与回收由该组件统一管理。
    /// </summary>
    private void EnsureShield(ResoluteBuff source)
    {
        Sprite sprite = source.ShieldSprite;

        if (shieldFollower != null)
        {
            if (!shieldFollower.IsActive)
            {
                shieldFollower = null;
            }
            else
            {
                SyncShieldSprite(sprite);
                return;
            }
        }

        if (ResourceManager.Instance == null)
            return;

        // 延迟补齐：资源未就绪时本次跳过，下次施加坚毅时重试。
        GameObject prefab = ResourceManager.Instance.GetGameObject(source.ShieldVisualPrefabKey);
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
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers != null && renderers.Length > 0)
            {
                renderer.sortingLayerID = renderers[0].sortingLayerID;
                renderer.sortingOrder = renderers[0].sortingOrder + 1;
            }
            renderer.color = new Color(1f, 1f, 1f, breathMaxAlpha);
        }

        follower.Init(gameObject, new Vector3(0f, shieldHeight, 0f),
            breathSpeed, breathMinAlpha, breathMaxAlpha);
        shieldFollower = follower;
        SyncShieldSprite(sprite);
    }

    /// <summary>把当前解析到的贴图同步到护盾视觉渲染器。</summary>
    private void SyncShieldSprite(Sprite sprite)
    {
        if (shieldFollower == null || sprite == null)
            return;

        SpriteRenderer renderer = shieldFollower.GetComponent<SpriteRenderer>();
        if (renderer != null && renderer.sprite != sprite)
            renderer.sprite = sprite;
    }

    /// <summary>
    /// 护盾缩放随层数累加：scale = 单层基础缩放 × (1 + (层数 - 1) × 每层增幅)。
    /// </summary>
    private void UpdateShieldScale()
    {
        if (shieldFollower == null || !shieldFollower.IsActive)
            return;

        int count = layers.Count;
        float scale = baseScale * (1f + Mathf.Max(0, count - 1) * growPerLayer);
        shieldFollower.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void RemoveAt(int index, bool decrementBlock)
    {
        layers.RemoveAt(index);

        if (prop != null && decrementBlock)
            prop.blockHits = Mathf.Max(0, prop.blockHits - 1);

        if (layers.Count == 0)
        {
            RemoveShield();
            Destroy(this);
        }
        else
        {
            UpdateShieldScale();
        }
    }

    private void OnDisable()
    {
        RemoveShield();
        layers.Clear();
    }

    private void RemoveShield()
    {
        if (shieldFollower != null)
        {
            shieldFollower.Finish();
            shieldFollower = null;
        }
    }
}
