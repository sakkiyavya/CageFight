using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class TriangleButton : UISystemBase, IPointerDownHandler, IPointerUpHandler
{

    public void OnPointerDown(PointerEventData eventData)
    {
        UIMotionEffect(isOpen);
        isOpen = !isOpen;
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        
    }
}
