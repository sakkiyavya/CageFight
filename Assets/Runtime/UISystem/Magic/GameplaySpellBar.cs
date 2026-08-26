using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内法术栏。UI 节点、冷却遮罩、瞄准线和相机均由 Inspector 预先配置，
/// 本组件不动态创建 UI，也不做全局场景查找。
/// 栏位布局自动计算：仅扫描自身直接子物体中挂有 SpellSlotButton 的活动栏位，
/// 按子物体顺序从左到右、以 slotRightEdge 为右边界靠边排列，栏位增删后自动重排。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplaySpellBar : MonoBehaviour
{
    [SerializeField] private PlayerLoadoutManager loadout;
    [SerializeField] private Image[] icons = new Image[3];
    [SerializeField] private Image[] cooldownMasks = new Image[3];
    [SerializeField] private LineRenderer aimPreview;
    [SerializeField] private SpriteRenderer aimStripPreview;
    [SerializeField] private Sprite aimPreviewSprite;
    [SerializeField] private Texture2D aimPreviewTexture;
    [SerializeField] private Material aimPreviewMaterial;
    [SerializeField] private SpriteRenderer aimTargetPreview;
    [SerializeField, Min(0f)] private float aimFlowSpeed = 1.5f;
    [SerializeField, Min(.01f)] private float aimTextureTiling = 1f;
    [SerializeField, Min(.1f)] private float aimVisualScale = 4f;
    [SerializeField, Range(0f, 1f)] private float warningPreviewAlpha = .45f;
    [SerializeField] private int aimSortingOrder = 32760;
    [SerializeField] private Camera worldCamera;
    [SerializeField, Range(8, 48)] private int arcPointCount = 24;
    [SerializeField, Tooltip("拖拽瞄准取消区（右上角 Set UI-3）；拖入该矩形区域后抬起即取消施法。仅拖拽瞄准期间显示，其余时间隐藏")]
    private RectTransform cancelZone;

    [Header("栏位自动布局")]
    [SerializeField, Tooltip("栏位排的右边界（模块本地坐标 x，相对模块左边缘）；展开状态下该点即贴屏幕右缘的终点")]
    private float slotRowRightEdge = 903.5572f;
    [SerializeField, Min(0f), Tooltip("相邻栏位的横向间距（像素）")]
    private float slotGap = 44f;
    [SerializeField, Tooltip("三角按钮相对排内位置的额外右移量（正值向右靠近图标排）")]
    private float triangleOffset = 20f;

    private readonly float[] readyTimes = new float[3];
    private readonly List<RectTransform> _slots = new List<RectTransform>();     // 活动栏位缓存（按子物体顺序）。
    private Vector3 aimPoint;
    private bool cancelRequested;                                           // 当前指针是否位于取消区内。
    private Material aimPreviewInstance;
    private Vector2 aimTextureOffset;
    private RenderTexture aimTextureCrop;
    private float aimBaseWidth;
    private float aimStripHeight;
    private Vector3 aimTargetBaseScale;
    private Sprite aimTargetDefaultSprite;

    private void Awake()
    {
        if (aimPreview)
        {
            aimBaseWidth = aimPreview.widthMultiplier;
            aimPreview.positionCount = arcPointCount;
            // 准星已在当前渲染管线中验证可见；未指定专用材质时直接复用它。
            Material material = aimPreviewMaterial ? aimPreviewMaterial :
                aimTargetPreview ? aimTargetPreview.sharedMaterial : aimPreview.sharedMaterial;
            if (material) aimPreview.sharedMaterial = material;
            aimPreview.startColor = Color.white;
            aimPreview.endColor = Color.white;
            aimPreview.textureMode = LineTextureMode.Tile;
            aimPreview.widthMultiplier = aimBaseWidth * aimVisualScale;
            aimPreviewInstance = aimPreview.material;
            ConfigureAimTexture();
            aimPreview.enabled = false;
        }
        if (aimTargetPreview)
        {
            aimTargetBaseScale = aimTargetPreview.transform.localScale;
            aimTargetDefaultSprite = aimTargetPreview.sprite;
            aimTargetPreview.enabled = false;
        }
        if (aimStripPreview)
        {
            aimStripPreview.sprite = aimPreviewSprite;
            aimStripPreview.drawMode = SpriteDrawMode.Tiled;
            aimStripPreview.enabled = false;
            aimStripHeight = aimPreviewSprite ?
                aimPreviewSprite.rect.height / aimPreviewSprite.pixelsPerUnit * aimVisualScale : .2f;
        }
    }

    private void OnEnable()
    {
        if (!loadout)
        {
            Debug.LogError("[GameplaySpellBar] 未配置 PlayerLoadoutManager。", this);
            return;
        }

        loadout.Changed += RefreshIcons;
        RefreshIcons();
        LayoutSlots();
    }

    private void OnDisable()
    {
        if (loadout) loadout.Changed -= RefreshIcons;
        HideAimPreview();
    }

    private void OnDestroy()
    {
        if (!aimTextureCrop) return;
        aimTextureCrop.Release();
        Destroy(aimTextureCrop);
    }

    private void Update()
    {
        if (aimPreview && aimPreview.enabled && aimPreviewInstance)
            aimPreviewInstance.mainTextureOffset = aimTextureOffset +
                new Vector2(Time.unscaledTime * aimFlowSpeed, 0f);

        for (int i = 0; i < readyTimes.Length; i++)
        {
            if (i >= cooldownMasks.Length || !cooldownMasks[i]) continue;
            if (!TryGetSpell(i, out SpellDefinition spell))
            {
                cooldownMasks[i].enabled = false;
                continue;
            }

            cooldownMasks[i].enabled = true;
            float cooldown = spell.Cooldown;
            cooldownMasks[i].fillAmount = cooldown <= 0f ? 1f :
                1f - Mathf.Clamp01((readyTimes[i] - Time.time) / cooldown);
        }
    }

    /// <summary>立即尝试施放不可拖拽法术；成功时才开始冷却。</summary>
    public void Cast(int slot)
    {
        if (!CanCast(slot, out EngineerController engineer, out SpellDefinition spell)) return;
        if (!EngineerSpellCaster.Cast(spell, engineer)) return;
        readyTimes[slot] = Time.time + spell.Cooldown;
    }

    /// <summary>开始拖拽瞄准；调用方应在指针按下时调用。进入瞄准即显示取消区。</summary>
    public bool BeginAim(int slot, Vector2 screenPoint)
    {
        if (!CanCast(slot, out EngineerController engineer, out SpellDefinition spell) ||
            !spell.DragAim || !worldCamera)
            return false;

        cancelRequested = false;
        SetCancelZoneVisible(true);

        if (aimPreview)
        {
            ApplyAimSorting(engineer);
            aimPreview.enabled = true;
        }
        if (aimStripPreview) aimStripPreview.enabled = true;

        UpdateAim(slot, screenPoint);
        return true;
    }

    /// <summary>
    /// 更新拖拽落点和预先配置的抛物线预览。
    /// 指针进入取消区时隐藏瞄准表现并进入取消状态；移出取消区时恢复瞄准。
    /// </summary>
    public void UpdateAim(int slot, Vector2 screenPoint)
    {
        cancelRequested = IsInCancelZone(screenPoint);
        if (cancelRequested)
        {
            HideAimPreview();
            return;
        }

        // 从取消区拖出时恢复瞄准表现。
        if (aimPreview) aimPreview.enabled = true;
        if (aimStripPreview) aimStripPreview.enabled = true;

        if (!TryGetSpell(slot, out SpellDefinition spell) || !worldCamera ||
            !EngineerController.Active)
            return;

        EngineerController engineer = EngineerController.Active;
        Vector3 start = engineer.SpellPosition;
        float distance = Mathf.Abs(start.z - worldCamera.transform.position.z);
        Vector3 raw = worldCamera.ScreenToWorldPoint(
            new Vector3(screenPoint.x, screenPoint.y, distance));
        raw.z = start.z;
        aimPoint = start + Vector3.ClampMagnitude(raw - start, spell.MaxDistance);
        DrawArc(start, aimPoint,
            spell.DeliveryType == SpellDeliveryType.Projectile ? spell.ArcHeight : 0f);
        if (aimTargetPreview)
        {
            Sprite warningSprite = spell.ShowWarningCircle && ResourceManager.Instance
                ? ResourceManager.Instance.GetSprite(spell.WarningCircleKey) : null;
            aimTargetPreview.sprite = warningSprite ? warningSprite : aimTargetDefaultSprite;
            aimTargetPreview.transform.position = new Vector3(aimPoint.x, aimPoint.y, -2f);
            aimTargetPreview.transform.localScale = aimTargetBaseScale *
                (spell.ShowWarningCircle ? spell.WarningCircleScale : 1f);
            Color color = aimTargetPreview.color;
            color.a = spell.ShowWarningCircle ? warningPreviewAlpha : 1f;
            aimTargetPreview.color = color;
            aimTargetPreview.enabled = aimTargetPreview.sprite;
        }
    }

    /// <summary>
    /// 结束拖拽：指针在取消区内抬起则取消施法（不进入冷却）；
    /// 否则向当前落点施法。结束后隐藏取消区。调用方应在指针抬起时调用。
    /// </summary>
    public void ReleaseAim(int slot)
    {
        HideAimPreview();
        SetCancelZoneVisible(false);
        if (cancelRequested)
        {
            cancelRequested = false;
            return;
        }
        if (!CanCast(slot, out EngineerController engineer, out SpellDefinition spell)) return;
        if (!EngineerSpellCaster.Cast(spell, engineer, aimPoint)) return;
        readyTimes[slot] = Time.time + spell.Cooldown;
    }

    /// <summary>中断当前瞄准表现并隐藏取消区，不触发施法。</summary>
    public void CancelAim()
    {
        cancelRequested = false;
        SetCancelZoneVisible(false);
        HideAimPreview();
    }

    /// <summary>
    /// 切换取消区的显示状态；仅在拖拽瞄准期间可见，其余时间隐藏。
    /// </summary>
    private void SetCancelZoneVisible(bool visible)
    {
        if (cancelZone != null && cancelZone.gameObject != null)
            cancelZone.gameObject.SetActive(visible);
    }

    /// <summary>
    /// 判断屏幕坐标是否位于取消区内；未配置取消区时始终返回 false。
    /// </summary>
    private bool IsInCancelZone(Vector2 screenPoint)
    {
        if (cancelZone == null)
            return false;

        return RectTransformUtility.RectangleContainsScreenPoint(cancelZone, screenPoint);
    }

    /// <summary>根据当前选择和资源缓存刷新三格图标。</summary>
    public void RefreshIcons()
    {
        for (int i = 0; i < icons.Length && i < 3; i++)
        {
            if (!icons[i]) continue;

            bool hasSpell = TryGetSpell(i, out SpellDefinition spell);
            Sprite icon = null;
            if (hasSpell && ResourceManager.Instance)
                icon = ResourceManager.Instance.GetSprite(spell.IconKey);

            // 图标资源尚未进入缓存时保留预置图片，避免 HUD 短暂变空。
            if (icon) icons[i].sprite = icon;
            icons[i].color = hasSpell ? Color.black : Color.clear;
            // 保持图标纵横比，避免非正方形图标在法术栏被拉伸。
            icons[i].preserveAspect = true;
            if (i < cooldownMasks.Length && cooldownMasks[i])
            {
                cooldownMasks[i].sprite = icons[i].sprite;
                cooldownMasks[i].enabled = hasSpell && icons[i].sprite;
                cooldownMasks[i].preserveAspect = true;
            }
        }

        LayoutSlots();
    }

    /// <summary>
    /// 栏位自动布局：收集自身直接子物体中挂有 SpellSlotButton 的活动栏位，
    /// 按子物体顺序从左到右、以 slotRowRightEdge（模块本地固定右边界）为基准
    /// 靠边右对齐排列；栏位数量增删后自动重排，无需手工调整坐标。
    /// 面板整体展开/收起由模块自身 UISystemBase 起止配置驱动，本布局只负责排内对齐。
    /// 仅遍历直接子物体，非逐帧调用。
    /// </summary>
    public void LayoutSlots()
    {
        CollectSlots();
        if (_slots.Count == 0)
            return;

        // 从固定的模块本地右边界向左依次排布；每个栏位右缘紧贴上一位的左缘减去间距。
        float cursor = slotRowRightEdge;

        for (int i = _slots.Count - 1; i >= 0; i--)
        {
            RectTransform slot = _slots[i];
            if (slot == null)
                continue;

            float targetX = cursor - slot.rect.width;
            // 三角按钮额外右移，贴近图标排（仅三角有效，不影响其他栏位间距）。
            if (slot.GetComponent<TriangleButton>() != null)
                targetX += triangleOffset;

            Vector2 position = slot.anchoredPosition;
            // 仅在值发生变化时写入，避免无关刷新重写布局属性。
            if (!Mathf.Approximately(position.x, targetX))
                slot.anchoredPosition = new Vector2(targetX, position.y);

            cursor = targetX - slotGap;
        }
    }

    /// <summary>收集活动栏位：法术槽位 + 转入转出三角按钮（作为排头元素），仅自身直接子物体。</summary>
    private void CollectSlots()
    {
        _slots.Clear();
        foreach (Transform child in transform)
        {
            if (child == null || !child.gameObject.activeInHierarchy)
                continue;
            // 栏位 = 法术槽位按钮 + 转入转出三角按钮（三角按子物体顺序天然排在最左端）。
            if (child.GetComponent<SpellSlotButton>() == null &&
                child.GetComponent<TriangleButton>() == null)
                continue;

            _slots.Add(child as RectTransform);
        }
    }

#if UNITY_EDITOR
    /// <summary>编辑器内即时预览布局；仅编辑器生效，不参与运行时逻辑。</summary>
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;
        LayoutSlots();
    }
#endif

    private bool CanCast(
        int slot,
        out EngineerController engineer,
        out SpellDefinition spell)
    {
        engineer = EngineerController.Active;
        spell = null;
        return (uint)slot < readyTimes.Length && loadout && engineer &&
            !engineer.IsStunned && Time.time >= readyTimes[slot] &&
            loadout.TryGetGameplaySpell(slot, out spell);
    }

    private bool TryGetSpell(int slot, out SpellDefinition spell)
    {
        spell = null;
        return (uint)slot < readyTimes.Length && loadout &&
            loadout.TryGetGameplaySpell(slot, out spell);
    }

    private void DrawArc(Vector3 start, Vector3 end, float height)
    {
        if (aimPreview)
        {
            int pointCount = Mathf.Max(2, arcPointCount);
            if (aimPreview.positionCount != pointCount) aimPreview.positionCount = pointCount;

            for (int i = 0; i < pointCount; i++)
            {
                float t = i / (pointCount - 1f);
                Vector3 point = Vector3.Lerp(start, end, t) +
                    Vector3.up * (4f * height * t * (1f - t));
                point.z = -2f;
                aimPreview.SetPosition(i, point);
            }
        }

        if (!aimStripPreview) return;
        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length < .01f) return;
        Vector2 normal = new Vector2(-delta.y, delta.x).normalized;
        aimStripPreview.transform.SetPositionAndRotation(
            start + (Vector3)(normal * (aimStripHeight * .5f)) + Vector3.forward * -2f,
            Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg));
        aimStripPreview.size = new Vector2(length, aimStripHeight);
    }

    private void HideAimPreview()
    {
        if (aimPreview) aimPreview.enabled = false;
        if (aimStripPreview) aimStripPreview.enabled = false;
        if (aimTargetPreview) aimTargetPreview.enabled = false;
    }

    private void ConfigureAimTexture()
    {
        Texture2D texture = aimPreviewSprite ? aimPreviewSprite.texture : aimPreviewTexture;
        if (!texture) return;

        aimPreviewInstance.mainTexture = texture;
        if (aimPreviewSprite)
        {
            // 多 Sprite 图中只取“虚线”所在的 UV 区域，保证流动时不会滑进旁边的准星图。
            Rect rect = aimPreviewSprite.rect;
            aimTextureCrop = new RenderTexture((int)rect.width, (int)rect.height, 0)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = texture.filterMode
            };
            aimTextureCrop.Create();
            Graphics.Blit(texture, aimTextureCrop,
                new Vector2(rect.width / texture.width, rect.height / texture.height),
                new Vector2(rect.x / texture.width, rect.y / texture.height));
            aimPreviewInstance.mainTexture = aimTextureCrop;
            aimTextureOffset = Vector2.zero;
            aimPreviewInstance.mainTextureScale = new Vector2(aimTextureTiling / aimVisualScale, 1f);
        }
        else
            aimPreviewInstance.mainTextureScale = new Vector2(aimTextureTiling / aimVisualScale, 1f);
    }

    private void ApplyAimSorting(EngineerController engineer)
    {
        SpriteRenderer renderer = engineer.GetComponentInChildren<SpriteRenderer>();
        if (renderer)
        {
            if (aimPreview) aimPreview.sortingLayerID = renderer.sortingLayerID;
            if (aimStripPreview) aimStripPreview.sortingLayerID = renderer.sortingLayerID;
            if (aimTargetPreview) aimTargetPreview.sortingLayerID = renderer.sortingLayerID;
        }

        if (aimPreview) aimPreview.sortingOrder = aimSortingOrder;
        if (aimStripPreview)
        {
            aimStripPreview.sortingOrder = aimSortingOrder + 1;
        }
        if (aimTargetPreview) aimTargetPreview.sortingOrder = aimSortingOrder + 1;
    }
}
