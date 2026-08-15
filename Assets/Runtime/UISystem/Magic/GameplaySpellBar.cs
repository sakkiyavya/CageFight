using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 局内三格法术栏。UI 节点、冷却遮罩、瞄准线和相机均由 Inspector 预先配置，
/// 本组件不扫描场景，也不动态创建 UI。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameplaySpellBar : MonoBehaviour
{
    [SerializeField] private PlayerLoadoutManager loadout;
    [SerializeField] private Image[] icons = new Image[3];
    [SerializeField] private Image[] cooldownMasks = new Image[3];
    [SerializeField] private LineRenderer aimPreview;
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

    private readonly float[] readyTimes = new float[3];
    private Vector3 aimPoint;
    private Material aimPreviewInstance;
    private Vector2 aimTextureOffset;
    private RenderTexture aimTextureCrop;
    private float aimBaseWidth;
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

    /// <summary>开始拖拽瞄准；调用方应在指针按下时调用。</summary>
    public bool BeginAim(int slot, Vector2 screenPoint)
    {
        if (!CanCast(slot, out EngineerController engineer, out SpellDefinition spell) ||
            !spell.DragAim || !worldCamera)
            return false;

        if (aimPreview)
        {
            ApplyAimSorting(engineer);
            aimPreview.enabled = true;
        }

        UpdateAim(slot, screenPoint);
        return true;
    }

    /// <summary>更新拖拽落点和预先配置的抛物线预览。</summary>
    public void UpdateAim(int slot, Vector2 screenPoint)
    {
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

    /// <summary>结束拖拽并向当前落点施法；调用方应在指针抬起时调用。</summary>
    public void ReleaseAim(int slot)
    {
        HideAimPreview();
        if (!CanCast(slot, out EngineerController engineer, out SpellDefinition spell)) return;
        if (!EngineerSpellCaster.Cast(spell, engineer, aimPoint)) return;
        readyTimes[slot] = Time.time + spell.Cooldown;
    }

    /// <summary>中断当前瞄准表现，不触发施法。</summary>
    public void CancelAim() => HideAimPreview();

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
            if (i < cooldownMasks.Length && cooldownMasks[i])
            {
                cooldownMasks[i].sprite = icons[i].sprite;
                cooldownMasks[i].enabled = hasSpell && icons[i].sprite;
            }
        }
    }

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
        if (!aimPreview) return;
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

    private void HideAimPreview()
    {
        if (aimPreview) aimPreview.enabled = false;
        if (aimTargetPreview) aimTargetPreview.enabled = false;
    }

    private void ConfigureAimTexture()
    {
        Texture2D texture = aimPreviewSprite ? aimPreviewSprite.texture : aimPreviewTexture;
        if (!texture) return;

        aimPreviewInstance.mainTexture = texture;
        if (aimPreviewSprite)
        {
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
            aimPreview.sortingLayerID = renderer.sortingLayerID;
            if (aimTargetPreview) aimTargetPreview.sortingLayerID = renderer.sortingLayerID;
        }

        aimPreview.sortingOrder = aimSortingOrder;
        if (aimTargetPreview) aimTargetPreview.sortingOrder = aimSortingOrder + 1;
    }
}
