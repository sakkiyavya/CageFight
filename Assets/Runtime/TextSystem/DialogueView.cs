using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 直接挂在 Canvas 子节点上的对话框显示组件。
/// 自身 RectTransform 同时负责对话框位移和 UI 射线拦截。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class DialogueView : Graphic, IPointerClickHandler
{
    [Header("内容引用")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text dialogueText;

    [Header("固定进退场动画")]
    [SerializeField, Min(0f)] private float enterDuration = 0.25f;
    [SerializeField, Min(0f)] private float exitDuration = 0.2f;
    [SerializeField] private Vector2 enterOffset = new Vector2(-160f, 0f);
    [SerializeField] private Vector2 exitOffset = new Vector2(160f, 0f);

    private Vector2 _shownPosition;
    private bool _initialized;
    private bool _acceptInput;

    public event Action AdvanceRequested;

    protected override void Awake()
    {
        // Graphic 的射线参与依赖 CanvasRenderer。RequireComponent 会为新建对象添加，
        // 这里同时兼容添加该特性之前已存在的场景对象。
        if (!TryGetComponent<CanvasRenderer>(out _))
            gameObject.AddComponent<CanvasRenderer>();

        base.Awake();
        InitializeIfNeeded();
        SnapHidden();
    }

    protected override void OnEnable()
    {
        base.OnEnable();

        // Graphic 会在编辑器模式下执行启用回调，不应因此改写场景的激活状态。
        if (!Application.isPlaying)
            return;

        DialogueManager manager = DialogueManager.Instance;
        if (manager != null && manager.ShouldKeepViewEnabled(this))
            return;

        SetInputBlocked(false);
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 检查 View 所需引用是否完整。
    /// </summary>
    public bool TryValidate(out string error)
    {
        InitializeIfNeeded();

        if (portraitImage == null)
            error = "Portrait Image 未配置";
        else if (dialogueText == null)
            error = "Dialogue TMP_Text 未配置";
        else if (GetComponentInParent<Canvas>(true) == null)
            error = "DialogueView 必须位于 Canvas 层级下";
        else
        {
            error = null;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 将人物立绘和正文写入 View。
    /// </summary>
    public void Bind(Sprite portrait, string text)
    {
        InitializeIfNeeded();

        if (portraitImage != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = portrait != null;
        }

        if (dialogueText != null)
            dialogueText.text = text ?? string.Empty;
    }

    /// <summary>
    /// 清除对已预加载人物资源的引用。
    /// </summary>
    public void ClearContent()
    {
        if (portraitImage != null)
        {
            portraitImage.sprite = null;
            portraitImage.enabled = false;
        }

        if (dialogueText != null)
            dialogueText.text = string.Empty;
    }

    /// <summary>
    /// 播放固定进场动画。动画始终使用不受 timeScale 影响的时间。
    /// </summary>
    public IEnumerator PlayEnter()
    {
        InitializeIfNeeded();
        SetInputBlocked(true);

        Vector2 from = _shownPosition + enterOffset;
        SetPosition(from);
        yield return Animate(from, _shownPosition, enterDuration);
    }

    /// <summary>
    /// 播放固定退场动画。动画始终使用不受 timeScale 影响的时间。
    /// </summary>
    public IEnumerator PlayExit()
    {
        InitializeIfNeeded();
        SetInputBlocked(true);

        Vector2 from = rectTransform.anchoredPosition;
        yield return Animate(from, _shownPosition + exitOffset, exitDuration);
    }

    /// <summary>
    /// 立即恢复隐藏状态，不播放动画。
    /// </summary>
    public void SnapHidden()
    {
        InitializeIfNeeded();
        SetPosition(_shownPosition + enterOffset);
        SetInputBlocked(false);
    }

    /// <summary>
    /// 开关对底层 UI 和游戏指针事件的射线拦截。
    /// </summary>
    public void SetInputBlocked(bool blocked)
    {
        _acceptInput = blocked;
        raycastTarget = blocked;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_acceptInput &&
            (eventData == null || eventData.button == PointerEventData.InputButton.Left))
        {
            eventData?.Use();
            AdvanceRequested?.Invoke();
        }
    }

    private void InitializeIfNeeded()
    {
        if (_initialized)
            return;

        _shownPosition = rectTransform.anchoredPosition;

        _initialized = true;
    }

    private IEnumerator Animate(
        Vector2 fromPosition,
        Vector2 toPosition,
        float duration)
    {
        if (duration <= 0f)
        {
            SetPosition(toPosition);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            SetPosition(Vector2.LerpUnclamped(fromPosition, toPosition, eased));
            yield return null;
        }

        SetPosition(toPosition);
    }

    private void SetPosition(Vector2 position)
    {
        rectTransform.anchoredPosition = position;
    }

    /// <summary>
    /// DialogueView 只参与 UI 射线检测，不绘制任何图形。
    /// </summary>
    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
    }
}
