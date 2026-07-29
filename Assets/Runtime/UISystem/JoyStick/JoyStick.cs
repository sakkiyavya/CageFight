using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class JoyStick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    // 单例模式
    public static JoyStick Instance { get; private set; }

    [Header("摇杆组件设置")]
    public RectTransform background;                                                       // 底盘
    public RectTransform handle;                                                           // 摇杆柄

    [Header("摇杆参数")]
    [Tooltip("摇杆柄可移动的最大半径范围")]
    public float maxRadius = 100f;                                                         // 摇杆柄相对底盘中心允许移动的最大距离。

    // 摇杆移动委托
    public Action<Vector2> OnJoystickMove;                                                 // 摇杆方向变化时触发的回调。
    private Vector2 inputDir = Vector2.zero;                                               // 当前归一化输入方向。
    public Vector2 InputDir => inputDir;                                                   // 提供给移动逻辑读取的当前方向。

    private Vector2 inputVector;                                                           // 摇杆柄位置映射到单位半径后的输入量。
    private bool isDragging = false;                                                       // 当前是否正在由已绑定手指拖动。

    // 手指绑定处理器
    private FingerIDHander fingerHandler = new FingerIDHander();                           // 防止多指输入互相抢占的手指绑定器。

    #region 生命周期与回调
    /// <summary>
    /// 建立摇杆单例；场景中存在重复摇杆时销毁后创建的对象。
    /// </summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// 尝试占用当前指针；成功后进入拖动状态，并立即按按下位置更新一次摇杆输入。
    /// </summary>
    /// <param name="eventData">包含指针编号和屏幕位置的按下事件数据。</param>
    public void OnPointerDown(PointerEventData eventData)
    {
        // 尝试绑定当前点击的手指 ID
        if (fingerHandler.TryBind(eventData.pointerId))
        {
            isDragging = true;
            OnDrag(eventData);
        }
    }

    /// <summary>
    /// 将已绑定指针的屏幕位置转换到底盘局部坐标，限制摇杆柄半径并发布归一化方向。
    /// 非当前绑定手指产生的拖动会被忽略。
    /// </summary>
    /// <param name="eventData">包含拖动指针编号、屏幕位置和事件摄像机的数据。</param>
    public void OnDrag(PointerEventData eventData)
    {
        // 校验是否为绑定的那根手指，防止其他手指干扰
        if (!isDragging || !fingerHandler.IsValid(eventData.pointerId)) return;

        Vector2 localPosition;                                                             // 指针相对摇杆底盘的局部坐标。
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out localPosition))
        {
            Vector2 clampedPosition = Vector2.ClampMagnitude(localPosition, maxRadius);    // 限制在最大半径内的摇杆柄位置。
            handle.anchoredPosition = clampedPosition;

            inputVector = clampedPosition / maxRadius;
            inputDir = inputVector.normalized;
            
            OnJoystickMove?.Invoke(inputDir);
        }
    }

    /// <summary>
    /// 在绑定手指抬起时释放全局占用，将摇杆柄和输入方向复位，并发布零方向。
    /// </summary>
    /// <param name="eventData">包含抬起指针编号的事件数据。</param>
    public void OnPointerUp(PointerEventData eventData)
    {
        // 校验并释放绑定
        if (fingerHandler.IsValid(eventData.pointerId))
        {
            isDragging = false;
            fingerHandler.Unbind(); // 释放全局和本地绑定

            inputVector = Vector2.zero;
            inputDir = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
            
            OnJoystickMove?.Invoke(inputDir);
        }
    }
    #endregion
}
