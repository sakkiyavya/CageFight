using TMPro;
using UnityEngine;

/// <summary>
/// 升级面板（Upgrade Canvas）显示视图：
/// 三个建筑行——LV 文本显示当前等级，UP 子级文本显示升级所需金条
/// （金条不足时文字变灰）；Base LV 显示总等级（三个建筑等级中的最低值）。
/// 订阅 UserGlobalInfo.Changed，等级/金条变化时自动刷新。
/// </summary>
public class UpgradePanelView : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI barracksLvText;
    [SerializeField] private TextMeshProUGUI darkBarracksLvText;
    [SerializeField] private TextMeshProUGUI sentryLvText;
    [SerializeField] private TextMeshProUGUI barracksCostText;
    [SerializeField] private TextMeshProUGUI darkBarracksCostText;
    [SerializeField] private TextMeshProUGUI sentryCostText;
    [SerializeField] private TextMeshProUGUI baseLvText;
    [SerializeField] private TextMeshProUGUI pwUpCostText;   // 攻击魔法升级费用（钻石，蓝色）。
    [SerializeField] private TextMeshProUGUI gdUpCostText;   // 防御魔法升级费用（钻石，蓝色）。

    private static readonly Color DiamondBlue = new Color(0.25f, 0.55f, 1f, 1f);          // 钻石费用主色（蓝色）。
    private static readonly Color DiamondBlueDim = new Color(0.3f, 0.36f, 0.5f, 1f);      // 钻石不足时的灰蓝。

    private UserGlobalInfo _info;

    private void OnEnable()
    {
        SubscribeAndRefresh();
    }

    private void OnDisable()
    {
        if (_info != null)
        {
            _info.Changed -= Refresh;
            _info = null;
        }
    }

    private void SubscribeAndRefresh()
    {
        if (_info != null)
            _info.Changed -= Refresh;

        _info = UserGlobalInfo.Instance;
        if (_info != null)
            _info.Changed += Refresh;

        Refresh();
    }

    private void Refresh()
    {
        if (_info == null)
            return;

        int gold = _info.GoldBarCount;
        int diamonds = _info.DiamondCount;

        SetLevelText(barracksLvText, UpgradeBuildingKind.Barracks);
        SetLevelText(darkBarracksLvText, UpgradeBuildingKind.DarkBarracks);
        SetLevelText(sentryLvText, UpgradeBuildingKind.Sentry);

        SetCostText(barracksCostText, UpgradeBuildingKind.Barracks, gold);
        SetCostText(darkBarracksCostText, UpgradeBuildingKind.DarkBarracks, gold);
        SetCostText(sentryCostText, UpgradeBuildingKind.Sentry, gold);

        SetMagicCostText(pwUpCostText, UpgradeMagicKind.AttackMagic, diamonds);
        SetMagicCostText(gdUpCostText, UpgradeMagicKind.DefenseMagic, diamonds);

        if (baseLvText != null)
            baseLvText.text = _info.TotalLevel.ToString();
    }

    private static void SetLevelText(TextMeshProUGUI text, UpgradeBuildingKind kind)
    {
        if (text != null)
            text.text = UpgradeLevelRules.GetLevel(kind).ToString();
    }

    private static void SetCostText(TextMeshProUGUI text, UpgradeBuildingKind kind, int gold)
    {
        if (text == null)
            return;

        int level = UpgradeLevelRules.GetLevel(kind);
        int cost = UpgradeLevelRules.GetUpgradeCost(level);
        text.text = cost.ToString();
        text.color = gold >= cost ? Color.white : new Color(0.6f, 0.6f, 0.6f, 1f);
    }

    /// <summary>魔法费用文本：蓝色代表钻石货币；钻石不足时用灰蓝。</summary>
    private static void SetMagicCostText(TextMeshProUGUI text, UpgradeMagicKind kind, int diamonds)
    {
        if (text == null)
            return;

        int level = UpgradeLevelRules.GetMagicLevel(kind);
        int cost = UpgradeLevelRules.GetMagicUpgradeCost(level);
        text.text = cost.ToString();
        text.color = diamonds >= cost ? DiamondBlue : DiamondBlueDim;
    }
}
