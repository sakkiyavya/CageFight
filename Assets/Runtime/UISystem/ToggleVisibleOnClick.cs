using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 点击时切换目标对象的显示状态；未指定目标时切换自身。
/// 适用于“点击切换显示与否”的按钮（如侧边栏入口/退出、难度选择面板）。
/// 按下时提供轻回弹缩放与 UI Click 音效（与全局点按反馈一致）。
/// </summary>
[DisallowMultipleComponent]
public sealed class ToggleVisibleOnClick : MonoBehaviour, IPointerDownHandler,
    IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField, Tooltip("要切换显示状态的目标；留空则切换自身。")]
    private GameObject target;

    [SerializeField, Range(0.5f, 1f), Tooltip("按下时的缩放谷值（0.88 = 缩小到 88%），提供点按反馈")]
    private float pressedScale = 0.88f;

    private Vector3 originalScale;
    private int pointerId;
    private bool pressing;
    private bool scaled;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || pressing)
            return;

        originalScale = transform.localScale;
        pointerId = eventData.pointerId;
        pressing = true;
        scaled = true;
        transform.localScale = originalScale * pressedScale;
        MenuClickFeedback.PlayUiClickSound(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressing || eventData.pointerId != pointerId ||
            eventData.button != PointerEventData.InputButton.Left) return;

        RestoreScale();
        pressing = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!pressing || eventData.pointerId != pointerId) return;

        RestoreScale();
    }

    private void RestoreScale()
    {
        if (!scaled) return;
        transform.localScale = originalScale;
        scaled = false;
    }

    private void OnDisable()
    {
        if (pressing)
        {
            RestoreScale();
            pressing = false;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        GameObject toggleTarget = target != null ? target : gameObject;
        toggleTarget.SetActive(!toggleTarget.activeSelf);
    }
}
