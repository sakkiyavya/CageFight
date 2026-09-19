using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>主界面当前工程师头像：显示对应头像并循环其休闲帧。</summary>
[DisallowMultipleComponent]
public sealed class EngineerMenuPortrait : MonoBehaviour
{
    [SerializeField] private PlayerLoadoutManager loadout;
    [SerializeField] private Image portrait;

    private Sprite[] frames;
    private float frameTime;
    private float elapsed;
    private int frameIndex;
    private Coroutine setupRoutine;
    private bool presentationReady;
    private RemoteStartup loadingScreen;
    private float baseSlotHeight;  // 槽位基准高度：所有工程师立绘统一显示高度。

    private void OnEnable()
    {
        presentationReady = false;
        frames = null;
        if (portrait)
        {
            portrait.preserveAspect = true;
            baseSlotHeight = portrait.rectTransform.sizeDelta.y;
            portrait.enabled = portrait.sprite != null;
        }
        // 首包进入菜单后，等注册表、存档及展示资源就绪，再揭开启动遮罩。
        loadingScreen = RemoteStartup.Instance;
        loadingScreen?.ShowLoading(this);
        if (loadout) loadout.Changed += HandleLoadoutChanged;
        setupRoutine = StartCoroutine(SetupRoutine());
    }

    private void OnDisable()
    {
        if (loadout) loadout.Changed -= HandleLoadoutChanged;
        if (setupRoutine != null) StopCoroutine(setupRoutine);
        setupRoutine = null;
        presentationReady = false;
        loadingScreen?.HideLoading(this);
        loadingScreen = null;
    }

    private IEnumerator SetupRoutine()
    {
        while (loadout && !loadout.IsReady) yield return null;
        if (loadout) yield return loadout.PreloadPresentationResources();
        setupRoutine = null;
        presentationReady = true;
        // 读取此刻的选择，包含等待期间存档恢复或默认选择产生的变更。
        Refresh();
        loadingScreen?.HideLoading(this);
        loadingScreen = null;
    }

    private void HandleLoadoutChanged()
    {
        if (presentationReady) Refresh();
    }

    private void Update()
    {
        if (frames == null || frames.Length < 2 || !portrait) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed < frameTime) return;
        elapsed -= frameTime;
        frameIndex = (frameIndex + 1) % frames.Length;
        portrait.sprite = frames[frameIndex];
        ApplySlotFit(portrait.sprite);
    }

    private void Refresh()
    {
        frames = null;
        frameIndex = 0;
        elapsed = 0f;
        if (!portrait || !loadout || !loadout.TryGetSelectedEngineer(out EngineerDefinition engineer) ||
            !ResourceManager.Instance) return;

        string[] keys = engineer.IdlePortraitFrameKeys;
        if (keys.Length == 0)
        {
            // 未配置待机帧：显示静态立绘（PortraitFrameKey），缺失时回退小图标。
            SetPortraitSprite(ResolvePortraitSprite(engineer));
            return;
        }

        frames = new Sprite[keys.Length];
        int count = 0;
        for (int i = 0; i < keys.Length; i++)
        {
            Sprite sprite = ResourceManager.Instance.GetSprite(keys[i]);
            if (sprite) frames[count++] = sprite;
        }
        if (count == 0)
        {
            // 待机帧全部解析失败：回退静态立绘。
            SetPortraitSprite(ResolvePortraitSprite(engineer));
            frames = null;
            return;
        }
        if (count != frames.Length) System.Array.Resize(ref frames, count);
        frameTime = 1f / Mathf.Max(1f, engineer.IdlePortraitFrameRate);
        SetPortraitSprite(frames[0]);
    }

    /// <summary>只用有效图片更新立绘；资源缺失时保留已有图片，没有图片则隐藏白色占位。</summary>
    private void SetPortraitSprite(Sprite sprite)
    {
        if (sprite)
        {
            portrait.sprite = sprite;
            ApplySlotFit(sprite);
        }
        portrait.enabled = portrait.sprite != null;
    }

    /// <summary>
    /// 按槽位基准高度等比适配立绘：宽高各异的工程师贴图统一显示高度，
    /// 宽度随贴图比例自然变化，新增工程师无需任何配置即自动跟随。
    /// </summary>
    private void ApplySlotFit(Sprite sprite)
    {
        if (!sprite || !portrait || baseSlotHeight <= 0f) return;
        float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
        Vector2 target = new Vector2(baseSlotHeight * aspect, baseSlotHeight);
        if (portrait.rectTransform.sizeDelta != target)
            portrait.rectTransform.sizeDelta = target;
    }

    /// <summary>解析静态立绘：优先 PortraitFrameKey，缺失时回退 IconKey。</summary>
    private Sprite ResolvePortraitSprite(EngineerDefinition engineer)
    {
        Sprite sprite = null;
        if (!string.IsNullOrEmpty(engineer.PortraitFrameKey))
            sprite = ResourceManager.Instance.GetSprite(engineer.PortraitFrameKey);
        if (sprite == null && !string.IsNullOrEmpty(engineer.IconKey))
            sprite = ResourceManager.Instance.GetSprite(engineer.IconKey);
        return sprite;
    }
}
