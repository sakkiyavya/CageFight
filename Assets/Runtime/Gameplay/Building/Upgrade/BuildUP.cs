using TMPro;
using UnityEngine;
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
    [SerializeField] GameObject upgradeEffectPrefab;
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
            BuildingUpgradeCoinClick click = coinIcon.GetComponent<BuildingUpgradeCoinClick>();
            if (!click) click = coinIcon.gameObject.AddComponent<BuildingUpgradeCoinClick>();
            click.owner = this;
            if (!coinIcon.GetComponent<Collider2D>())
            {
                BoxCollider2D box = coinIcon.gameObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                if (coinIcon.sprite) box.size = coinIcon.sprite.bounds.size * 1.25f;
            }
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
        if (upgradeEffectPrefab && GameObjectPool.Instance)
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
        Camera cam = Camera.main;
        AudioManager.Instance.PlayEffect(upgradeAudio, (uint)upgradeAudio.priority,
            cam ? Vector3.Distance(transform.position, cam.transform.position) : 0f, transform);
    }

}

class BuildingUpgradeCoinClick : MonoBehaviour
{
    public BuildUP owner;

    void OnMouseDown()
    {
        if (BuildingUpgradeButton.Active && owner)
            owner.TryUpgrade();
    }
}
