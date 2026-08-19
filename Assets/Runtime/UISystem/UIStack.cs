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

    private readonly Stack<UISystemBase> _openStack = new Stack<UISystemBase>();    // 按打开顺序保存的界面栈。

    // 用于空白处检测
    private GraphicRaycaster _raycaster;                                            // 用于判断点击是否命中 UI 的射线检测器。
    private EventSystem _eventSystem;                                               // 创建指针射线数据所需的事件系统。

    // 帧标记：防止同帧 Push 后立即被空白处检测 Pop 掉
    private bool _pushedThisFrame;                                                  // 本帧是否刚压入界面，用于避免同帧误关闭。

    // ─────────────────────────────────────────────────────────

    #region 生命周期与回调
    /// <summary>
    /// 建立 UI 栈单例，并缓存用于空白区域点击检测的射线检测器和事件系统。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // 优先从父级 Canvas 获取 GraphicRaycaster，其次从事件系统层级获取；均找不到时警告。
        _raycaster = GetComponentInParent<GraphicRaycaster>();
        _eventSystem = EventSystem.current;
        if (_raycaster == null && _eventSystem != null)
            _raycaster = _eventSystem.GetComponentInParent<GraphicRaycaster>();

        if (_raycaster == null)
            Debug.LogWarning("[UIStack] 未找到 GraphicRaycaster，空白处检测将失效。请确保 Canvas 上已挂载该组件。");
    }

    /// <summary>
    /// 监听鼠标或触摸按下事件；若点击位置未命中栈顶界面及其子对象，则关闭栈顶界面。
    /// 刚完成入栈的同一帧会跳过检测，避免打开操作立即触发关闭。
    /// </summary>
    private void Update()
    {
        // 消费帧标记，跳过本帧检测
        if (_pushedThisFrame) { _pushedThisFrame = false; return; }
        if (_openStack.Count == 0) return;
        if (_raycaster == null || _eventSystem == null) return;

        // 检测输入（同时兼容鼠标与触屏）
        bool inputBegan = Input.GetMouseButtonDown(0);                              // 本帧是否开始了点击输入。
        Vector2 inputPos = Input.mousePosition;                                     // 当前输入的屏幕坐标。

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
        UISystemBase top = _openStack.Peek();                                       // 当前最上层的打开界面。
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
    #endregion

    // ─── 对外接口 ────────────────────────────────────────────

    #region 公开接口
    /// <summary>
    /// 将界面压入打开栈，并标记本帧跳过空白区域关闭检测。
    /// 本方法只维护栈结构，不负责播放打开动画。
    /// </summary>
    /// <param name="ui">刚被打开、需要纳入关闭顺序管理的界面。</param>
    public void Push(UISystemBase ui)
    {
        if (ui == null) return;
        _pushedThisFrame = true;
        _openStack.Push(ui);
    }

    /// <summary>
    /// 从栈中移除最上层界面，并请求其播放关闭动画。
    /// </summary>
    public void Pop()
    {
        if (_openStack.Count == 0) return;
        UISystemBase top = _openStack.Pop();                                        // 需要关闭的最上层界面。
        top?.UIMotionEffect(false);
    }

    /// <summary>
    /// 获取当前最上层的打开界面，但不修改栈内容。
    /// </summary>
    /// <returns>栈顶界面；栈为空时返回 <see langword="null"/>。</returns>
    public UISystemBase Peek() => _openStack.Count > 0 ? _openStack.Peek() : null;

    /// <summary>
    /// 获取当前由栈管理的打开界面数量。
    /// </summary>
    public int Count => _openStack.Count;                                           // 当前打开界面数量。
    #endregion
}
