using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>首页侧边栏入口。保留原脚本名以兼容序列化引用，不发放奖励。</summary>
[DisallowMultipleComponent]
public sealed class SidebarRevisitRewardButton : MonoBehaviour, IPointerClickHandler
{
    private DouyinSidebarRevisit _sidebar;

    private void Start()
    {
        _sidebar = DouyinSidebarRevisit.Instance;
        if (_sidebar != null)
            _sidebar.SidebarSupportChanged += RefreshVisibility;
        RefreshVisibility(_sidebar != null && _sidebar.IsSidebarSupported);
    }

    private void OnDestroy()
    {
        // 不在 OnDisable 退订：入口隐藏后仍需响应异步能力检测结果。
        if (_sidebar != null)
            _sidebar.SidebarSupportChanged -= RefreshVisibility;
    }

    private void RefreshVisibility(bool supported)
    {
#if UNITY_EDITOR
        // 编辑器保留入口用于布局检查，不模拟真实平台成功。
        gameObject.SetActive(true);
#else
        gameObject.SetActive(supported);
#endif
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        MenuClickFeedback.PlayUiClickSound(this);
#if UNITY_EDITOR
        Debug.Log("[Sidebar] 编辑器仅预览入口；请在抖音开发者工具或真机测试侧边栏跳转。", this);
#else
        if (_sidebar != null)
            _sidebar.NavigateToSidebarFromUserClick();
#endif
    }
}
