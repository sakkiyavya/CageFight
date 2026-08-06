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
    /// 按下按钮时按当前开关状态播放界面展开或收起动画，并翻转下一次切换方向。
    /// </summary>
    /// <param name="eventData">本次按下事件的指针数据；当前实现不读取其中的具体值。</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        UIMotionEffect(isOpen);
        isOpen = !isOpen;
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
