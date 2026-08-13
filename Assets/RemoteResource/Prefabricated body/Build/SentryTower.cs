using TMPro;
using UnityEngine;

public class SentryTower : MonoBehaviour
{
    [System.Serializable]
    public struct LevelData
    {
        public Sprite sprite;
        public int maxHp;
        public int attack;
        public float range;
        public float attacksPerSecond;
        public int upgradeCost;
    }

    [Header("三个等级的数据")]
    public LevelData[] levels = new LevelData[3];

    [Header("攻击")]
    [ResourceKey(typeof(GameObject))]
    public string projectileKey;

    public Transform shootPoint;
    public LayerMask targetLayer = ~0;

    [Header("升级显示")]
    public GameObject upgradeMark;
    public SpriteRenderer coinIcon;
    public TMP_Text costText;

    static readonly Collider2D[] hits = new Collider2D[64];

    GameObjectProperty prop;
    BuildingHealth health;
    SpriteRenderer body;

    int level;
    float nextAttackTime;

    public bool CanUpgrade =>
        levels != null && level < levels.Length - 1;

    public int UpgradeCost =>
        CanUpgrade ? levels[level + 1].upgradeCost : 0;

    void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<BuildingHealth>();
        body = GetComponent<SpriteRenderer>();

        if (!shootPoint)
            shootPoint = transform;

        ApplyLevel(false);
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

    void Update()
    {
        // 建筑施工期间主体图片是关闭的，不进行攻击
        if (!body || !body.enabled ||
            levels == null || levels.Length == 0 ||
            Time.time < nextAttackTime)
            return;

        GameObjectProperty target = FindTarget();
        if (!target) return;

        LevelData data = levels[level];

        nextAttackTime = Time.time +
            1f / Mathf.Max(0.01f, data.attacksPerSecond);

        Shoot(target);
    }

    GameObjectProperty FindTarget()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position,
            levels[level].range,
            hits,
            targetLayer);

        GameObjectProperty nearest = null;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            GameObjectProperty target =
                hits[i].GetComponentInParent<GameObjectProperty>();

            if (!target ||
                target == prop ||
                target.isDead ||
                target.side == prop.side ||
                target.GetComponent<ICollide>() == null)
                continue;

            float distance =
                (target.transform.position -
                 transform.position).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = target;
            }
        }

        return nearest;
    }

    void Shoot(GameObjectProperty target)
    {
        GameObject prefab =
            ResourceManager.Instance.GetGameObject(projectileKey);

        if (!prefab) return;

        GameObject bullet =
            GameObjectPool.Instance.Get(prefab);

        if (!bullet) return;

        bullet.transform.position = shootPoint.position;
        bullet.transform.right =
            target.transform.position - shootPoint.position;

        DamageSource source =
            bullet.GetComponent<DamageSource>();

        if (!source) return;

        source.damage.initialDamage = prop.atk;
        source.damage.source = gameObject;
        source.damage.side = prop.side;
        source.damage.repel = prop.repel;
        source.damage.target = target.gameObject;
        source.target = target.gameObject;
        source.Init();

        prop.OnAtt?.Invoke();
    }

    public void ShowUpgrade(bool show)
    {
        show &= CanUpgrade;

        if (upgradeMark)
            upgradeMark.SetActive(show);

        if (body)
        {
            body.color = show
                ? new Color(0.35f, 0.65f, 1f)
                : Color.white;
        }

        if (show)
            RefreshUpgrade();
    }

    public void RefreshUpgrade()
    {
        bool enough =
            Coins.Instance &&
            Coins.Instance.CurrentCoins >= UpgradeCost;

        if (costText)
        {
            costText.text = UpgradeCost.ToString();
            costText.color =
                enough ? Color.white : Color.gray;
        }

        if (coinIcon)
        {
            coinIcon.color = enough
                ? Color.white
                : new Color(0.3f, 0.3f, 0.3f, 1f);
        }
    }

    public bool TryUpgrade()
    {
        if (!CanUpgrade ||
            !Coins.Instance ||
            !Coins.Instance.ConsumeCoins(UpgradeCost))
            return false;

        level++;
        ApplyLevel(true);
        ShowUpgrade(BuildingUpgradeButton.Active);

        return true;
    }

    void OnMouseDown()
    {
        if (BuildingUpgradeButton.Active)
            TryUpgrade();
    }

    void ApplyLevel(bool preserveHp)
    {
        if (levels == null || levels.Length == 0)
            return;

        level = Mathf.Clamp(level, 0, levels.Length - 1);

        float hpPercent = 1f;

        if (preserveHp && health && prop.maxHp > 0)
        {
            hpPercent = Mathf.Clamp01(
                (float)health.HP / prop.maxHp);
        }

        LevelData data = levels[level];

        prop.maxHp = data.maxHp;
        prop.atk = data.attack;
        prop.atkRate = data.attacksPerSecond;

        if (body && data.sprite)
            body.sprite = data.sprite;

        if (health)
            health.SetPercentHp(hpPercent);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (levels == null || levels.Length == 0)
            return;

        int current =
            Mathf.Clamp(level, 0, levels.Length - 1);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(
            transform.position,
            levels[current].range);
    }
#endif
}
