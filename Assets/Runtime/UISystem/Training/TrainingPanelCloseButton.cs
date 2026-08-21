using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 训练面板关闭按钮的点击处理器：直接挂在关闭按钮对象上。
/// 同时实现 IPointerDownHandler 与 IPointerClickHandler——按下即关闭
/// （与建筑点击同机制，避免移动端手指滑动导致 Click 事件不触发）。
/// </summary>
[DisallowMultipleComponent]
public sealed class TrainingPanelCloseButton : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    [SerializeField] private TroopTrainingPanel panel;

    private void Awake()
    {
        if (!panel)
            panel = GetComponentInParent<TroopTrainingPanel>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (panel) panel.Close();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (panel) panel.Close();
    }
}
