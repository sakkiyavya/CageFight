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
    public UISystemBase targetUI;

    // 缓存自身 RectTransform，供子类或外部做视觉反馈使用
    private RectTransform _rectTransform;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    // ─── 指针事件 ─────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        // 预留：可在此处添加按压视觉反馈（缩放、颜色等）或音效
    }

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
}
