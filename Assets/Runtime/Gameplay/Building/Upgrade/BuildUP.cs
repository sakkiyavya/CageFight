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

    GameObjectProperty prop;
    BuildingHealth health;
    SpriteRenderer body;
    Color bodyColor;
    Vector3 baseScale;
    int level;
    bool upgrading;
    AudioSource upgradeAudio;

    public bool CanUpgrade =>
        !upgrading && levels != null && level < levels.Length - 1;

    /// <summary>当前等级（0 起），供维护费、UI 等外部逻辑读取。</summary>
    public int CurrentLevel => level;

    public int Cost =>
        CanUpgrade ? levels[level + 1].cost : 0;

    void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<BuildingHealth>();
        body = GetComponent<SpriteRenderer>();
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
        BuildingUpgradeButton.Register(this);
    }

    void OnDisable()
    {
        BuildingUpgradeButton.Unregister(this);
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade || !Coins.Instance ||
            !Coins.Instance.ConsumeCoins(Cost))
            return false;

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

    void ApplyLevel()
    {
        if (!prop || levels == null || levels.Length == 0)
            return;

        level = Mathf.Clamp(level, 0, levels.Length - 1);
        LevelData data = levels[level];

        prop.maxHp = data.maxHp;
        prop.atk = data.attack;
        prop.atkRange = data.attackRange;

        if (body && data.sprite)
            body.sprite = data.sprite;
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
/// 事件走 EventSystem（IPointerDownHandler）：Physics2DRaycaster 挂在主相机上——
/// 主相机自带 Camera 组件，不会触发 RequireComponent 自动补相机，且射线使用主相机的真实投影。
/// </summary>
class BuildingUpgradeCoinClick : MonoBehaviour, IPointerDownHandler
{
    /// <summary>金币图标专用物理层序号（TagManager 第 8 层，索引 7）。</summary>
    public const int UpgradeCoinLayer = 7;

    public BuildUP owner;

    private static bool warnedMissingCamera;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (BuildingUpgradeButton.Active && owner)
            owner.TryUpgrade();
    }

    /// <summary>
    /// 确保主相机上挂有 Physics2DRaycaster（仅检测 UpgradeCoin 层）。
    /// 主相机已有 Camera 组件，添加射线器不会触发 RequireComponent 自动补相机，
    /// 射线直接复用主相机的真实投影，无需任何额外相机。
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
        if (raycaster == null)
        {
            raycaster = main.gameObject.AddComponent<Physics2DRaycaster>();
        }

        // 只检测金币图标专用层，避免把其它 2D 碰撞体误判为 UI。
        raycaster.eventMask = 1 << UpgradeCoinLayer;
    }
}
