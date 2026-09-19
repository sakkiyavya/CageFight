using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>The only player scene: local animation first, remote assets before menu Awake/Start.</summary>
public sealed class RemoteStartup : MonoBehaviour
{
    public static RemoteStartup Instance { get; private set; }
    public const string PreloadLabel = "StartupPreload";
    public const string MenuAddress = "StartupMenuScene";
    [SerializeField] private Sprite[] frames;
    [SerializeField, Min(1)] private float framesPerSecond = 12;

    private Image animationImage;
    private GameObject overlay;
    private float elapsed;
    private string message;
    private bool failed;
    private bool complete;
    private Text statusText;
    private Button retryButton;
    private GameObject retryEvents;
    private AsyncOperationHandle<IList<Object>> assets;
    private AsyncOperationHandle<SceneInstance> menu;
    private readonly HashSet<object> loadingOwners = new HashSet<object>();
    private const float MinimumLoadingSeconds = 0.5f;
    private double loadingStartedAt;

    /// <summary>正式包复用启动对象；编辑器直开菜单场景时，仅创建本地动画，不执行远程首包流程。</summary>
    public static RemoteStartup GetForStageLoading()
    {
#if UNITY_EDITOR
        if (Instance == null)
        {
            var sprites = new List<Sprite>();
            foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                         "Assets/Resource/LocalResource/Animation/Load Anime AP.png"))
                if (asset is Sprite sprite) sprites.Add(sprite);
            // 按数字帧号排序，避免第 10 帧排在第 2 帧之前。
            sprites.Sort((a, b) => GetFrameNumber(a).CompareTo(GetFrameNumber(b)));
            if (sprites.Count == 0)
            {
                Debug.LogWarning("[RemoteStartup] 未找到本地加载动画帧。");
                return null;
            }
            var preview = new GameObject("Stage loading preview").AddComponent<RemoteStartup>();
            preview.frames = sprites.ToArray();
            preview.complete = true;
            preview.CreateOverlay();
            preview.overlay.SetActive(false);
        }
#endif
        return Instance;
    }

#if UNITY_EDITOR
    private static int GetFrameNumber(Sprite sprite)
    {
        string suffix = sprite.name.Substring(sprite.name.LastIndexOf('_') + 1);
        return int.TryParse(suffix, out int number) ? number : 0;
    }
#endif

    public IEnumerator WaitForMinimumDisplay()
    {
        while (overlay && overlay.activeSelf &&
               Time.realtimeSinceStartupAsDouble - loadingStartedAt < MinimumLoadingSeconds)
            yield return null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>复用首包内置动画；多个加载流程全部结束后才隐藏遮罩。</summary>
    public void ShowLoading(object owner)
    {
        if (owner == null || !loadingOwners.Add(owner)) return;
        if (loadingOwners.Count == 1) loadingStartedAt = Time.realtimeSinceStartupAsDouble;
        if (!overlay) return;
        if (!overlay.activeSelf)
        {
            elapsed = 0;
            if (frames != null && frames.Length > 0) animationImage.sprite = frames[0];
        }
        if (complete)
        {
            message = "Loading resources...";
            statusText.text = message;
            retryButton.gameObject.SetActive(false);
        }
        overlay.SetActive(true);
    }

    public void HideLoading(object owner)
    {
        if (owner != null) loadingOwners.Remove(owner);
        if (complete && loadingOwners.Count == 0 && overlay &&
            Time.realtimeSinceStartupAsDouble - loadingStartedAt >= MinimumLoadingSeconds)
            overlay.SetActive(false);
    }

    private IEnumerator Start()
    {
        if (complete) yield break; // 编辑器直开场景只复用动画，不下载或切换菜单。
        CreateOverlay();
        if (frames == null || frames.Length == 0)
        {
            Fail("Loading animation has no frames. Reconfigure the startup scene.");
            yield break;
        }
        // Give the local loading screen a frame before starting any network work.
        yield return null;
        yield return LoadMenu();
    }

    private void CreateOverlay()
    {
        if (overlay) return;
        var canvasObject = new GameObject("Startup loading", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        overlay = canvasObject;
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);
        var background = new GameObject("Background", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        background.transform.SetParent(canvasObject.transform, false);
        background.color = new Color(0.06f, 0.07f, 0.09f);
        background.rectTransform.anchorMin = Vector2.zero;
        background.rectTransform.anchorMax = Vector2.one;
        background.rectTransform.sizeDelta = Vector2.zero;
        animationImage = new GameObject("Loading animation", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        animationImage.transform.SetParent(canvasObject.transform, false);
        animationImage.rectTransform.sizeDelta = new Vector2(300, 300);
        animationImage.preserveAspect = true;
        statusText = CreateText("Status", canvasObject.transform, new Vector2(0, -210), new Vector2(1100, 60));
        retryButton = new GameObject("Retry", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        retryButton.transform.SetParent(canvasObject.transform, false);
        var retryRect = (RectTransform)retryButton.transform;
        retryRect.sizeDelta = new Vector2(180, 55);
        retryRect.anchoredPosition = new Vector2(0, -285);
        retryButton.targetGraphic = retryButton.GetComponent<Image>();
        var retryText = CreateText("Retry label", retryButton.transform, Vector2.zero, new Vector2(180, 55));
        retryText.text = "Retry";
        retryText.color = Color.black;
        retryButton.onClick.AddListener(() => { if (failed) StartCoroutine(LoadMenu()); });
        retryButton.gameObject.SetActive(false);
        canvasObject.AddComponent<GraphicRaycaster>();
        if (frames != null && frames.Length > 0) animationImage.sprite = frames[0];
    }

    private void Update()
    {
        if (complete && loadingOwners.Count == 0 && overlay && overlay.activeSelf &&
            Time.realtimeSinceStartupAsDouble - loadingStartedAt >= MinimumLoadingSeconds)
            overlay.SetActive(false);
        if (statusText) statusText.text = message ?? "Loading...";
        if (!overlay || !overlay.activeInHierarchy || !animationImage || frames == null || frames.Length == 0) return;
        elapsed += Time.unscaledDeltaTime;
        animationImage.sprite = frames[(int)(elapsed * framesPerSecond) % frames.Length];
    }

    private IEnumerator LoadMenu()
    {
        failed = false;
        retryButton.gameObject.SetActive(false);
        if (retryEvents) Destroy(retryEvents);
        message = "Initializing...";
        var initialize = Addressables.InitializeAsync(false);
        yield return initialize;
        bool initialized = initialize.Status == AsyncOperationStatus.Succeeded;
        string error = initialize.OperationException?.Message;
        Addressables.Release(initialize);
        if (!initialized) { Fail(error); yield break; }

        // Resolve explicitly: an absent label must fail instead of silently entering an incomplete menu.
        var locations = Addressables.LoadResourceLocationsAsync(PreloadLabel, typeof(Object));
        yield return locations;
        if (locations.Status != AsyncOperationStatus.Succeeded || locations.Result.Count == 0)
        {
            error = locations.OperationException?.Message ?? "StartupPreload label is empty. Rebuild Addressables.";
            Addressables.Release(locations);
            Fail(error);
            yield break;
        }
        message = "Downloading resources...";
        var download = Addressables.DownloadDependenciesAsync(locations.Result, false);
        while (!download.IsDone)
        {
            var status = download.GetDownloadStatus();
            message = $"Downloading resources... {status.Percent:P0}";
            yield return null;
        }
        bool downloaded = download.Status == AsyncOperationStatus.Succeeded;
        error = download.OperationException?.Message;
        Addressables.Release(download);
        if (!downloaded)
        {
            Addressables.Release(locations);
            Fail(error);
            yield break;
        }

        message = "Loading resources...";
        // Keep this handle for the session: downloading alone does not load assets into memory.
        assets = Addressables.LoadAssetsAsync<Object>(locations.Result, null, true);
        yield return assets;
        Addressables.Release(locations);
        if (assets.Status != AsyncOperationStatus.Succeeded)
        {
            error = assets.OperationException?.Message;
            Addressables.Release(assets);
            assets = default;
            Fail(error);
            yield break;
        }

        message = "Opening menu...";
        // No menu GameObject exists before the preloading operation above succeeds.
        menu = Addressables.LoadSceneAsync(MenuAddress, LoadSceneMode.Single, true);
        yield return menu;
        if (menu.Status != AsyncOperationStatus.Succeeded)
        {
            error = menu.OperationException?.Message;
            Addressables.Release(menu);
            menu = default;
            Addressables.Release(assets);
            assets = default;
            Fail(error);
            yield break;
        }
        yield return null;
        complete = true;
        message = "Loading resources...";
        overlay.SetActive(loadingOwners.Count > 0);
    }

    private void Fail(string error)
    {
        failed = true;
        retryButton.gameObject.SetActive(true);
        if (!EventSystem.current)
        {
            retryEvents = new GameObject("Startup retry input", typeof(EventSystem), typeof(StandaloneInputModule));
            retryEvents.transform.SetParent(transform, false);
        }
        message = "Loading failed. Check your connection and retry.";
        Debug.LogError("[RemoteStartup] " + error);
    }

    private static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var text = new GameObject(name, typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        text.transform.SetParent(parent, false);
        text.rectTransform.anchoredPosition = position;
        text.rectTransform.sizeDelta = size;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (assets.IsValid()) Addressables.Release(assets);
        if (menu.IsValid()) Addressables.Release(menu);
    }
}
