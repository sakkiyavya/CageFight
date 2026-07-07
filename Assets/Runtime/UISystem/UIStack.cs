using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// UI 打开栈 — 全局单例
/// 负责维护当前打开的 UI 栈，检测点击空白处并执行出栈与关闭动画。
/// 入栈与打开动画由 UIOpenButton 负责；出栈与关闭动画由本类负责。
/// </summary>
public class UIStack : MonoBehaviour
{
    public static UIStack Instance { get; private set; }

    private readonly Stack<UISystemBase> _openStack = new Stack<UISystemBase>();

    // 用于空白处检测
    private GraphicRaycaster _raycaster;
    private EventSystem _eventSystem;

    // 帧标记：防止同帧 Push 后立即被空白处检测 Pop 掉
    private bool _pushedThisFrame;

    // ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 优先从父级 Canvas 获取 GraphicRaycaster，找不到则全场景搜索
        _raycaster = GetComponentInParent<GraphicRaycaster>();
        if (_raycaster == null)
            _raycaster = FindObjectOfType<GraphicRaycaster>();

        _eventSystem = EventSystem.current;

        if (_raycaster == null)
            Debug.LogWarning("[UIStack] 未找到 GraphicRaycaster，空白处检测将失效。请确保 Canvas 上已挂载该组件。");
    }

    private void Update()
    {
        // 消费帧标记，跳过本帧检测
        if (_pushedThisFrame) { _pushedThisFrame = false; return; }
        if (_openStack.Count == 0) return;
        if (_raycaster == null || _eventSystem == null) return;

        // 检测输入（同时兼容鼠标与触屏）
        bool inputBegan = Input.GetMouseButtonDown(0);
        Vector2 inputPos = Input.mousePosition;

#if UNITY_IOS || UNITY_ANDROID
        if (!inputBegan && Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            inputBegan = true;
            inputPos = Input.GetTouch(0).position;
        }
#endif

        if (!inputBegan) return;

        // 对输入位置做 UI 射线检测
        var pointerData = new PointerEventData(_eventSystem) { position = inputPos };
        var results = new List<RaycastResult>();
        _raycaster.Raycast(pointerData, results);

        // 检查是否命中了栈顶 UI 层级内的任意元素
        UISystemBase top = _openStack.Peek();
        if (top == null) { _openStack.Pop(); return; }

        foreach (var result in results)
        {
            if (result.gameObject != null &&
                result.gameObject.transform.IsChildOf(top.transform))
            {
                // 点击在 UI 内部，不关闭
                return;
            }
        }

        // 点击到空白处，关闭最上层 UI
        Pop();
    }

    // ─── 对外接口 ────────────────────────────────────────────

    /// <summary>
    /// 将 UI 压入栈（仅维护栈结构，打开动画由 UIOpenButton 负责）。
    /// </summary>
    public void Push(UISystemBase ui)
    {
        if (ui == null) return;
        _pushedThisFrame = true;
        _openStack.Push(ui);
    }

    /// <summary>
    /// 将栈顶 UI 弹出并播放关闭动画。
    /// </summary>
    public void Pop()
    {
        if (_openStack.Count == 0) return;
        UISystemBase top = _openStack.Pop();
        top?.UIMotionEffect(false);
    }

    /// <summary>
    /// 返回当前栈顶 UI，若栈空则返回 null。
    /// </summary>
    public UISystemBase Peek() => _openStack.Count > 0 ? _openStack.Peek() : null;

    /// <summary>
    /// 当前栈内 UI 数量。
    /// </summary>
    public int Count => _openStack.Count;
}
