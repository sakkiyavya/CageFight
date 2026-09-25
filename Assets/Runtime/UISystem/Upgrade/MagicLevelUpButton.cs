using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 升级面板 Magic 页的 UP 按钮（PWUP = 攻击魔法，GDUP = 防御魔法）：
/// 点击后消耗钻石把对应魔法等级 +1；钻石不足时弹出“钻石不足”提示。
/// 走 EventSystem（IPointerDownHandler），显示刷新由 UpgradePanelView 订阅 Changed 完成。
/// </summary>
public class MagicLevelUpButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private UpgradeMagicKind kind = UpgradeMagicKind.AttackMagic;

    public void OnPointerDown(PointerEventData eventData)
    {
        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
        {
            Debug.LogError("[MagicLevelUpButton] UserGlobalInfo 缺失。", this);
            return;
        }

        int level = UpgradeLevelRules.GetMagicLevel(kind);
        int cost = UpgradeLevelRules.GetMagicUpgradeCost(level);
        if (info.DiamondCount < cost)
        {
            CurrencyFeedbackAudio.PlayWrong();   // 不满足要求：Wrong UI AD。
            UpgradeNoticeText.Show("货币不足");
            return;
        }

        UpgradeLevelRules.TryUpgradeMagic(kind);
    }
}
