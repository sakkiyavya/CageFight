using System;
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
using TTSDK;
using TTSDK.UNBridgeLib.LitJson;
#endif
using UnityEngine;

/// <summary>
/// 首页侧边栏复访能力封装。
///
/// 完整链路：
/// 1. TTPlatformManager 注册 OnShow，并始终保存最新启动参数；
/// 2. 本服务通过 CheckScene 决定 UI 是否展示入口；
/// 3. 用户点击后通过 NavigateToScene 跳转；
/// 4. 只有后续 OnShow 参数确认来自侧边栏时，才触发 SidebarEntryDetected。
///
/// 注意：NavigateToScene 的 success 只表示“跳转请求成功”，绝不能直接据此发奖励。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TTPlatformManager))]
public sealed class TTSidebarService : MonoBehaviour
{
    [Header("依赖")]
    [SerializeField] private TTPlatformManager platformManager;

    [Header("模拟设置")]
    [Tooltip("Simulated 模式下 CheckScene 返回的本地结果。不会调用 TTSDK。")]
    [SerializeField] private bool simulatedSupport = true;

    [Header("Inspector 事件")]
    [SerializeField] private TTPlatformBoolUnityEvent
        supportChanged = new TTPlatformBoolUnityEvent();
    [SerializeField] private TTPlatformLaunchContextUnityEvent
        sidebarEntryDetected = new TTPlatformLaunchContextUnityEvent();
    [SerializeField] private TTPlatformRequestResultUnityEvent
        requestCompleted = new TTPlatformRequestResultUnityEvent();

    private TTPlatformManager _subscribedManager;
    private TTSidebarSupportState _supportState = TTSidebarSupportState.Unknown;
    private TTPlatformLaunchContext _latestLaunchContext;
    private TTPlatformLaunchContext _lastDetectedSidebarContext;
    private bool _latestShowWasSidebarEntry;
    private bool _isChecking;
    private bool _isNavigating;
    private int _checkGeneration;
    private int _navigateGeneration;
    private bool _checkResolved;
    private bool _navigateResolved;

    public TTSidebarSupportState SupportState => _supportState;
    public bool IsSupported => _supportState == TTSidebarSupportState.Supported;
    public bool IsChecking => _isChecking;
    public bool IsNavigating => _isNavigating;
    public TTPlatformLaunchContext LatestLaunchContext => _latestLaunchContext;
    public bool LatestShowWasSidebarEntry => _latestShowWasSidebarEntry;

    /// <summary>能力检测结果发生变化时触发。</summary>
    public event Action<bool> SupportChanged;

    /// <summary>
    /// 最新 OnShow 被确认来自侧边栏时触发。
    /// 业务层可在这里核验任务状态并发奖；本服务自身不会修改经济数据。
    /// </summary>
    public event Action<TTPlatformLaunchContext> SidebarEntryDetected;

    /// <summary>本服务发起的请求完成时触发。</summary>
    public event Action<TTPlatformRequestResult> RequestCompleted;

    #region Unity 生命周期
    private void Reset()
    {
        platformManager = GetComponent<TTPlatformManager>();
    }

    private void Awake()
    {
        ResolveManager();
    }

    private void OnEnable()
    {
        AttachManagerEvents();
    }

    private void OnDisable()
    {
        DetachManagerEvents();
    }

    private void OnDestroy()
    {
        // 临时 Disable 时仍接收平台请求回调；只有对象销毁后才让旧回调失效。
        _checkGeneration++;
        _navigateGeneration++;
        _isChecking = false;
        _isNavigating = false;
    }
    #endregion

    #region 对外请求
    /// <summary>
    /// 检测当前宿主是否支持首页侧边栏。可直接绑定 Button / UnityEvent。
    /// UI 应只在 IsSupported 为 true 时显示“去侧边栏”入口。
    /// </summary>
    public void RequestCheckSupport()
    {
        if (_isChecking)
        {
            Complete(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.CheckSidebarSupport,
                "侧边栏能力检测正在进行。"));
            return;
        }

        if (!TryPrepareRequest(
                TTPlatformOperation.CheckSidebarSupport,
                out TTPlatformInvocationMode invocationMode,
                out TTPlatformRequestResult blockedResult))
        {
            Complete(blockedResult);
            return;
        }

        _isChecking = true;
        _checkResolved = false;
        int generation = ++_checkGeneration;

        if (invocationMode == TTPlatformInvocationMode.Simulated)
        {
            HandleCheckSuccess(generation, simulatedSupport, simulated: true);
            return;
        }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        try
        {
            TT.CheckScene(
                TTSideBar.SceneEnum.SideBar,
                supported => HandleCheckSuccess(generation, supported, simulated: false),
                () => HandleCheckComplete(generation),
                (errorCode, errorMessage) =>
                    HandleCheckError(generation, errorCode, errorMessage));
        }
        catch (Exception exception)
        {
            HandleCheckError(generation, -1, exception.Message);
        }
#else
        HandleCheckError(generation, -1, "当前构建平台未启用 TTSDK 运行时。" );
#endif
    }

    /// <summary>
    /// 请求跳转首页侧边栏。调用前必须已得到 CheckScene 支持结果。
    /// UI 层应先关闭奖励说明弹窗，再调用本方法。
    /// </summary>
    public void RequestNavigateToSidebar()
    {
        if (_isNavigating)
        {
            Complete(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.NavigateToSidebar,
                "侧边栏跳转请求正在进行，请勿重复点击。"));
            return;
        }

        if (_supportState != TTSidebarSupportState.Supported)
        {
            Complete(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.NavigateToSidebar,
                "尚未确认当前宿主支持侧边栏，请先成功执行 RequestCheckSupport。"));
            return;
        }

        if (!TryPrepareRequest(
                TTPlatformOperation.NavigateToSidebar,
                out TTPlatformInvocationMode invocationMode,
                out TTPlatformRequestResult blockedResult))
        {
            Complete(blockedResult);
            return;
        }

        _isNavigating = true;
        _navigateResolved = false;
        int generation = ++_navigateGeneration;

        if (invocationMode == TTPlatformInvocationMode.Simulated)
        {
            HandleNavigateSuccess(generation, simulated: true);
            return;
        }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        try
        {
            JsonData data = new JsonData();
            data["scene"] = "sidebar";

            TT.NavigateToScene(
                data,
                () => HandleNavigateSuccess(generation, simulated: false),
                () => HandleNavigateComplete(generation),
                (errorCode, errorMessage) =>
                    HandleNavigateError(generation, errorCode, errorMessage));
        }
        catch (Exception exception)
        {
            HandleNavigateError(generation, -1, exception.Message);
        }
#else
        HandleNavigateError(generation, -1, "当前构建平台未启用 TTSDK 运行时。" );
#endif
    }

    /// <summary>
    /// 清除“最新一次 OnShow 来自侧边栏”的本地标记。
    /// 适合业务层消费完回流任务后调用；不会清除平台数据，也不会调用 TTSDK。
    /// </summary>
    public void ResetLatestSidebarEntryFlag()
    {
        _latestShowWasSidebarEntry = false;
    }
    #endregion

    #region 回调处理
    private void HandleCheckSuccess(int generation, bool supported, bool simulated)
    {
        if (!CanResolveCheck(generation))
            return;

        _checkResolved = true;
        _isChecking = false;
        SetSupportState(
            supported
                ? TTSidebarSupportState.Supported
                : TTSidebarSupportState.Unsupported);

        Complete(TTPlatformRequestResult.Success(
            TTPlatformOperation.CheckSidebarSupport,
            supported ? "当前宿主支持首页侧边栏。" : "当前宿主不支持首页侧边栏。",
            simulated));
    }

    private void HandleCheckError(int generation, int errorCode, string errorMessage)
    {
        if (!CanResolveCheck(generation))
            return;

        _checkResolved = true;
        _isChecking = false;

        // 接口失败不等价于平台永久不支持，保留 Unknown 以允许稍后重试。
        SetSupportState(TTSidebarSupportState.Unknown);
        Complete(TTPlatformRequestResult.Failure(
            TTPlatformOperation.CheckSidebarSupport,
            string.IsNullOrWhiteSpace(errorMessage)
                ? "侧边栏能力检测失败。"
                : errorMessage,
            errorCode));
    }

    private void HandleCheckComplete(int generation)
    {
        if (!CanResolveCheck(generation))
            return;

        _checkResolved = true;
        _isChecking = false;
        SetSupportState(TTSidebarSupportState.Unknown);
        Complete(TTPlatformRequestResult.Failure(
            TTPlatformOperation.CheckSidebarSupport,
            "CheckScene 已结束，但没有返回 success 或 error。"));
    }

    private void HandleNavigateSuccess(int generation, bool simulated)
    {
        if (!CanResolveNavigate(generation))
            return;

        _navigateResolved = true;
        _isNavigating = false;
        Complete(TTPlatformRequestResult.Success(
            TTPlatformOperation.NavigateToSidebar,
            "跳转请求已成功发出；需等待后续 OnShow 确认用户是否从侧边栏返回。",
            simulated));
    }

    private void HandleNavigateError(int generation, int errorCode, string errorMessage)
    {
        if (!CanResolveNavigate(generation))
            return;

        _navigateResolved = true;
        _isNavigating = false;
        Complete(TTPlatformRequestResult.Failure(
            TTPlatformOperation.NavigateToSidebar,
            string.IsNullOrWhiteSpace(errorMessage)
                ? "跳转首页侧边栏失败。"
                : errorMessage,
            errorCode));
    }

    private void HandleNavigateComplete(int generation)
    {
        if (!CanResolveNavigate(generation))
            return;

        _navigateResolved = true;
        _isNavigating = false;
        Complete(TTPlatformRequestResult.Failure(
            TTPlatformOperation.NavigateToSidebar,
            "NavigateToScene 已结束，但没有返回 success 或 error。"));
    }

    private bool CanResolveCheck(int generation)
    {
        return this != null &&
               generation == _checkGeneration &&
               !_checkResolved;
    }

    private bool CanResolveNavigate(int generation)
    {
        return this != null &&
               generation == _navigateGeneration &&
               !_navigateResolved;
    }
    #endregion

    #region Manager 事件
    private void AttachManagerEvents()
    {
        ResolveManager();
        if (platformManager == null || _subscribedManager == platformManager)
            return;

        DetachManagerEvents();
        _subscribedManager = platformManager;
        _subscribedManager.AppShown += HandleAppShown;

        if (_subscribedManager.LatestLaunchContext != null)
            HandleAppShown(_subscribedManager.LatestLaunchContext);
    }

    private void DetachManagerEvents()
    {
        if (_subscribedManager != null)
            _subscribedManager.AppShown -= HandleAppShown;

        _subscribedManager = null;
    }

    private void HandleAppShown(TTPlatformLaunchContext context)
    {
        if (context == null)
            return;

        _latestLaunchContext = context;
        _latestShowWasSidebarEntry = context.IsSidebarEntry;

        if (!_latestShowWasSidebarEntry ||
            ReferenceEquals(context, _lastDetectedSidebarContext))
        {
            return;
        }

        // 同一份 OnShow 快照只派发一次；组件 Disable/Enable 不会导致重复发奖事件。
        _lastDetectedSidebarContext = context;
        TTPlatformEventUtility.InvokeSafely(
            SidebarEntryDetected,
            context,
            this,
            nameof(SidebarEntryDetected));
        TTPlatformEventUtility.InvokeSafely(
            sidebarEntryDetected,
            context,
            this,
            nameof(sidebarEntryDetected));
    }
    #endregion

    #region 工具方法
    private bool TryPrepareRequest(
        string operation,
        out TTPlatformInvocationMode invocationMode,
        out TTPlatformRequestResult blockedResult)
    {
        ResolveManager();
        AttachManagerEvents();

        if (platformManager == null)
        {
            invocationMode = TTPlatformInvocationMode.Blocked;
            blockedResult = TTPlatformRequestResult.Unavailable(
                operation,
                "未找到 TTPlatformManager。" );
            return false;
        }

        return platformManager.TryPrepareCapabilityRequest(
            operation,
            requiresInitialization: true,
            out invocationMode,
            out blockedResult);
    }

    private void Complete(TTPlatformRequestResult result)
    {
        if (result == null)
            return;

        ResolveManager();
        if (platformManager != null)
            platformManager.PublishRequestResult(result);
        else if (result.Status == TTPlatformRequestStatus.Failed ||
                 result.Status == TTPlatformRequestStatus.Unavailable)
            Debug.LogError($"[TTSidebarService] {result}", this);
        else
            Debug.LogWarning($"[TTSidebarService] {result}", this);

        TTPlatformEventUtility.InvokeSafely(
            RequestCompleted,
            result,
            this,
            nameof(RequestCompleted));
        TTPlatformEventUtility.InvokeSafely(
            requestCompleted,
            result,
            this,
            nameof(requestCompleted));
    }

    private void SetSupportState(TTSidebarSupportState state)
    {
        if (_supportState == state)
            return;

        _supportState = state;
        bool supported = state == TTSidebarSupportState.Supported;
        TTPlatformEventUtility.InvokeSafely(
            SupportChanged,
            supported,
            this,
            nameof(SupportChanged));
        TTPlatformEventUtility.InvokeSafely(
            supportChanged,
            supported,
            this,
            nameof(supportChanged));
    }

    private void ResolveManager()
    {
        if (platformManager == null)
            platformManager = GetComponent<TTPlatformManager>();

        if (platformManager == null)
            platformManager = TTPlatformManager.Instance;
    }
    #endregion
}
