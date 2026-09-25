using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 关卡选择面板的“选中”视觉（共享式，不逐个关卡接线）：
/// 订阅 StageButton.Selected 事件：
///   1) 共享标记页（Level selection lcon AP_1，经 SpriteRegistry 键加载）弹动出场，
///      悬于选中关卡的正头上方（可见图像水平居中，底部与关卡头顶保持 markerGap 间距）；
///   2) 共享黄光图 = 选中图标同素材放大 glowScale 倍的半透明黄色覆盖，形成图标边缘黄光。
/// 挂在 StageSelectCanvas 上；标记与黄光为 Canvas 的两个共享 Image 子对象。
/// </summary>
[DisallowMultipleComponent]
public sealed class StageSelectSelection : MonoBehaviour
{
    [SerializeField] private Image markerImage;                              // 共享标记页。
    [SerializeField] private Image glowImage;                                // 共享黄光位。
    [SerializeField] private string markerSpriteKey = "Level selection lcon AP_1";
    [SerializeField] private Color glowColor = new Color(1f, 0.84f, 0.32f, 0.55f);
    [SerializeField] private float glowScale = 1.12f;                        // 黄光相对图标的大小倍数。
    [SerializeField] private Vector2 markerSize = new Vector2(420f, 420f);   // 标记页尺寸（在原 300 基础上再扩大 1.4 倍）。
    [SerializeField] private float markerGap = 10f;                          // 标记页底部与关卡头顶的间距。
    [SerializeField] private Image[] centerSlots;                            // 标记页正中间的展示位（最多 4 个，1-4 个图标居中横排）。
    [SerializeField] private float centerIconOffsetY = 20f;                  // 展示位相对弹窗中心的向上偏移。

    private StageButton _selected;
    private bool _markerReady;
    private Sprite _markerSprite;
    private RectTransform _canvasRect;
    private Coroutine _markerBounce;
    private Vector2 _markerTargetPos;                                       // 标记页目标位置（弹动动画逐帧跟随的最新值）。

    private void Awake()
    {
        _canvasRect = (RectTransform)transform;
        StageButton.Selected += OnStageSelected;

        if (markerImage != null) markerImage.enabled = false;
        if (glowImage != null) glowImage.enabled = false;
    }

    private void OnEnable()
    {
        // 防御：编辑器里若把面板根缩成 0（收纳面板的常见操作），打开时强制恢复 1，
        // 否则整个关卡选择界面会被压成一个点、看不见也点不到。
        transform.localScale = Vector3.one;

        // 每次进入关卡选择界面先清空选中视觉：标记与黄光只在选中关卡后出现，
        // 不会残留在主菜单/其它界面（SelectMark/SelectGlow 的场景默认状态也是隐藏）。
        ClearSelection();

        // 提前加载标记素材：素材异步到位前，首次点击只能用“素材填满整个矩形”的近似尺寸摆放，
        // 会把标记页摆得比真实位置偏上；预加载让首次点击即按真实内容尺寸定位。
        TryLoadMarkerSprite();
    }

    private void OnDestroy()
    {
        StageButton.Selected -= OnStageSelected;
    }

    private void OnDisable()
    {
        ClearSelection();
    }

    private void OnStageSelected(StageButton button)
    {
        if (button == null || _selected == button)
            return;

        _selected = button;
        RefreshVisuals();
    }

    private void Update()
    {
        // 状态机一离开关卡选择（GO 进入加载/局内、X 返回主菜单等）立即隐藏标记与黄光：
        // StageSelectCanvas 常驻激活、不随 FSM 关闭，否则选中视觉会残留在战斗/主菜单界面。
        if (_selected == null)
            return;

        SceneFSM fsm = SceneFSM.Instance;
        if (fsm != null && fsm.CurrentStateEnum != GameState.StageSelect)
            ClearSelection();
    }

    private void RefreshVisuals()
    {
        if (_selected == null || _canvasRect == null)
            return;

        RectTransform buttonRt = _selected.ButtonRect;
        if (buttonRt == null)
            return;

        // 可见图标：优先 StageButton.icon（运行时由关卡配置赋值），
        // 未初始化时回落使用关卡对象自身的 Image（场景里实际显示的图标）。
        Image visibleIcon = _selected.GetComponent<Image>();
        Sprite iconSprite = null;
        if (_selected.icon != null && _selected.icon.sprite != null)
            iconSprite = _selected.icon.sprite;
        if (iconSprite == null && visibleIcon != null)
            iconSprite = visibleIcon.sprite;

        // 图标中心与尺寸：以关卡对象自身矩形为准。
        Vector2 centerPos = GetCanvasPosition(buttonRt);
        Vector2 iconSize = buttonRt.sizeDelta;

        if (markerImage != null)
        {
            markerImage.enabled = true;
            markerImage.rectTransform.sizeDelta = markerSize;
            PositionMarker(buttonRt, centerPos);
            PlayMarkerBounce();
            UpdateCenterIcons();

            TryLoadMarkerSprite();   // 兜底：OnEnable 时资源管理器尚未就绪的首次点击。
        }

        if (glowImage != null && iconSprite != null)
        {
            glowImage.enabled = true;
            glowImage.sprite = iconSprite;      // 与图标同素材。
            glowImage.color = glowColor;        // 半透明黄 → 中心泛金、外圈呈黄光边缘。
            glowImage.rectTransform.anchoredPosition = centerPos;
            glowImage.rectTransform.sizeDelta = iconSize * glowScale;
        }
        else if (glowImage != null)
        {
            glowImage.enabled = false;
        }
    }

    /// <summary>
    /// 把标记页摆到选中关卡的正头上方。
    /// UI Image 的 Simple 模式忽略 Sprite 自身锚点、始终把图像居中画在矩形中心，
    /// 因此直接按“可见图像（去掉透明留白）的中心”对齐关卡中心，
    /// 底部与关卡头顶保持 markerGap 的间距。
    /// </summary>
    private void PositionMarker(RectTransform buttonRt, Vector2 centerPos)
    {
        RectTransform rt = markerImage.rectTransform;

        float visHalfW = markerSize.x * 0.5f;
        float visHalfH = markerSize.y * 0.5f;
        Vector2 visOffset = Vector2.zero;   // 可见图像中心相对矩形中心的偏移（透明留白造成）。

        if (_markerSprite != null)
        {
            Vector2 full = _markerSprite.rect.size;
            if (full.x > 0f && full.y > 0f)
            {
                Vector4 pad = UnityEngine.Sprites.DataUtility.GetPadding(_markerSprite);
                float aspect = full.x / full.y;
                float boxAspect = markerSize.x / markerSize.y;
                float drawnW, drawnH;
                if (aspect > boxAspect) { drawnW = markerSize.x; drawnH = drawnW / aspect; }
                else { drawnH = markerSize.y; drawnW = drawnH * aspect; }
                visHalfW = drawnW * (1f - (pad.x + pad.z) / full.x) * 0.5f;
                visHalfH = drawnH * (1f - (pad.y + pad.w) / full.y) * 0.5f;
                visOffset = new Vector2(
                    drawnW * (pad.x - pad.z) / full.x * 0.5f,
                    drawnH * (pad.y - pad.w) / full.y * 0.5f);
            }
        }

        // 关卡头顶 = 按钮上边缘（含按钮自身缩放）。
        float scaleY = Mathf.Abs(buttonRt.lossyScale.y);
        if (scaleY < 0.0001f) scaleY = 1f;
        float headY = centerPos.y + buttonRt.sizeDelta.y * 0.5f * scaleY;

        _markerTargetPos = new Vector2(
            centerPos.x - visOffset.x,
            headY + markerGap + visHalfH - visOffset.y);
        rt.anchoredPosition = _markerTargetPos;
    }

    /// <summary>
    /// 加载标记页素材（幂等）：到位后写入素材并按真实内容尺寸重新定位。
    /// OnEnable 时预加载一次，避免首次点击用近似尺寸摆放导致位置偏上。
    /// </summary>
    private void TryLoadMarkerSprite()
    {
        if (_markerReady || markerImage == null || string.IsNullOrEmpty(markerSpriteKey) ||
            ResourceManager.Instance == null)
            return;

        ResourceManager.Instance.LoadExtraResourceAsync<Sprite>(markerSpriteKey, sprite =>
        {
            if (sprite == null || markerImage == null)
                return;

            _markerSprite = sprite;
            markerImage.sprite = sprite;
            _markerReady = true;
            RefreshMarkerPosition();   // 素材到位后按真实内容尺寸重新定位。
        });
    }

    /// <summary>按当前选中关卡重新摆放标记页（素材异步加载完成后调用）。</summary>
    private void RefreshMarkerPosition()
    {
        if (markerImage == null || _selected == null || _canvasRect == null)
            return;

        RectTransform buttonRt = _selected.ButtonRect;
        if (buttonRt != null)
            PositionMarker(buttonRt, GetCanvasPosition(buttonRt));
    }

    /// <summary>
    /// 把选中关卡的展示图（StageConfig.displayIcons，最多 4 个）填进标记页正中间的展示位：
    /// 1-4 个图标按从左到右居中横排在弹窗正中间，多余的展示位隐藏。
    /// </summary>
    private void UpdateCenterIcons()
    {
        if (centerSlots == null || centerSlots.Length == 0)
            return;

        StageConfig config = _selected != null ? _selected.Config : null;
        var icons = new System.Collections.Generic.List<Sprite>();
        if (config != null && config.displayIcons != null)
        {
            for (int i = 0; i < config.displayIcons.Count && icons.Count < centerSlots.Length; i++)
            {
                if (config.displayIcons[i] != null)
                    icons.Add(config.displayIcons[i]);
            }
        }

        const float spacing = 8f;
        int count = icons.Count;
        for (int i = 0; i < centerSlots.Length; i++)
        {
            Image slot = centerSlots[i];
            if (slot == null)
                continue;

            bool show = i < count;
            slot.gameObject.SetActive(show);
            if (!show)
                continue;

            slot.sprite = icons[i];
            float slotSize = slot.rectTransform.sizeDelta.x;
            float x = (i - (count - 1) * 0.5f) * (slotSize + spacing);
            slot.rectTransform.anchoredPosition = new Vector2(x, centerIconOffsetY);
        }
    }

    /// <summary>标记页弹动出场：从 0.5 倍缩放回弹过冲到 1 倍，并自下方 36px 上浮到位。</summary>
    private void PlayMarkerBounce()
    {
        if (markerImage == null)
            return;

        if (_markerBounce != null)
            StopCoroutine(_markerBounce);
        _markerBounce = StartCoroutine(MarkerBounceRoutine());
    }

    private IEnumerator MarkerBounceRoutine()
    {
        RectTransform rt = markerImage.rectTransform;
        const float duration = 0.45f;
        const float rise = 36f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float back = EaseOutBack(p);
            rt.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, back);
            // 逐帧跟随最新目标位置：若素材在弹动中途异步到位，PositionMarker 会更新目标，
            // 弹动过程与结束都收敛到最新定位，不再停留在首次点击的偏上旧位置。
            rt.anchoredPosition = _markerTargetPos + new Vector2(0f, -rise * (1f - p));
            yield return null;
        }

        rt.localScale = Vector3.one;
        rt.anchoredPosition = _markerTargetPos;
        _markerBounce = null;
    }

    /// <summary>带回弹过冲的缓出曲线：0→1，途中短暂超过 1（约 1.1）再回落。</summary>
    private static float EaseOutBack(float p)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(p - 1f, 3f) + c1 * Mathf.Pow(p - 1f, 2f);
    }

    /// <summary>清除选中：隐藏标记与黄光。</summary>
    public void ClearSelection()
    {
        _selected = null;
        if (_markerBounce != null)
        {
            StopCoroutine(_markerBounce);
            _markerBounce = null;
        }
        if (markerImage != null) markerImage.enabled = false;
        if (glowImage != null) glowImage.enabled = false;

        // 标记页隐藏时，其子级展示位也必须一起隐藏（父 Image 的 enabled 不影响子对象渲染）。
        if (centerSlots != null)
        {
            for (int i = 0; i < centerSlots.Length; i++)
            {
                if (centerSlots[i] != null)
                    centerSlots[i].gameObject.SetActive(false);
            }
        }
    }

    /// <summary>由“GO”按钮调用：对当前选中的关卡发起进入请求（未选中则无操作）。</summary>
    public void StartSelectedStage()
    {
        if (_selected == null)
            return;

        _selected.StartStage();
        ClearSelection();   // 点 GO 后立即收起选中视觉，加载与局内不再显示标记与黄光。
    }

    /// <summary>把某个 RectTransform 的锚点位置换算到 Canvas 坐标系（沿父链累加）。</summary>
    private Vector2 GetCanvasPosition(RectTransform rt)
    {
        Vector2 pos = Vector2.zero;
        RectTransform cur = rt;
        while (cur != null && cur != _canvasRect)
        {
            pos += cur.anchoredPosition;
            cur = cur.parent as RectTransform;
        }
        return pos;
    }
}
