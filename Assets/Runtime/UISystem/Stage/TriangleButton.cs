using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TriangleButton : UISystemBase, IPointerDownHandler, IPointerUpHandler
{
    
    protected override void Awake()
    {
        base.Awake();
        isOpen = false;
    }

    #region 生命周期与回调
    /// <summary>
    /// 按下按钮：展开时先让父模块的栏位脚本重排（三角会排到图标排最左端），
    /// 并把该位置记为自己的 endPos，从把手位（startPos）动画滑到图标旁；
    /// 父模块经 subUI 联动整体滑出贴屏幕右缘。
    /// 收起时动画反向：三角滑回把手位，父模块经 subUI 联动整排藏入屏幕右侧外。
    /// </summary>
    /// <param name="eventData">本次按下事件的指针数据；当前实现不读取其中的具体值。</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (!isOpen)
        {
            // 展开：重排后三角位于图标排最左端，把该位置记为本体动画终点。
            RelayoutPanelSlots();
            Vector3 current = rectTransform.anchoredPosition3D;
            endPos = new Vector3(current.x, endPos.y, endPos.z);
        }

        UIMotionEffect(isOpen);
        isOpen = !isOpen;
    }

    /// <summary>触发父模块的栏位重排：优先 EdgeSlotLayout，其次 GameplaySpellBar。</summary>
    private void RelayoutPanelSlots()
    {
        if (transform.parent == null)
            return;

        EdgeSlotLayout edgeLayout = transform.parent.GetComponent<EdgeSlotLayout>();
        if (edgeLayout != null)
        {
            edgeLayout.LayoutSlots();
            return;
        }

        GameplaySpellBar spellBar = transform.parent.GetComponent<GameplaySpellBar>();
        if (spellBar != null)
            spellBar.LayoutSlots();
    }
    /// <summary>
    /// 接收指针抬起回调；当前按钮的状态切换已在按下时完成，因此此处不执行额外逻辑。
    /// </summary>
    /// <param name="eventData">本次抬起事件的指针数据。</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        
    }
    #endregion
}
