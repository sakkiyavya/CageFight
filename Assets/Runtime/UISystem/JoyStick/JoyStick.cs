using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class JoyStick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    // 单例模式
    public static JoyStick Instance { get; private set; }

    [Header("摇杆组件设置")]
    public RectTransform background; // 底盘
    public RectTransform handle;     // 摇杆柄

    [Header("摇杆参数")]
    [Tooltip("摇杆柄可移动的最大半径范围")]
    public float maxRadius = 100f;

    // 摇杆移动委托
    public Action<Vector2> OnJoystickMove;
    private Vector2 inputDir = Vector2.zero;
    public Vector2 InputDir => inputDir;

    private Vector2 inputVector;
    private bool isDragging = false; 

    // 手指绑定处理器
    private FingerIDHander fingerHandler = new FingerIDHander();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 尝试绑定当前点击的手指 ID
        if (fingerHandler.TryBind(eventData.pointerId))
        {
            isDragging = true;
            OnDrag(eventData);
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 校验是否为绑定的那根手指，防止其他手指干扰
        if (!isDragging || !fingerHandler.IsValid(eventData.pointerId)) return;

        Vector2 localPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out localPosition))
        {
            Vector2 clampedPosition = Vector2.ClampMagnitude(localPosition, maxRadius);
            handle.anchoredPosition = clampedPosition;

            inputVector = clampedPosition / maxRadius;
            inputDir = inputVector.normalized;
            
            OnJoystickMove?.Invoke(inputDir);
        }
    }

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
}
