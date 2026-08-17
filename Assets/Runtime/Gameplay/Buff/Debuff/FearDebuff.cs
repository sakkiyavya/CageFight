using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 畏惧：减益状态。生效期间单位停止索敌与攻击，改为随机乱跑移动，直到状态结束。
/// 可无限叠加：每层独立计时、逐层到期；效果单份（层数不影响表现）。
/// 视觉：单位重心 y 轴上方 2 格处显示一个呼吸灯效果图像
/// （默认资源键 "Buff1 AP"，指向该图集第二个子图，可在 Inspector 调整）。
/// 无等级成长、无音效。
/// 实现说明：CharacterAI.AIBehaviour 每帧 AI 决策前询问 FearState.IsActive，
/// 畏惧时跳过全部索敌/寻路/攻击行为，改由 FearState.DoFearMove 驱动随机乱跑。
/// </summary>
public class FearDebuff : BuffBase
{
    [Header("畏惧数值")]
    [SerializeField, Min(0.1f)]
    private float duration = 5f;            // 每层持续秒。
    [SerializeField, Min(0f)]
    private float moveSpeedBonus = 0.5f;    // 畏惧期间移速加成（50%）。
    [SerializeField, Min(0.05f)]
    private float minChangeInterval = 0.5f; // 随机换向最小间隔秒。
    [SerializeField, Min(0.05f)]
    private float maxChangeInterval = 1.5f; // 随机换向最大间隔秒。

    [Header("畏惧呼吸灯视觉")]
    [SerializeField, ResourceKey(typeof(Sprite))]
    private string fearSpriteKey = "Buff1 AP"; // 呼吸灯图像资源键（默认图集 Buff1 AP 的第二个子图）。
    [SerializeField, Min(0f)]
    private float yOffset = 2f;             // 图像相对单位重心 y 轴上的偏移（默认 2 格）。
    [SerializeField, Min(0f)]
    private float breathSpeed = 2f;         // 呼吸频率（每秒周期数）。
    [SerializeField, Range(0f, 1f)]
    private float breathMinAlpha = 0.35f;   // 呼吸透明度下限。
    [SerializeField, Range(0f, 1f)]
    private float breathMaxAlpha = 0.65f;   // 呼吸透明度上限。

    public override float buffSustainTime => duration;
    public override bool isDeBuff => true;

    /// <summary>每层持续时间，供层管理器读取。</summary>
    public float Duration => duration;
    /// <summary>畏惧期间移速加成比例，供层管理器读取。</summary>
    public float MoveSpeedBonus => moveSpeedBonus;
    /// <summary>随机换向最小间隔，供层管理器读取。</summary>
    public float MinChangeInterval => minChangeInterval;
    /// <summary>随机换向最大间隔，供层管理器读取。</summary>
    public float MaxChangeInterval => maxChangeInterval;
    /// <summary>呼吸灯图像资源键，供层管理器读取。</summary>
    public string FearSpriteKey => fearSpriteKey;
    /// <summary>图像 y 偏移，供层管理器读取。</summary>
    public float YOffset => yOffset;
    /// <summary>呼吸频率，供层管理器读取。</summary>
    public float BreathSpeed => breathSpeed;
    /// <summary>呼吸透明度下限，供层管理器读取。</summary>
    public float BreathMinAlpha => breathMinAlpha;
    /// <summary>呼吸透明度上限，供层管理器读取。</summary>
    public float BreathMaxAlpha => breathMaxAlpha;

    /// <summary>供运行时创建/配置实例时设置每层持续时间。</summary>
    public void SetDuration(float seconds)
    {
        duration = Mathf.Max(0.1f, seconds);
    }

    #region Buff 生命周期
    /// <summary>
    /// 叠加一层畏惧；不设层数上限，每层独立计时。
    /// </summary>
    public override bool ApplyBuff(GameObjectProperty prop)
    {
        if (prop == null || prop.isDead)
            return false;

        FearState state = prop.GetComponent<FearState>();
        if (state == null)
            state = prop.gameObject.AddComponent<FearState>();

        return state.AddLayer(this);
    }

    /// <summary>
    /// 从层管理器移除由当前实例施加的一层畏惧。
    /// </summary>
    public override bool CancelBuff(GameObjectProperty prop)
    {
        if (prop == null)
            return false;

        FearState state = prop.GetComponent<FearState>();
        return state != null && state.RemoveLayer(this);
    }
    #endregion
}

/// <summary>
/// 目标身上的畏惧层管理器：无限叠加、每层独立到期，效果单份；
/// 生效期间清除目标与路径（停止索敌），由 CharacterAI 调用 DoFearMove 随机乱跑，
/// 并驱动呼吸灯图像表现；全部层消失后销毁自身恢复正常 AI。
/// </summary>
internal class FearState : MonoBehaviour
{
    /// <summary>单层畏惧快照。</summary>
    private class Layer
    {
        public FearDebuff source;   // 施加该层的实例，用于取消时匹配。
        public float expireTime;    // 该层到期时间。
    }

    private readonly List<Layer> layers = new List<Layer>();

    private GameObjectProperty prop;
    private SpriteRenderer fearIcon;        // 呼吸灯图像（子级，运行时创建）。
    private float yOffset = 2f;
    private float breathSpeed = 2f;
    private float breathMinAlpha = 0.35f;
    private float breathMaxAlpha = 0.65f;
    private float moveSpeedBonus = 0.5f;    // 畏惧期间移速加成比例。
    private float minChangeInterval = 0.5f;
    private float maxChangeInterval = 1.5f;
    private float baseMoveSpeed;            // 首个层激活时快照的基础移速。

    private Vector2 fearDir = Vector2.right; // 当前随机乱跑方向。
    private float nextDirTime = -1f;         // 下一次随机换向的时间。
    private bool warnedMissingSprite;        // 是否已输出过图像缺失警告（一次性）。

    /// <summary>畏惧状态是否生效（存在任意层）。</summary>
    public bool IsActive => layers.Count > 0;

    private void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
    }

    /// <summary>
    /// 无限叠加一层畏惧；首个层加入时快照配置、停止索敌并创建呼吸灯图像。
    /// </summary>
    public bool AddLayer(FearDebuff source)
    {
        if (source == null || prop == null)
            return false;

        if (layers.Count == 0)
        {
            yOffset = source.YOffset;
            breathSpeed = source.BreathSpeed;
            breathMinAlpha = source.BreathMinAlpha;
            breathMaxAlpha = source.BreathMaxAlpha;
            moveSpeedBonus = source.MoveSpeedBonus;
            minChangeInterval = source.MinChangeInterval;
            maxChangeInterval = source.MaxChangeInterval;
            // 停止索敌：清除当前目标与路径，禁止攻击。
            prop.target = null;
            prop.path.Clear();
            prop.isAttack = false;
            // 畏惧期间移速 +50%（快照激活瞬间的基础移速，解除时还原）。
            baseMoveSpeed = prop.moveSpeed;
            prop.moveSpeed = baseMoveSpeed * (1f + moveSpeedBonus);
            CreateIcon(source.FearSpriteKey);
        }

        layers.Add(new Layer
        {
            source = source,
            expireTime = Time.time + source.Duration,
        });

        return true;
    }

    /// <summary>
    /// 移除由指定实例施加的一层，用于 Buff 取消流程。
    /// </summary>
    public bool RemoveLayer(FearDebuff source)
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

        if (layers.Count == 0)
            return;

        // 防御性保持不索敌（避免其他逻辑重新赋目标）。
        if (prop != null && prop.target != null)
            prop.target = null;

        UpdateBreathing();
    }

    /// <summary>
    /// 随机乱跑移动（由 CharacterAI.AIBehaviour 在畏惧期间每帧调用）：
    /// 每隔随机间隔更换一个随机方向，按当前移速自由移动并同步水平朝向。
    /// </summary>
    public void DoFearMove(GameObject self, GameObjectProperty ownerProp, CharacterHealth health)
    {
        if (self == null || ownerProp == null)
            return;

        if (Time.time >= nextDirTime)
        {
            nextDirTime = Time.time + Random.Range(minChangeInterval, maxChangeInterval);
            fearDir = Random.insideUnitCircle;
            if (fearDir.sqrMagnitude < 0.01f)
                fearDir = Vector2.right;
            fearDir.Normalize();
        }

        Vector3 pos = self.transform.position;
        pos += new Vector3(fearDir.x, fearDir.y, 0f) * ownerProp.moveSpeed * Time.deltaTime;
        self.transform.position = pos;

        if (Mathf.Abs(fearDir.x) > 0.01f)
            ownerProp.isFacingLeft = fearDir.x < 0f;

        self.transform.localScale = new Vector3(ownerProp.isFacingLeft ? -1f : 1f, 1f, 1f);
    }

    /// <summary>
    /// 创建呼吸灯图像（子级 SpriteRenderer），位于单位重心 y 轴上方 yOffset 处。
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
                Debug.LogWarning($"[FearDebuff] 精灵资源 {spriteKey} 未加载，畏惧呼吸灯图像无法显示。", this);
            }
            return;
        }

        GameObject child = new GameObject("FearIcon");
        child.transform.SetParent(transform, false);
        child.transform.localPosition = new Vector3(0f, yOffset, 0f);

        fearIcon = child.AddComponent<SpriteRenderer>();
        fearIcon.sprite = sprite;
        fearIcon.color = new Color(1f, 1f, 1f, breathMaxAlpha);

        if (renderers != null && renderers.Length > 0)
        {
            fearIcon.sortingLayerID = renderers[0].sortingLayerID;
            fearIcon.sortingOrder = renderers[0].sortingOrder + 1;
        }
    }

    /// <summary>
    /// 驱动呼吸灯图像：透明度在 min 与 max 之间正弦波动。
    /// </summary>
    private void UpdateBreathing()
    {
        if (fearIcon == null)
            return;

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * breathSpeed * Mathf.PI * 2f);
        float alpha = Mathf.Lerp(breathMinAlpha, breathMaxAlpha, wave);
        Color color = fearIcon.color;
        color.a = alpha;
        fearIcon.color = color;
    }

    private void RemoveAt(int index)
    {
        layers.RemoveAt(index);

        if (layers.Count == 0)
        {
            // 畏惧结束：还原移速。
            if (prop != null)
                prop.moveSpeed = baseMoveSpeed;
            DestroyIcon();
            Destroy(this);
        }
    }

    private void OnDisable()
    {
        // 停用/回收时还原移速。
        if (prop != null && layers.Count > 0)
            prop.moveSpeed = baseMoveSpeed;
        DestroyIcon();
        layers.Clear();
    }

    private void DestroyIcon()
    {
        if (fearIcon != null)
        {
            Destroy(fearIcon.gameObject);
            fearIcon = null;
        }
    }
}
