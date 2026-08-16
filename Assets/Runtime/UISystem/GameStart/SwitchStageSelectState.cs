using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 关卡选择入口按钮。
/// 将本组件挂到可接收 UI 射线的 Image/Graphic 对象上，点击后进入 StageSelectState。
/// </summary>
public class SwitchStageSelectState : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    #region 生命周期与回调
    /// <summary>
    /// 接收入口按钮按下事件；状态切换延迟到抬起时执行，以保持完整点击语义。
    /// </summary>
    /// <param name="eventData">本次按下事件的指针数据。</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        // 保持与 LevelButton 等 UI 点击脚本一致，按下阶段暂不执行状态切换。
    }

    /// <summary>
    /// 指针抬起时校验场景状态机，并请求从主菜单进入关卡选择状态。
    /// </summary>
    /// <param name="eventData">本次抬起事件的指针数据；当前实现不读取其中的具体值。</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (SceneFSM.Instance == null)
        {
            Debug.LogWarning("[SwitchStageSelectState] SceneFSM 尚未初始化，无法进入关卡选择状态。", this);
            return;
        }

        MenuAmbientAudio.NotifyMenuBegin();
        SceneFSM.Instance.OpenStageSelect();
    }
    #endregion
}
