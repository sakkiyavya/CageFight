using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>点击前往侧边栏；确认复访后再次点击，同时领取全局金币和钻石。</summary>
[DisallowMultipleComponent]
public sealed class SidebarRevisitRewardButton : MonoBehaviour, IPointerClickHandler
{
    private const int GoldRewardAmount = 100;
    private const int DiamondRewardAmount = 10;

    public void OnPointerClick(PointerEventData eventData)
    {
        var sidebar = DouyinSidebarRevisit.Instance;
        var info = UserGlobalInfo.Instance;
        if (sidebar == null || info == null) return;
        var persistence = info.GetComponent<UserGlobalInfoPersistence>();
        if (persistence != null && !persistence.IsLoaded) return;

        if (!sidebar.IsReturnedFromSidebar)
        {
            sidebar.NavigateToSidebarFromUserClick();
            return;
        }

        // 先消费本次复访，避免余额变化事件触发重入或多个按钮重复领取。
        sidebar.ConsumeSidebarReturn();
        info.SetGoldBarCount((int)System.Math.Min(int.MaxValue, (long)info.GoldBarCount + GoldRewardAmount));
        info.SetDiamondCount((int)System.Math.Min(int.MaxValue, (long)info.DiamondCount + DiamondRewardAmount));
    }
}
