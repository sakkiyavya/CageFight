using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 翻页按钮。isNext=true 翻到下一页，false 翻到上一页。
/// 边界处理由 LevelButtonLayout.TurnPage() 负责。
/// </summary>
public class LevelPageButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool isNext;
    [SerializeField] private LevelButtonLayout layout;

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (layout == null) { Debug.LogWarning("[LevelPageButton] layout 未配置！", this); return; }
        layout.TurnPage(isNext);
    }
}
