using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 目盲：减益状态。生效期间单位攻击时按未命中率掷骰，未命中则本次伤害与击退全部落空，
/// 目标头顶跳出 “miss” 字样。
/// 叠加公式（递减）：第 1 层 8%、第 2 层 7%、…第 8 层 1%，此后每层不再增加，
/// 8 层封顶：8+7+6+5+4+3+2+1 = 36%（极限）。层独立计时、逐层到期，总未命中率按剩余层求和。
/// 视觉：单位重心 y 轴上方 2 格处显示一个静态图标
/// （默认资源键 "State1 AP"，指向该图集第二个子精灵，可在 Inspector 调整）。
/// 无等级成长、无音效。
/// </summary>
public class BlindDebuff : BuffBase
{
    [Header("目盲数值")]
    [SerializeField, Min(0f)]
    private float firstMissPercent = 0.08f; // 第 1 层未命中率（8%）。
    [SerializeField, Min(0f)]
    private float missDecayPercent = 0.01f; // 每多一层递减 1%。
    [SerializeField, Range(0f, 1f)]
    private float maxMissPercent = 0.36f;   // 总未命中率上限（36%，8 层封顶）。
    [SerializeField, Min(0.1f)]
    private float duration = 5f;            // 每层持续秒。

    [Header("目盲图标视觉")]
    [SerializeField, ResourceKey(typeof(Sprite))]
    private string blindSpriteKey = "State1 AP"; // 图标图像资源键（默认图集 State1 AP 的第二个子精灵）。
    [SerializeField, Min(0f)]
    private float yOffset = 2f;             // 图标相对单位重心 y 轴上的偏移（默认 2 格）。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => true;

    /// <summary>第 1 层未命中率，供层管理器读取。</summary>
    public float FirstMissPercent => firstMissPercent;
    /// <summary>每层递减比例，供层管理器读取。</summary>
    public float MissDecayPercent => missDecayPercent;
    /// <summary>总未命中率上限，供层管理器读取。</summary>
    public float MaxMissPercent => maxMissPercent;
    /// <summary>每层持续时间，供层管理器读取。</summary>
    public float Duration => duration;
    /// <summary>图标图像资源键，供层管理器读取。</summary>
    public string BlindSpriteKey => blindSpriteKey;
    /// <summary>图标 y 偏移，供层管理器读取。</summary>
    public float YOffset => yOffset;

    /// <summary>供运行时创建/配置实例时设置每层持续时间。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层目盲；不设层数上限，每层独立计时，未命中率按层序递减。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        BlindState state = prop.GetComponent<BlindState>();
        if (state == null)
            state = prop.gameObject.AddComponent<BlindState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层目盲。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        BlindState state = prop.GetComponent<BlindState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion
}

/// <summary>
/// 目标身上的目盲层管理器：无限叠加、每层独立到期，
/// 第 n 层未命中率 = max(0, 8% - (n-1) × 1%)，总未命中率按剩余层求和（上限 36%）
/// 写入目标属性的 missChance（攻击未命中率）字段，并驱动静态图标显示。
/// </summary>
internal class BlindState : MonoBehaviour
{
    /// <summary>单层目盲快照，施加瞬间锁定未命中率与到期时间。</summary>
    private class Layer
    {
        public BlindDebuff source;      // 施加该层的实例，用于取消时匹配。
        public float missPercent;       // 本层快照的未命中率（按层序递减）。
        public float expireTime;        // 本层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private const string IconVisualPrefabKey = "UnitVisualFollower"; // 图标视觉预制体资源键（池化生成）。
    private UnitVisualFollower blindFollower;   // 图标视觉（池化跟随对象）。
    private float yOffset = 2f;
    private float firstMissPercent = 0.08f;
    private float missDecayPercent = 0.01f;
    private float maxMissPercent = 0.36f;
    private bool warnedMissingSprite;   // 是否已输出过图像缺失警告（一次性）。

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    /// <summary>
    /// 无限叠加一层目盲：按当前层序计算本层未命中率，首个层加入时创建图标。
    /// </summary>
    public bool AddLayer(BlindDebuff source)
    {
        if (source == null || prop == null)
            return false;

        if (layers.Count == 0)
        {
            yOffset = source.YOffset;
            firstMissPercent = source.FirstMissPercent;
            missDecayPercent = source.MissDecayPercent;
            maxMissPercent = source.MaxMissPercent;
            CreateIcon(source.BlindSpriteKey);
        }

        // 第 n 层（n = layers.Count + 1）未命中率 = 首层 - (n-1) × 递减；低于 0 记为 0（8 层后不再增加）。
        float missPercent = Mathf.Max(0f, firstMissPercent - layers.Count * missDecayPercent);

        layers.Add(new Layer
        {
            source = source,
            missPercent = missPercent,
            expireTime = Time.time + source.Duration,
        });

        ApplyEffect();
        return true;
    }

    /// <summary>
    /// 移除由指定实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(BlindDebuff source)
    {
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].source != source)
                continue;

            RemoveAt(i);
            return true;
        }

        return false;
    }

    private void Update()
    {
        // 倒序清理到期层；全部到期后本管理器会销毁自身。
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            if (Time.time >= layers[i].expireTime)
                RemoveAt(i);
        }
    }

    /// <summary>
    /// 按当前全部层的未命中率求和（上限 36%），与目标基础未命中率取较大者后
    /// 写入目标的 missChance 字段（不覆盖基础值，如 Cat dad 基础 50% 时保底 50%）。
    /// </summary>
    private void ApplyEffect()
    {
        if (prop == null)
            return;

        float total = 0f;
        for (int i = 0; i < layers.Count; i++)
            total += layers[i].missPercent;

        float bonus = Mathf.Clamp(total, 0f, maxMissPercent);
        prop.missChance = Mathf.Max(prop.baseMissChance, bonus);
    }

    /// <summary>
    /// 创建图标（子级 SpriteRenderer），位于单位重心 y 轴上方 yOffset 处，静态显示。
    /// </summary>
    private void CreateIcon(string spriteKey)
    {
        DestroyIcon();

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        Sprite sprite = null;
        if (!string.IsNullOrEmpty(spriteKey) && ResourceManager.Instance != null)
            sprite = ResourceManager.Instance.GetSprite(spriteKey);
        if (sprite == null)
        {
            if (!warnedMissingSprite)
            {
                warnedMissingSprite = true;
                Debug.LogWarning($"[BlindDebuff] 精灵资源 {spriteKey} 未加载，目盲图标无法显示。", this);
            }
            return;
        }

        GameObject prefab = ResourceManager.Instance.GetGameObject(IconVisualPrefabKey);
        if (prefab == null)
            return;

        GameObject go = GameObjectPool.Instance.Get(prefab);
        if (go == null)
            return;

        UnitVisualFollower follower = go.GetComponent<UnitVisualFollower>();
        if (follower == null)
            follower = go.AddComponent<UnitVisualFollower>();

        SpriteRenderer iconRenderer = go.GetComponent<SpriteRenderer>();
        if (iconRenderer != null)
        {
            iconRenderer.sprite = sprite;
            iconRenderer.color = Color.white;
            if (renderers != null && renderers.Length > 0)
            {
                iconRenderer.sortingLayerID = renderers[0].sortingLayerID;
                iconRenderer.sortingOrder = renderers[0].sortingOrder + 1;
            }
        }

        follower.Init(gameObject, new Vector3(0f, yOffset, 0f), 0f, 1f, 1f);
        blindFollower = follower;
    }

    private void RemoveAt(int index)
    {
        layers.RemoveAt(index);
        ApplyEffect();

        if (layers.Count == 0)
        {
            prop.missChance = prop.baseMissChance;
            DestroyIcon();
            Destroy(this);
        }
    }

    private void OnDisable()
    {
        if (prop != null && layers.Count > 0)
            prop.missChance = prop.baseMissChance;
        DestroyIcon();
        layers.Clear();
    }

    private void DestroyIcon()
    {
        if (blindFollower != null)
        {
            blindFollower.Finish();
            blindFollower = null;
        }
    }
}
