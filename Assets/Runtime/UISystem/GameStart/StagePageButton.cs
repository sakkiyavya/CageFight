using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Page button. isNext=true turns to the next page; false turns to the previous page.
/// </summary>
public class StagePageButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool isNext;
    [SerializeField] private StageConfigLoader loader;

    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (loader == null)
        {
            Debug.LogWarning("[StagePageButton] StageConfigLoader 未配置！", this);
            return;
        }

        loader.TurnPage(isNext);
    }
}
