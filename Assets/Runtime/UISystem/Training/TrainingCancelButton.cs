using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 训练面板"取消出兵"按钮的点击处理器：直接挂在取消按钮对象上（与关闭按钮同机制）。
/// 按下即取消当前兵营训练（清除训练状态，维护费随训练取消一并停止），并关闭面板。
/// </summary>
[DisallowMultipleComponent]
public sealed class TrainingCancelButton : MonoBehaviour, IPointerDownHandler, IPointerClickHandler
{
    [SerializeField] private TroopTrainingPanel panel;

    private void Awake()
    {
        if (!panel)
            panel = GetComponentInParent<TroopTrainingPanel>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        CancelTraining();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        CancelTraining();
    }

    private void CancelTraining()
    {
        BuildingTraining building = TroopTrainingPanel.ActiveBuilding;
        if (building)
            building.CancelTraining();

        if (panel)
            panel.Close();
    }
}
