using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 菜单页面互斥：挂在各页面根（Canvas）上。页面被打开（SetActive(true)）时，
/// 自动关闭其它已登记的页面，保证同一时刻最多只存在一个页面（含侧边栏 Cebianlan）。
/// 打开/关闭均为即时切换，不含动画。
/// </summary>
[DisallowMultipleComponent]
public sealed class PanelExclusive : MonoBehaviour
{
    private static readonly List<PanelExclusive> OpenPanels = new List<PanelExclusive>();

    private void OnEnable()
    {
        // 先关闭其它已打开的页面（反向遍历，安全处理被关闭页面触发的 OnDisable 移除）。
        for (int i = OpenPanels.Count - 1; i >= 0; i--)
        {
            PanelExclusive other = OpenPanels[i];
            if (other != null && other != this && other.gameObject.activeInHierarchy)
                other.gameObject.SetActive(false);
        }

        if (!OpenPanels.Contains(this))
            OpenPanels.Add(this);
    }

    private void OnDisable()
    {
        OpenPanels.Remove(this);
    }
}
