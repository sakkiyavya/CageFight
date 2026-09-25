using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 升级面板里的 UP 按钮：点击后消耗金条（局外资源）把对应建筑等级 +1。
/// 走 EventSystem（IPointerDownHandler），显示刷新由 UpgradePanelView 订阅 Changed 完成。
/// </summary>
public class BuildingLevelUpButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private UpgradeBuildingKind kind = UpgradeBuildingKind.Barracks;

    public void OnPointerDown(PointerEventData eventData)
    {
        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
        {
            Debug.LogError("[BuildingLevelUpButton] UserGlobalInfo 缺失。", this);
            return;
        }

        int level = UpgradeLevelRules.GetLevel(kind);
        int cost = UpgradeLevelRules.GetUpgradeCost(level);
        if (info.GoldBarCount < cost)
        {
            CurrencyFeedbackAudio.PlayWrong();   // 不满足要求：Wrong UI AD。
            UpgradeNoticeText.Show("货币不足");
            return;
        }

        UpgradeLevelRules.TryUpgrade(kind);
    }
}
