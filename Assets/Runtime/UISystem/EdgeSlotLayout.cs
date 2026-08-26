using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 通用贴边自动布局：收集自身直接子物体中带指针交互接口
/// （IPointerDownHandler / IPointerClickHandler）的活动槽位，
/// 按子物体顺序从左到右、以模块自身右边缘（rect.width - slotEdgeInset）为基准
/// 靠边右对齐排列；槽位增删后自动重排。纯装饰子物体（无指针接口）不参与排列。
/// 仅遍历直接子物体（不做全局场景查找）、非逐帧调用、仅在值变化时写入布局属性。
/// </summary>
[DisallowMultipleComponent]
public sealed class EdgeSlotLayout : MonoBehaviour
{
    [SerializeField, Tooltip("栏位排的右边界（模块本地坐标 x，相对模块左边缘）；展开状态下该点即贴屏幕右缘的终点")]
    private float rowRightEdge = 1333.6667f;

    [SerializeField, Min(0f), Tooltip("相邻槽位的横向间距（像素）")]
    private float slotGap = 44f;

    [SerializeField, Tooltip("三角按钮相对排内位置的额外右移量（正值向右靠近图标排）")]
    private float triangleOffset = 20f;

    private readonly List<RectTransform> _slots = new List<RectTransform>();

    private void OnEnable()
    {
        LayoutSlots();
    }

    /// <summary>
    /// 按当前活动槽位重新贴边排列；右边界取模块本地固定值 rowRightEdge，
    /// 展开时即贴屏幕右缘的终点。面板整体展开/收起由模块 UISystemBase 起止配置驱动，
    /// 本布局只负责排内对齐。
    /// </summary>
    public void LayoutSlots()
    {
        CollectSlots();
        if (_slots.Count == 0)
            return;

        float cursor = rowRightEdge;

        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            RectTransform slot = _slots[i];
            if (slot == null)
                continue;

            float targetX = cursor - slot.rect.width;
            // 三角按钮额外右移，贴近图标排（仅三角有效，不影响其他槽位间距）。
            if (slot.GetComponent<TriangleButton>() != null)
                targetX += triangleOffset;

            Vector2 position = slot.anchoredPosition;
            // 仅在值发生变化时写入，避免无关刷新重写布局属性。
            if (!Mathf.Approximately(position.x, targetX))
                slot.anchoredPosition = new Vector2(targetX, position.y);

            cursor = targetX - slotGap;
        }
    }

    /// <summary>收集活动槽位：仅自身直接子物体中带指针交互接口的元素（转入转出三角作为排头元素一并参与）。</summary>
    private void CollectSlots()
    {
        _slots.Clear();
        foreach (Transform child in transform)
        {
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;

            bool interactive =
                child.GetComponent<IPointerDownHandler>() != null ||
                child.GetComponent<IPointerClickHandler>() != null;
            if (!interactive)
                continue;

            _slots.Add(child as RectTransform);
        }
    }

#if UNITY_EDITOR
    /// <summary>编辑器内即时预览布局；仅编辑器生效，不参与运行时逻辑。</summary>
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;
        LayoutSlots();
    }
#endif
}
