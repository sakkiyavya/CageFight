using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 关卡选择入口按钮。
/// 将本组件挂到可接收 UI 射线的 Image/Graphic 对象上，点击后进入 StageSelectState。
/// </summary>
public class SwitchStageSelectState : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public void OnPointerDown(PointerEventData eventData)
    {
        // 保持与 LevelButton 等 UI 点击脚本一致，按下阶段暂不执行状态切换。
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (SceneFSM.Instance == null)
        {
            Debug.LogWarning("[SwitchStageSelectState] SceneFSM 尚未初始化，无法进入关卡选择状态。", this);
            return;
        }

        SceneFSM.Instance.OpenStageSelect();
    }
}
