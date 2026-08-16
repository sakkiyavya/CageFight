using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>预置候选按钮，仅保存稳定 ID 和显示引用，不直接持有定义资源。</summary>
[DisallowMultipleComponent]
public sealed class LoadoutSelectionOption : MonoBehaviour, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private LoadoutSelectionPanel panel;
    [SerializeField] private string definitionId;
    [SerializeField] private Image icon;
    [SerializeField] private Image portraitFrame;
    [SerializeField] private Graphic selectedMarker;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField, Range(0f, 1f)] private float unavailableAlpha = .35f;
    [SerializeField] private Vector2 checkOffset = new Vector2(18f, -18f);
    [SerializeField] private float holdTime = .5f;

    private bool markerLayoutCached;
    private Vector2 markerOffsetMin;
    private Vector2 markerOffsetMax;
    private float pressedAt;
    private bool pressing;
    private bool suppressClick;

    private void Awake()
    {
        if (icon) icon.preserveAspect = true;
        if (portraitFrame) portraitFrame.preserveAspect = true;
    }

    /// <summary>此按钮对应的定义稳定 ID。</summary>
    public string DefinitionId => definitionId;

    public void SetDefinitionId(string value) => definitionId = value ?? string.Empty;

    /// <summary>由所属面板统一刷新可用、选中和图标状态。</summary>
    public void SetPresentation(Sprite sprite, Sprite frame, bool selected, bool available, Sprite checkSprite, float unselectedAlpha)
    {
        if (icon)
        {
            icon.preserveAspect = true;
            icon.sprite = sprite;
            icon.color = sprite ? Color.white : Color.clear;
        }
        if (portraitFrame)
        {
            portraitFrame.preserveAspect = true;
            portraitFrame.sprite = frame;
            portraitFrame.color = frame ? Color.white : Color.clear;
        }

        if (selectedMarker)
        {
            Image check = selectedMarker as Image;
            if (check)
            {
                check.sprite = checkSprite;
                Color color = check.color;
                color.a = 1f;
                check.color = color;
                ApplyCheckOffset(check.rectTransform);
            }
            selectedMarker.enabled = selected && checkSprite;
        }
        if (!canvasGroup) return;

        canvasGroup.alpha = available ? (selected ? 1f : unselectedAlpha) : unavailableAlpha;
        canvasGroup.interactable = available;
        canvasGroup.blocksRaycasts = available;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (suppressClick)
        {
            suppressClick = false;
            return;
        }
        if (panel) panel.Select(definitionId);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressing = true;
        pressedAt = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData) => pressing = false;

    private void Update()
    {
        if (!pressing || Time.unscaledTime - pressedAt < holdTime) return;
        pressing = false;
        suppressClick = true;
        panel?.PreviewEngineer(definitionId);
    }

    private void ApplyCheckOffset(RectTransform marker)
    {
        if (!markerLayoutCached)
        {
            markerLayoutCached = true;
            markerOffsetMin = marker.offsetMin;
            markerOffsetMax = marker.offsetMax;
        }
        marker.offsetMin = markerOffsetMin + checkOffset;
        marker.offsetMax = markerOffsetMax + checkOffset;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (portraitFrame || !panel || panel.Kind != LoadoutSelectionKind.Engineer) return;
        GameObject frame = new GameObject("PortraitFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        frame.transform.SetParent(transform, false);
        frame.transform.SetSiblingIndex(0);
        RectTransform rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        portraitFrame = frame.GetComponent<Image>();
        portraitFrame.raycastTarget = false;
    }
#endif
}
