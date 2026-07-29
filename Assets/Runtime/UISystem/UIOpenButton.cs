using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// UI 打开按钮
/// 挂载在需要触发打开 UI 的按钮对象上，暴露目标 UISystemBase 字段。
/// 通过 IPointerDownHandler / IPointerUpHandler 检测点击；
/// 抬起时将目标 UI 压入 UIStack 并调用 UIMotionEffect(true)。
/// </summary>
public class UIOpenButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Tooltip("点击此按钮后要打开的 UI")]
    public UISystemBase targetUI;            // 点击抬起后需要打开的目标界面。

    // 缓存自身 RectTransform，供子类或外部做视觉反馈使用
    private RectTransform _rectTransform;    // 当前按钮的矩形变换组件。

    #region 生命周期与回调
    /// <summary>
    /// 缓存按钮自身的矩形变换，供按压反馈或扩展逻辑使用。
    /// </summary>
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    // ─── 指针事件 ─────────────────────────────────────────────

    /// <summary>
    /// 接收按钮按下事件；当前实现仅保留扩展入口，不改变界面状态。
    /// </summary>
    /// <param name="eventData">本次按下事件的指针位置、按键和摄像机信息。</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        // 预留：可在此处添加按压视觉反馈（缩放、颜色等）或音效
    }

    /// <summary>
    /// 校验目标界面和 UI 栈后，将目标界面入栈并播放打开动画。
    /// </summary>
    /// <param name="eventData">本次抬起事件的指针数据；当前实现不读取其中的具体值。</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (targetUI == null)
        {
            Debug.LogWarning("[UIOpenButton] targetUI 未配置，请在 Inspector 中指定目标 UI。", this);
            return;
        }

        if (UIStack.Instance == null)
        {
            Debug.LogWarning("[UIOpenButton] UIStack 单例未就绪，请确保场景中已放置 UIStack 对象。", this);
            return;
        }

        // 入栈（UIStack 仅维护结构）并立即触发打开动画
        UIStack.Instance.Push(targetUI);
        targetUI.UIMotionEffect(true);
    }
    #endregion
}
