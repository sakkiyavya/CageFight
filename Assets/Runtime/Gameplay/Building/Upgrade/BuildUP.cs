using TMPro;
using UnityEngine;

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

    GameObjectProperty prop;
    BuildingHealth health;
    SpriteRenderer body;
    Color bodyColor;
    int level;

    public bool CanUpgrade =>
        levels != null && level < levels.Length - 1;

    public int Cost =>
        CanUpgrade ? levels[level + 1].cost : 0;

    void Awake()
    {
        prop = GetComponent<GameObjectProperty>();
        health = GetComponent<BuildingHealth>();
        body = GetComponent<SpriteRenderer>();
        if (body) bodyColor = body.color;
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

        int oldMaxHp = prop ? prop.maxHp : 0;
        int oldHp = health ? health.HP : oldMaxHp;
        level++;
        ApplyLevel();
        if (health && prop && prop.maxHp > 0)
        {
            int newHp = Mathf.Clamp(oldHp + prop.maxHp - oldMaxHp, 0, prop.maxHp);
            health.SetPercentHp((float)newHp / prop.maxHp);
        }
        BuildingUpgradeButton.CloseAll();
        return true;
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
