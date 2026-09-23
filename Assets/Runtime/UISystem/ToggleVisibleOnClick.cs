using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 点击时切换目标对象的显示状态；未指定目标时切换自身。
/// 适用于“点击切换显示与否”的按钮（如难度选择面板）。
/// </summary>
[DisallowMultipleComponent]
public sealed class ToggleVisibleOnClick : MonoBehaviour, IPointerClickHandler
{
    [SerializeField, Tooltip("要切换显示状态的目标；留空则切换自身。")]
    private GameObject target;

    public void OnPointerClick(PointerEventData eventData)
    {
        GameObject toggleTarget = target != null ? target : gameObject;
        toggleTarget.SetActive(!toggleTarget.activeSelf);
    }
}