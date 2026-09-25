using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 每日登录面板的 OK 领取按钮：点击后发放金条 + 钻石奖励并关闭面板。
/// 奖励直接写入 UserGlobalInfo（Changed 事件自动触发存档）。
/// 走 EventSystem（IPointerDownHandler），与项目内其它 UI 按钮同一套点击约定。
/// </summary>
public class DailyLoginClaimButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField] private int goldBarReward = 30;
    [SerializeField] private int diamondReward = 30;
    [SerializeField] private GameObject panelToClose;   // 领取后关闭的面板（Daily login Canvas 根节点）。

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[DailyLoginClaimButton] 领取每日奖励：金条+{goldBarReward}，钻石+{diamondReward}。", this);

        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
        {
            Debug.LogError("[DailyLoginClaimButton] UserGlobalInfo 缺失，无法发放奖励。", this);
            return;
        }

        info.SetGoldBarCount(info.GoldBarCount + goldBarReward);
        info.SetDiamondCount(info.DiamondCount + diamondReward);

        if (panelToClose != null)
            panelToClose.SetActive(false);
    }
}
