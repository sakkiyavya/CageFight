using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 升级面板（Upgrade Canvas）页签按钮：Build UP 打开建筑面板，Magic UP 切换魔法页面。
/// 走 EventSystem（IPointerDownHandler），与项目内其它 UI 按钮同一套点击约定；
/// 两个页签互斥：显示本页签对应页面时自动隐藏另一页。
/// </summary>
public class UpgradeTabButton : MonoBehaviour, IPointerDownHandler
{
    public enum UpgradeTab { Build = 0, Magic = 1 }

    [SerializeField] private UpgradeTab tab = UpgradeTab.Build;
    [SerializeField] private GameObject buildPanel;   // Build 面板容器（点击 Build UP 时显示）。
    [SerializeField] private GameObject magicPanel;   // Magic 页面容器（点击 Magic UP 时显示）。

    public void OnPointerDown(PointerEventData eventData)
    {
        Switch();
    }

    /// <summary>切换到本按钮对应的页面（另一页隐藏）。</summary>
    public void Switch()
    {
        if (buildPanel != null)
            buildPanel.SetActive(tab == UpgradeTab.Build);
        if (magicPanel != null)
            magicPanel.SetActive(tab == UpgradeTab.Magic);
    }
}
