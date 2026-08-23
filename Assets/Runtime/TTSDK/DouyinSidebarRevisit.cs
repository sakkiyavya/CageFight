using System;
using System.Collections.Generic;
using UnityEngine;

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
#endif

/// <summary>
/// 抖音小游戏首页侧边栏复访能力。
///
/// 自动在首个场景加载前创建，确保尽早初始化并注册 OnShow：
/// 1. CheckScene 结果决定是否展示“侧边栏复访”入口；
/// 2. UI 的用户点击回调调用 NavigateToSidebarFromUserClick；
/// 3. 最新 OnShow 参数为 homepage + sidebar_card 时，IsReturnedFromSidebar 为 true。
///
/// 本组件只判断复访状态，不负责弹窗、礼包或任何奖励发放。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-32000)]
public sealed class DouyinSidebarRevisit : MonoBehaviour
{
    private const string BootstrapObjectName = "DouyinSidebarRevisit";

    public static DouyinSidebarRevisit Instance { get; private set; }

    /// <summary>当前宿主是否支持首页侧边栏入口。</summary>
    public bool IsSidebarSupported { get; private set; }

    /// <summary>
    /// 最新启动/回前台参数是否确认来自首页侧边栏。
    /// 业务层仅应在此值为 true 时展示“领取奖励”。
    /// </summary>
    public bool IsReturnedFromSidebar { get; private set; }

    /// <summary>最近一次启动参数的场景值，仅用于日志和排查。</summary>
    public string LatestScene { get; private set; } = string.Empty;

    public event Action<bool> SidebarSupportChanged;
    public event Action<bool> SidebarReturnStateChanged;
    public event Action<int, string> SidebarRequestFailed;

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
    private TTAppLifeCycle _appLifeCycle;
#endif

    private bool _sdkInitialized;
    private bool _isNavigating;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBeforeFirstScene()
    {
        if (Instance != null)
            return;

        GameObject gameObject = new GameObject(BootstrapObjectName);
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<DouyinSidebarRevisit>();
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

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        // 对应指南的“游戏启动时同步监听”。初始化成功后立即注册 OnShow。
        TT.InitSDK(HandleSdkInitialized);
#endif
    }

    private void OnDestroy()
    {
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        if (_appLifeCycle != null)
            _appLifeCycle.OnShow -= HandleAppShow;
#endif

        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 绑定到“去首页侧边栏”按钮的 OnClick。
    /// 必须从用户点击触发，不要在自动流程、计时器或 Awake 中调用。
    /// </summary>
    public void NavigateToSidebarFromUserClick()
    {
        if (!_sdkInitialized || !IsSidebarSupported)
        {
            Debug.LogWarning(
                "[DouyinSidebarRevisit] 当前宿主不支持侧边栏，或 TTSDK 尚未初始化。",
                this);
            return;
        }

        if (_isNavigating)
            return;

        _isNavigating = true;

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        JsonData data = new JsonData();
        data["scene"] = "sidebar";

        TT.NavigateToScene(
            data,
            () => Debug.Log(
                "[DouyinSidebarRevisit] 已请求跳转首页侧边栏；请等待用户从侧边栏重新进入游戏。",
                this),
            () => _isNavigating = false,
            HandleNavigateFailed);
#else
        _isNavigating = false;
        Debug.LogWarning(
            "[DouyinSidebarRevisit] Unity Editor 不调用 TTSDK；请使用真机完整链路测试。",
            this);
#endif
    }

    /// <summary>
    /// 奖励发放完成后由业务层调用，清除当前回流标记。
    /// 不会清除平台启动参数，也不会调用任何 TTSDK API。
    /// </summary>
    public void ConsumeSidebarReturn()
    {
        SetReturnedFromSidebar(false);
    }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
    private void HandleSdkInitialized(int errorCode, ContainerEnv containerEnvironment)
    {
        if (errorCode != 0 || containerEnvironment == null)
        {
            NotifyFailed(errorCode, "TTSDK 初始化失败，侧边栏复访能力未启用。");
            return;
        }

        _sdkInitialized = true;
        RegisterOnShow();
        CheckSidebarSupport();

        // 冷启动可能不会再补发 OnShow，因此用 ContainerEnv 保存第一次启动参数。
        try
        {
            LaunchOption launchOption = containerEnvironment.GetLaunchOptionsSync();
            UpdateReturnState(
                launchOption != null ? launchOption.Scene : string.Empty,
                containerEnvironment.GetLaunchFrom(),
                containerEnvironment.GetLocation());
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[DouyinSidebarRevisit] 读取冷启动参数失败：{exception.Message}",
                this);
        }
    }

    private void RegisterOnShow()
    {
        _appLifeCycle = TT.GetAppLifeCycle();
        if (_appLifeCycle == null)
        {
            NotifyFailed(-1, "未获取到 TTAppLifeCycle，无法监听 OnShow。");
            return;
        }

        _appLifeCycle.OnShow += HandleAppShow;
    }

    private void CheckSidebarSupport()
    {
        TT.CheckScene(
            TTSideBar.SceneEnum.SideBar,
            SetSidebarSupport,
            () => { },
            (errorCode, errorMessage) =>
            {
                SetSidebarSupport(false);
                NotifyFailed(errorCode, errorMessage);
            });
    }

    private void HandleAppShow(Dictionary<string, object> launchOptions)
    {
        // 不同 SDK/宿主使用 launch_from 或 launchFrom；两种都兼容。
        UpdateReturnState(
            GetString(launchOptions, "scene"),
            GetString(launchOptions, "launch_from", "launchFrom"),
            GetString(launchOptions, "location"));
    }

    private void HandleNavigateFailed(int errorCode, string errorMessage)
    {
        _isNavigating = false;
        NotifyFailed(errorCode, errorMessage);
    }
#endif

    private void SetSidebarSupport(bool supported)
    {
        if (IsSidebarSupported == supported)
            return;

        IsSidebarSupported = supported;
        SidebarSupportChanged?.Invoke(supported);
    }

    private void UpdateReturnState(string scene, string launchFrom, string location)
    {
        LatestScene = scene ?? string.Empty;

        // scene=1036 也可能对应其他入口，因此不能单独据此发奖。
        bool returnedFromSidebar =
            string.Equals(launchFrom, "homepage", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(location, "sidebar_card", StringComparison.OrdinalIgnoreCase);

        SetReturnedFromSidebar(returnedFromSidebar);
    }

    private void SetReturnedFromSidebar(bool returnedFromSidebar)
    {
        if (IsReturnedFromSidebar == returnedFromSidebar)
            return;

        IsReturnedFromSidebar = returnedFromSidebar;
        SidebarReturnStateChanged?.Invoke(returnedFromSidebar);
    }

    private void NotifyFailed(int errorCode, string errorMessage)
    {
        string message = string.IsNullOrWhiteSpace(errorMessage)
            ? "侧边栏请求失败。"
            : errorMessage;

        Debug.LogWarning(
            $"[DouyinSidebarRevisit] 侧边栏请求失败 ({errorCode})：{message}",
            this);
        SidebarRequestFailed?.Invoke(errorCode, message);
    }

    private static string GetString(
        IReadOnlyDictionary<string, object> dictionary,
        params string[] keys)
    {
        if (dictionary == null || keys == null)
            return string.Empty;

        foreach (string key in keys)
        {
            if (dictionary.TryGetValue(key, out object value) && value != null)
                return Convert.ToString(value) ?? string.Empty;
        }

        return string.Empty;
    }
}
