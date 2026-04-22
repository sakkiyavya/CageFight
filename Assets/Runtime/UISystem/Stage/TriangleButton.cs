using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TriangleButton : UISystemBase, IPointerDownHandler, IPointerUpHandler
{

    // 处理按下事件。
    public void OnPointerDown(PointerEventData eventData)
    {
        UIMotionEffect(isOpen);
        isOpen = !isOpen;
    }
    // 处理抬起事件。
    public void OnPointerUp(PointerEventData eventData)
    {
        
    }
}
