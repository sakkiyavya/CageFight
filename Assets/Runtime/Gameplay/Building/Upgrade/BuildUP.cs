using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;

public class BuildUP : MonoBehaviour
{
    [System.Serializable]
    public struct LevelData
    {
        public Sprite sprite;
        public int maxHp;
        public int attack;
        public Vector2Int attackRange;
        public int cost;
    }

    [Header("三个等级")]
    public LevelData[] levels = new LevelData[3];

    [Header("升级提示")]
    public GameObject upgradeMark;
    public SpriteRenderer coinIcon;
    public TMP_Text costText;

    [Header("升级表现")]
    [ResourceKey(typeof(GameObject))]
    [SerializeField] string upgradeEffectPrefabKey = "UPanime"; // 升级特效预制体资源键（经资源框架解析后交对象池）。
    [SerializeField, Min(.05f)] float effectTime = .7f;
    [SerializeField, Min(.05f)] float jellyTime = .22f;
    [SerializeField, Range(0f, .5f)] float jellyAmount = .16f;

    [Header("升级音效")]
    [ResourceKey(typeof(AudioClip))]
    [SerializeField] string upgradeSoundKey = "UP";

    [Header("拆除")]
    [SerializeField, Tooltip("是否允许被拆除（大本营等核心建筑设为 false，不显示红色与金币）")]
    private bool removable = true;
    [ResourceKey(typeof(AudioClip))]
    [SerializeField, Tooltip("拆除时播放的音效资源键")]
    private string demolishSoundKey = "Construct-Hit";
    [SerializeField, Min(0f), Tooltip("拆除动画先行时长（秒，应与拆除动画播放时长一致）；动画播完后再弹动")]
    private float demolishAnimWait = .7f;
    [SerializeField, Min(.01f), Tooltip("拆除掉落动画时长（秒）")]
    private float demolishFallTime = .8f;
    [SerializeField, Min(0f), Tooltip("拆除掉落距离（向下，世界单位；同时保证掉出屏幕底边）")]
    private float demolishFallDistance = 15f;
    [SerializeField, Tooltip("贝塞尔曲线水平摆幅（0 为直线下落）")]
    private float demolishSway = .6f;
    [SerializeField, Range(0f, 1f), Tooltip("掉落前建筑本体的透明度")]
    private float demolishAlpha = .5f;

    GameObjectProperty prop;
    BuildingHealth health;
    BuildingBase buildingBase;
    SpriteRenderer body;
    Color bodyColor;
    Vector3 baseScale;
    int level;
    bool upgrading;
    AudioSource upgradeAudio;
    private int _paidUpgradeCost;                 // 本轮启用周期内已支付的升级费用（池化复用时归零）。
    private bool _demolishing;                    // 拆除流程进行中（弹动阶段防重复触发）。

    public bool CanUpgrade =>
        !upgrading && levels != null && level < levels.Length - 1;

    /// <summary>当前等级（0 起），供维护费、UI 等外部逻辑读取。</summary>
    public int CurrentLevel => level;

    public int Cost =>
        CanUpgrade ? levels[level + 1].cost : 0;

    /// <summary>本建筑总花费 = 建筑费用（levels[0].cost）+ 已支付的升级费用。</summary>
    public int TotalSpent =>
        (levels != null && levels.Length > 0 ? levels[0].cost : 0) + _paidUpgradeCost;

    /// <summary>拆除返还金额：总花费的 50%（向下取整，不为负）。</summary>
    public int RefundAmount => Mathf.Max(0, TotalSpent / 2);

    void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<BuildingHealth>();
        body = GetComponent<SpriteRenderer>();
        buildingBase = GetComponent<BuildingBase>();
        upgradeAudio = GetComponent<AudioSource>();
        if (!upgradeAudio)
        {
            upgradeAudio = gameObject.AddComponent<AudioSource>();
            upgradeAudio.playOnAwake = false;
            upgradeAudio.spatialBlend = 0f;
        }
        if (body) bodyColor = body.color;
        baseScale = transform.localScale;
        if (coinIcon)
        {
            // 金币图标的触发器碰撞体已由 CoinIcon 预制体配置，这里挂点击处理并配置输入层。
            BuildingUpgradeCoinClick click = coinIcon.GetComponent<BuildingUpgradeCoinClick>();
            if (!click) click = coinIcon.gameObject.AddComponent<BuildingUpgradeCoinClick>();
            click.owner = this;

            coinIcon.gameObject.layer = BuildingUpgradeCoinClick.UpgradeCoinLayer;
            BuildingUpgradeCoinClick.EnsurePhysics2DRaycaster();
        }

        ApplyLevel();
        ShowUpgrade(false);
    }

    void OnEnable()
    {
        // 池化复用：拆除后重建的建筑回到 1 级初始状态（否则 CanUpgrade 恒为 false）。
        level = 0;
        upgrading = false;
        transform.localScale = baseScale;
        ApplyLevel();
        if (body) body.color = bodyColor;
        _paidUpgradeCost = 0;
        _demolishing = false;

        BuildingUpgradeButton.Register(this);
        if (removable)
            BuildingRemoveButton.Register(this);

        // 预载拆除音效（经资源框架异步预载后写入音频缓存，供 GetAudio 解析；
        // 已缓存时跳过，避免每个建筑重复发起加载）。
        if (ResourceManager.Instance &&
            ResourceManager.Instance.GetAudio(demolishSoundKey) == null)
        {
            ResourceManager.Instance.LoadExtraResourceAsync<AudioClip>(demolishSoundKey);
        }
    }

    void OnDisable()
    {
        BuildingUpgradeButton.Unregister(this);
        BuildingRemoveButton.Unregister(this);
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade || !Coins.Instance)
            return false;

        int cost = Cost;                          // 在置位 upgrading 前先取值（Cost 依赖 CanUpgrade）。
        if (!Coins.Instance.ConsumeCoins(cost))
            return false;

        _paidUpgradeCost += cost;                 // 累计已付升级费（拆除返还按总花费 50% 计入）。
        upgrading = true;
        PlayUpgradeSound();
        StartCoroutine(UpgradeRoutine());
        BuildingUpgradeButton.CloseAll();
        return true;
    }

    IEnumerator UpgradeRoutine()
    {
        GameObject upgradeEffect = null;
        if (!string.IsNullOrEmpty(upgradeEffectPrefabKey) &&
            ResourceManager.Instance && GameObjectPool.Instance)
        {
            // 升级特效预制体先经资源框架按资源键解析，再交给对象池生成。
            GameObject upgradeEffectPrefab = ResourceManager.Instance.GetGameObject(upgradeEffectPrefabKey);
            if (upgradeEffectPrefab)
            {
                upgradeEffect = GameObjectPool.Instance.Get(upgradeEffectPrefab);
                upgradeEffect.transform.position = transform.position;
                upgradeEffect.transform.rotation = Quaternion.identity;
                Animator animator = upgradeEffect.GetComponent<Animator>();
                SpriteRenderer effectBody = upgradeEffect.GetComponent<SpriteRenderer>();
                if (effectBody && body)
                {
                    effectBody.sortingLayerID = body.sortingLayerID;
                    effectBody.sortingOrder = body.sortingOrder + 1;
                }
                if (animator)
                {
                    animator.Rebind();
                    animator.Play(0, 0, 0f);
                }
            }
        }

        yield return new WaitForSeconds(effectTime);

        int oldMaxHp = prop ? prop.maxHp : 0;
        int oldHp = health ? health.HP : oldMaxHp;
        level++;
        ApplyLevel();
        if (health && prop && prop.maxHp > 0)
        {
            int newHp = Mathf.Clamp(oldHp + prop.maxHp - oldMaxHp, 0, prop.maxHp);
            health.SetPercentHp((float)newHp / prop.maxHp);
        }

        float elapsed = 0f;
        while (elapsed < jellyTime)
        {
            elapsed += Time.deltaTime;
            float wave = Mathf.Sin(elapsed / jellyTime * Mathf.PI * 2.5f) *
                         (1f - elapsed / jellyTime);
            transform.localScale = new Vector3(
                baseScale.x * (1f + wave * jellyAmount),
                baseScale.y * (1f - wave * jellyAmount * .7f),
                baseScale.z);
            yield return null;
        }

        transform.localScale = baseScale;
        if (upgradeEffect) GameObjectPool.Instance.Release(upgradeEffect);
        upgrading = false;
    }

    public void ShowUpgrade(bool show)
    {
        show &= CanUpgrade;

        if (upgradeMark)
            upgradeMark.SetActive(show);

        if (body)
        {
            body.color = show
                ? new Color(.35f, .65f, 1f)
                : bodyColor;
        }

        if (show)
            RefreshCost();
    }

    public void RefreshCost()
    {
        bool enough = Coins.Instance &&
                      Coins.Instance.CurrentCoins >= Cost;

        if (costText)
        {
            costText.text = Cost.ToString();
            costText.color = enough
                ? Color.white
                : Color.gray;
        }

        if (coinIcon)
        {
            coinIcon.color = enough
                ? Color.white
                : new Color(.3f, .3f, .3f, 1f);
        }
    }

    /// <summary>
    /// 拆除模式表现：显示顶部金币图标与返还金额文本，建筑本体变红；
    /// 关闭时恢复本色并隐藏标记。大本营（removable=false）与拖拽预览/未完工的建筑不显示。
    /// </summary>
    /// <param name="show">是否进入拆除模式显示。</param>
    public void ShowRemove(bool show)
    {
        bool display = show && removable &&
            buildingBase != null && buildingBase.IsCompleted &&
            !(BuildingPlace.Instance != null &&
              BuildingPlace.Instance.IsBuildingInPreview(buildingBase));

        if (upgradeMark)
            upgradeMark.SetActive(display);
        if (body)
            body.color = display ? new Color(1f, .35f, .35f, 1f) : bodyColor;
        if (!display)
            return;

        if (coinIcon)
            coinIcon.color = Color.white;
        if (costText)
        {
            costText.text = RefundAmount.ToString();
            costText.color = Color.white;
        }
    }

    /// <summary>
    /// 执行拆除：先播放拆除音效、建筑弹动一下，弹动结束后播放拆除动画、
    /// 返还 50% 总花费并回收建筑本体。大本营或处于拖拽预览/拆除流程中的建筑不可重复拆除。
    /// </summary>
    /// <returns>是否进入了拆除流程。</returns>
    public bool TryRemove()
    {
        if (!removable || _demolishing)
            return false;
        if (buildingBase != null && BuildingPlace.Instance != null &&
            BuildingPlace.Instance.IsBuildingInPreview(buildingBase))
            return false;

        _demolishing = true;
        StartCoroutine(DemolishRoutine());
        return true;
    }

    /// <summary>
    /// 拆除序列：①先触发拆除动画并等待其播完；②随后弹动效果与音效同时触发；
    /// ③弹动结束后建筑变半透明；④沿贝塞尔曲线掉落出屏幕；⑤落地后返还 50% 总花费并回收建筑
    /// （占地/登记/维护费由各组件 OnDisable 自动清理）。
    /// </summary>
    private IEnumerator DemolishRoutine()
    {
        // 1. 先触发拆除动画（UPanime），同时收起顶部金币标记。
        Vector3 position = transform.position;
        BuildingRemoveButton.PlayDemolishEffect(position);
        if (upgradeMark)
            upgradeMark.SetActive(false);

        // 动画先行：等待拆除动画播完（demolishAnimWait 应与动画时长一致）。
        if (demolishAnimWait > 0f)
            yield return new WaitForSeconds(demolishAnimWait);

        // 2. 弹动效果与音效同时触发（幅度/时长复用升级果冻配置 jellyAmount/jellyTime）。
        PlayDemolishSound();
        float elapsed = 0f;
        while (elapsed < jellyTime)
        {
            elapsed += Time.deltaTime;
            float wave = Mathf.Sin(elapsed / jellyTime * Mathf.PI * 2.5f) *
                         (1f - elapsed / jellyTime);
            transform.localScale = new Vector3(
                baseScale.x * (1f + wave * jellyAmount),
                baseScale.y * (1f - wave * jellyAmount * .7f),
                baseScale.z);
            yield return null;
        }
        transform.localScale = baseScale;

        // 3. 建筑变半透明（保留当前颜色，仅降低透明度）。
        if (body)
        {
            Color color = body.color;
            color.a = demolishAlpha;
            body.color = color;
        }

        // 4. 沿二次贝塞尔曲线掉落出屏幕（至少下落 demolishFallDistance，
        //    且终点保证低于主相机屏幕底边）。
        Vector3 start = transform.position;
        float targetY = start.y - demolishFallDistance;
        if (AudioManager.Instance != null && AudioManager.Instance.MainCamera != null)
        {
            Vector3 bottom = AudioManager.Instance.MainCamera.ScreenToWorldPoint(
                new Vector3(Screen.width * .5f, 0f, 0f));
            targetY = Mathf.Min(targetY, bottom.y - 1.5f);
        }

        Vector3 end = start;
        end.y = targetY;
        Vector3 control = Vector3.Lerp(start, end, .5f) + Vector3.right * demolishSway;
        float t = 0f;
        while (t < 1f)
        {
            t = Mathf.Clamp01(t + Time.deltaTime / Mathf.Max(.01f, demolishFallTime));
            float u = 1f - t;
            transform.position = u * u * start + 2f * u * t * control + t * t * end;
            yield return null;
        }

        // 5. 消失：返还 50% 总花费并回收建筑。
        int refund = RefundAmount;
        if (Coins.Instance != null && refund > 0)
            Coins.Instance.GainCoins(refund);

        if (GameObjectPool.Instance != null)
            GameObjectPool.Instance.Release(gameObject);
        else
            gameObject.SetActive(false);
    }

    /// <summary>播放拆除音效（复用升级缓存音频源，经 AudioManager 播放）。</summary>
    private void PlayDemolishSound()
    {
        if (string.IsNullOrEmpty(demolishSoundKey) || !ResourceManager.Instance ||
            !AudioManager.Instance || !upgradeAudio) return;

        AudioClip clip = ResourceManager.Instance.GetAudio(demolishSoundKey);
        if (!clip) return;

        upgradeAudio.clip = clip;
        upgradeAudio.volume = 1f;
        upgradeAudio.priority = 32;
        AudioManager.Instance.PlayEffectAt(upgradeAudio, (uint)upgradeAudio.priority, transform);
    }

    void ApplyLevel()
    {
        if (!prop || levels == null || levels.Length == 0)
            return;

        level = Mathf.Clamp(level, 0, levels.Length - 1);
        LevelData data = levels[level];

        // 经建筑生命框架受控 API 应用升级后的最大生命（业务不得直写 maxHp）。
        if (health)
            health.SetMaxHp(data.maxHp);
        prop.atk = data.attack;
        prop.atkRange = data.attackRange;

        if (body && data.sprite)
            body.sprite = data.sprite;

        // 同步写入框架的建筑等级数据（BuildingBase.Level，显示等级 1 起），
        // 供训练解锁等业务按框架 API 读取；业务不再回读本组件的 CurrentLevel。
        if (buildingBase)
            buildingBase.SetLevel(level + 1);
    }

    void PlayUpgradeSound()
    {
        if (string.IsNullOrEmpty(upgradeSoundKey) || !ResourceManager.Instance ||
            !AudioManager.Instance || !upgradeAudio) return;

        AudioClip clip = ResourceManager.Instance.GetAudio(upgradeSoundKey);
        if (!clip) return;

        upgradeAudio.clip = clip;
        upgradeAudio.volume = 1f;
        upgradeAudio.priority = 32;
        AudioManager.Instance.PlayEffectAt(upgradeAudio, (uint)upgradeAudio.priority, transform);
    }

}

/// <summary>
/// 金币图标点击（世界空间 Sprite + 预制体配置的触发器）：升级模式下点击图标即消耗对应金币升级。
/// 事件走 EventSystem（IPointerDownHandler）：Physics2DRaycaster 由场景统一预配置在主相机上
/// （Event Mask 仅含金币图标层），本组件只做校验与告警——规范禁止运行时添加射线器/改写 eventMask。
/// </summary>
class BuildingUpgradeCoinClick : MonoBehaviour, IPointerDownHandler
{
    /// <summary>金币图标专用物理层序号（TagManager 第 8 层，索引 7）。</summary>
    public const int UpgradeCoinLayer = 7;

    public BuildUP owner;

    private static bool warnedMissingCamera;

    public void OnPointerDown(PointerEventData eventData)
    {
        // 拆除模式优先：点击金币执行拆除（此时升级模式已互斥关闭）。
        if (BuildingRemoveButton.Active)
        {
            if (owner) owner.TryRemove();
            return;
        }

        if (BuildingUpgradeButton.Active && owner)
            owner.TryUpgrade();
    }

    /// <summary>
    /// 校验主相机已按场景配置预挂载 Physics2DRaycaster 且 Event Mask 仅含金币图标层
    /// （场景/输入框架统一预配置；规范禁止运行时 AddComponent 与改写 eventMask）。
    /// 缺失或配置不符时输出一次性警告，不再运行时自动补装。
    /// </summary>
    public static void EnsurePhysics2DRaycaster()
    {
        Camera main = Camera.main;
        if (main == null)
        {
            if (!warnedMissingCamera)
            {
                warnedMissingCamera = true;
                Debug.LogWarning("[BuildUP] 场景中缺少主相机，建筑升级点击无法工作。");
            }
            return;
        }

        Physics2DRaycaster raycaster = main.GetComponent<Physics2DRaycaster>();
        if (raycaster == null || raycaster.eventMask != (1 << UpgradeCoinLayer))
        {
            if (!warnedMissingCamera)
            {
                warnedMissingCamera = true;
                Debug.LogWarning("[BuildUP] 主相机未预配置 Physics2DRaycaster（Event Mask 需仅含金币图标层），建筑升级点击无法工作。");
            }
        }
    }
}
