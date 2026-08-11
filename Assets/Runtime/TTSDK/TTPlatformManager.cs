using System;
using System.Collections.Generic;
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
using TTSDK;
#endif
using UnityEngine;

/// <summary>
/// 项目访问 TTSDK 的总入口。
///
/// 设计约束：
/// 1. 默认 Execution Mode 为 Disabled，Awake/OnEnable 不会调用任何 TTSDK API；
/// 2. Simulated 只走本地回调，便于在 Unity Editor 检查 UI 和业务流程；
/// 3. 只有显式切到 Live 并主动调用 RequestInitialize，才可能访问真实平台；
/// 4. 厂商类型被封闭在本目录，其他业务模块只依赖项目层模型和事件。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1100)]
public sealed class TTPlatformManager : MonoBehaviour
{
    public static TTPlatformManager Instance { get; private set; }

    [Header("执行开关")]
    [Tooltip("默认 Disabled：所有平台请求只返回 Skipped，不会调用 TTSDK。运行时修改将在下次启动生效。")]
    [SerializeField] private TTPlatformExecutionMode executionMode =
        TTPlatformExecutionMode.Disabled;

    [Tooltip("初始化成功后注册 OnShow，并读取一次冷启动参数。正式接入侧边栏时建议保持开启。")]
    [SerializeField] private bool prepareLifecycleAfterInitialization = true;

    [Tooltip("跨场景保留本对象。应只在实际启动场景中放置一个实例。")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Tooltip("将每次统一请求结果输出到 Console。")]
    [SerializeField] private bool logRequestResults = true;

    [Header("能力组件（通常与本组件挂在同一对象）")]
    [SerializeField] private TTSidebarService sidebarService;
    [SerializeField] private TTGameRecorderService gameRecorderService;

    [Header("Inspector 事件")]
    [SerializeField] private TTPlatformInitializationStateUnityEvent
        initializationStateChanged = new TTPlatformInitializationStateUnityEvent();
    [SerializeField] private TTPlatformRequestResultUnityEvent
        requestCompleted = new TTPlatformRequestResultUnityEvent();
    [SerializeField] private TTPlatformLaunchContextUnityEvent
        appShown = new TTPlatformLaunchContextUnityEvent();

    private TTPlatformInitializationState _initializationState =
        TTPlatformInitializationState.NotInitialized;
    private TTPlatformExecutionMode _effectiveExecutionMode;
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
    // 当前项目的 ttsdk.dll 在 Import Settings 中禁用了 Editor，厂商类型必须隔离在此条件内。
    private TTAppLifeCycle _appLifeCycle;
#endif
    private bool _isAppShowSubscribed;
    private TTPlatformLaunchContext _latestLaunchContext;
    private int _callbackGeneration;

    /// <summary>Awake 时锁定的本次运行模式；Play 中修改 Inspector 不会混用两套 SDK 状态。</summary>
    public TTPlatformExecutionMode ExecutionMode => _effectiveExecutionMode;
    public TTPlatformInitializationState InitializationState => _initializationState;
    public bool IsInitialized => _initializationState == TTPlatformInitializationState.Initialized;
    public bool IsAppShowSubscribed => _isAppShowSubscribed;
    public TTPlatformLaunchContext LatestLaunchContext => _latestLaunchContext;
    public TTSidebarService Sidebar => sidebarService;
    public TTGameRecorderService GameRecorder => gameRecorderService;

    /// <summary>每次初始化状态变化时触发。</summary>
    public event Action<TTPlatformInitializationState> InitializationStateChanged;

    /// <summary>所有平台请求（包括跳过、拒绝和模拟结果）的统一出口。</summary>
    public event Action<TTPlatformRequestResult> RequestCompleted;

    /// <summary>冷启动参数读取成功或游戏每次回到前台时触发。</summary>
    public event Action<TTPlatformLaunchContext> AppShown;

    #region Unity 生命周期
    private void Reset()
    {
        ResolveCapabilityServices();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[TTPlatformManager] 场景中存在重复实例，后创建的组件将被销毁。",
                this);
            Destroy(this);
            return;
        }

        Instance = this;
        _effectiveExecutionMode = executionMode;
        ResolveCapabilityServices();

        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        // 刻意不在这里初始化或订阅 TTSDK。
        // 当前阶段只有外部显式发起 RequestInitialize 才会进入平台流程。
    }

    private void OnDestroy()
    {
        // 让晚到的异步回调失效，避免对象销毁后继续改状态或派发事件。
        _callbackGeneration++;
        UnsubscribeAppShowWithoutResult();

        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion

    #region 初始化与生命周期
    /// <summary>
    /// 初始化平台。可直接绑定 UnityEvent / UGUI Button。
    /// Disabled 模式只回传 Skipped；Simulated 模式只完成本地状态；Live 才调用 TT.InitSDK。
    /// </summary>
    public void RequestInitialize()
    {
        if (_initializationState == TTPlatformInitializationState.Initializing)
        {
            PublishRequestResult(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.Initialize,
                "已有初始化请求正在进行。"));
            return;
        }

        if (_initializationState == TTPlatformInitializationState.Initialized)
        {
            PublishRequestResult(TTPlatformRequestResult.Success(
                TTPlatformOperation.Initialize,
                "TTSDK 已处于初始化完成状态。",
                ExecutionMode == TTPlatformExecutionMode.Simulated));
            return;
        }

        if (_initializationState == TTPlatformInitializationState.Failed &&
            ExecutionMode == TTPlatformExecutionMode.Live)
        {
            PublishRequestResult(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.Initialize,
                "本进程中的 TTSDK 初始化已失败。6.7.3 不支持可靠的进程内重试，请修正配置后重启游戏。"));
            return;
        }

        if (ExecutionMode == TTPlatformExecutionMode.Disabled)
        {
            PublishRequestResult(TTPlatformRequestResult.Skipped(
                TTPlatformOperation.Initialize,
                "Execution Mode 为 Disabled，未调用 TT.InitSDK。"));
            return;
        }

        if (ExecutionMode == TTPlatformExecutionMode.Simulated)
        {
            SetInitializationState(TTPlatformInitializationState.Initialized);
            PublishRequestResult(TTPlatformRequestResult.Success(
                TTPlatformOperation.Initialize,
                "已完成本地模拟初始化，未调用 TTSDK。",
                simulated: true));
            PrepareLifecycleIfNeeded();
            return;
        }

        if (!CanUseLiveSdk(
                TTPlatformOperation.Initialize,
                out TTPlatformRequestResult blockedResult))
        {
            SetInitializationState(TTPlatformInitializationState.Failed);
            PublishRequestResult(blockedResult);
            return;
        }

        SetInitializationState(TTPlatformInitializationState.Initializing);
        int callbackGeneration = ++_callbackGeneration;

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        try
        {
            TT.InitSDK((errorCode, containerEnvironment) =>
            {
                if (this == null || callbackGeneration != _callbackGeneration)
                    return;

                if (errorCode != 0)
                {
                    SetInitializationState(TTPlatformInitializationState.Failed);
                    PublishRequestResult(TTPlatformRequestResult.Failure(
                        TTPlatformOperation.Initialize,
                        "TT.InitSDK 回调失败。",
                        errorCode));
                    return;
                }

                SetInitializationState(TTPlatformInitializationState.Initialized);
                PublishRequestResult(TTPlatformRequestResult.Success(
                    TTPlatformOperation.Initialize,
                    $"TTSDK 初始化成功，容器环境：{containerEnvironment}。"));
                PrepareLiveLifecycleIfNeeded(containerEnvironment);
            });
        }
        catch (Exception exception)
        {
            SetInitializationState(TTPlatformInitializationState.Failed);
            PublishRequestResult(TTPlatformRequestResult.Failure(
                TTPlatformOperation.Initialize,
                $"调用 TT.InitSDK 时发生异常：{exception.Message}"));
        }
#else
        PublishRequestResult(TTPlatformRequestResult.Unavailable(
            TTPlatformOperation.Initialize,
            "当前构建平台未启用 TTSDK 运行时；Editor 请改用 Simulated 模式。"));
#endif
    }

    /// <summary>
    /// 注册回前台事件。正式侧边栏链路必须监听最新一次 OnShow 参数。
    /// </summary>
    public void RequestSubscribeAppShow()
    {
        if (_isAppShowSubscribed)
        {
            PublishRequestResult(TTPlatformRequestResult.Success(
                TTPlatformOperation.SubscribeAppShow,
                "OnShow 已注册，无需重复注册。",
                ExecutionMode == TTPlatformExecutionMode.Simulated));
            return;
        }

        if (!TryPrepareCapabilityRequest(
                TTPlatformOperation.SubscribeAppShow,
                requiresInitialization: true,
                out TTPlatformInvocationMode invocationMode,
                out TTPlatformRequestResult blockedResult))
        {
            PublishRequestResult(blockedResult);
            return;
        }

        if (invocationMode == TTPlatformInvocationMode.Simulated)
        {
            _isAppShowSubscribed = true;
            PublishRequestResult(TTPlatformRequestResult.Success(
                TTPlatformOperation.SubscribeAppShow,
                "已注册本地模拟 OnShow，未调用 TTSDK。",
                simulated: true));
            return;
        }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        try
        {
            _appLifeCycle = TT.GetAppLifeCycle();
            if (_appLifeCycle == null)
            {
                PublishRequestResult(TTPlatformRequestResult.Unavailable(
                    TTPlatformOperation.SubscribeAppShow,
                    "TT.GetAppLifeCycle 返回空，无法注册 OnShow。"));
                return;
            }

            _appLifeCycle.OnShow += HandleAppShow;
            _isAppShowSubscribed = true;
            PublishRequestResult(TTPlatformRequestResult.Success(
                TTPlatformOperation.SubscribeAppShow,
                "已注册 TTAppLifeCycle.OnShow。"));
        }
        catch (Exception exception)
        {
            _appLifeCycle = null;
            _isAppShowSubscribed = false;
            PublishRequestResult(TTPlatformRequestResult.Failure(
                TTPlatformOperation.SubscribeAppShow,
                $"注册 OnShow 时发生异常：{exception.Message}"));
        }
#else
        PublishRequestResult(TTPlatformRequestResult.Unavailable(
            TTPlatformOperation.SubscribeAppShow,
            "当前构建平台未启用 TTSDK 运行时；Editor 请改用 Simulated 模式。"));
#endif
    }

    /// <summary>
    /// 主动注销回前台事件。通常由 OnDestroy 自动完成，也可由业务流程显式调用。
    /// </summary>
    public void RequestUnsubscribeAppShow()
    {
        if (!_isAppShowSubscribed)
        {
            PublishRequestResult(TTPlatformRequestResult.Success(
                TTPlatformOperation.UnsubscribeAppShow,
                "OnShow 当前未注册。",
                ExecutionMode == TTPlatformExecutionMode.Simulated));
            return;
        }

        try
        {
            UnsubscribeAppShowWithoutResult();
            PublishRequestResult(TTPlatformRequestResult.Success(
                TTPlatformOperation.UnsubscribeAppShow,
                "已注销 OnShow 监听。",
                ExecutionMode == TTPlatformExecutionMode.Simulated));
        }
        catch (Exception exception)
        {
            PublishRequestResult(TTPlatformRequestResult.Failure(
                TTPlatformOperation.UnsubscribeAppShow,
                $"注销 OnShow 时发生异常：{exception.Message}"));
        }
    }

    /// <summary>
    /// 读取冷启动参数，补足“初始化完成前尚未注册 OnShow”可能漏掉的首次入口信息。
    /// </summary>
    public void RequestRefreshLaunchOptions()
    {
        if (!TryPrepareCapabilityRequest(
                TTPlatformOperation.RefreshLaunchOptions,
                requiresInitialization: true,
                out TTPlatformInvocationMode invocationMode,
                out TTPlatformRequestResult blockedResult))
        {
            PublishRequestResult(blockedResult);
            return;
        }

        if (invocationMode == TTPlatformInvocationMode.Simulated)
        {
            PublishRequestResult(TTPlatformRequestResult.Success(
                TTPlatformOperation.RefreshLaunchOptions,
                "模拟模式不生成冷启动参数；可调用 SimulateSidebarReturn 测试回流。",
                simulated: true));
            return;
        }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        try
        {
            LaunchOption launchOption = TT.GetLaunchOptionsSync();
            if (launchOption == null)
            {
                PublishRequestResult(TTPlatformRequestResult.Unavailable(
                    TTPlatformOperation.RefreshLaunchOptions,
                    "TT.GetLaunchOptionsSync 返回空。"));
                return;
            }

            string launchFrom = GetDictionaryString(
                launchOption.Extra,
                "launch_from",
                "launchFrom");
            string location = GetDictionaryString(launchOption.Extra, "location");
            string showFrom = GetDictionaryString(
                launchOption.Extra,
                "showFrom",
                "show_from");

            // 某些版本会把扩展字段放入 Query；仅在 Extra 缺失时兜底读取。
            if (string.IsNullOrEmpty(launchFrom))
                launchFrom = GetDictionaryString(
                    launchOption.Query,
                    "launch_from",
                    "launchFrom");
            if (string.IsNullOrEmpty(location))
                location = GetDictionaryString(launchOption.Query, "location");
            if (string.IsNullOrEmpty(showFrom))
                showFrom = GetDictionaryString(
                    launchOption.Query,
                    "showFrom",
                    "show_from");

            DispatchLaunchContext(new TTPlatformLaunchContext(
                launchOption.Scene,
                launchFrom,
                location,
                showFrom,
                initialLaunch: true));

            PublishRequestResult(TTPlatformRequestResult.Success(
                TTPlatformOperation.RefreshLaunchOptions,
                "已读取冷启动参数。"));
        }
        catch (Exception exception)
        {
            PublishRequestResult(TTPlatformRequestResult.Failure(
                TTPlatformOperation.RefreshLaunchOptions,
                $"读取冷启动参数时发生异常：{exception.Message}"));
        }
#else
        PublishRequestResult(TTPlatformRequestResult.Unavailable(
            TTPlatformOperation.RefreshLaunchOptions,
            "当前构建平台未启用 TTSDK 运行时；Editor 请改用 Simulated 模式。"));
#endif
    }

    /// <summary>
    /// 仅用于 Simulated 模式：模拟一次从首页侧边栏回到游戏的 OnShow。
    /// 不会调用 TTSDK，也不会直接发奖励。
    /// </summary>
    public void SimulateSidebarReturn()
    {
        if (ExecutionMode != TTPlatformExecutionMode.Simulated)
        {
            PublishRequestResult(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.SimulateSidebarReturn,
                "该方法只允许在 Simulated 模式使用。"));
            return;
        }

        if (!IsInitialized || !_isAppShowSubscribed)
        {
            PublishRequestResult(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.SimulateSidebarReturn,
                "请先完成模拟初始化并注册 OnShow。"));
            return;
        }

        DispatchLaunchContext(new TTPlatformLaunchContext(
            "021036",
            "homepage",
            "sidebar_card",
            string.Empty,
            initialLaunch: false));

        PublishRequestResult(TTPlatformRequestResult.Success(
            TTPlatformOperation.SimulateSidebarReturn,
            "已派发本地侧边栏回流参数，未调用 TTSDK。",
            simulated: true));
    }
    #endregion

    #region 统一能力门面
    /// <summary>检测当前宿主是否支持首页侧边栏。</summary>
    public void RequestCheckSidebarSupport()
    {
        if (TryGetSidebarService(out TTSidebarService service))
            service.RequestCheckSupport();
    }

    /// <summary>请求跳转首页侧边栏；跳转成功不等于任务完成。</summary>
    public void RequestNavigateToSidebar()
    {
        if (TryGetSidebarService(out TTSidebarService service))
            service.RequestNavigateToSidebar();
    }

    /// <summary>开始游戏录屏。</summary>
    public void RequestStartGameRecording()
    {
        if (TryGetGameRecorderService(out TTGameRecorderService service))
            service.RequestStartRecording();
    }

    /// <summary>停止游戏录屏并保存可分享视频。</summary>
    public void RequestStopGameRecording()
    {
        if (TryGetGameRecorderService(out TTGameRecorderService service))
            service.RequestStopRecording();
    }

    /// <summary>分享最近一次完成的录屏。</summary>
    public void RequestShareRecordedVideo()
    {
        if (TryGetGameRecorderService(out TTGameRecorderService service))
            service.RequestShareLastRecording();
    }

    /// <summary>清除项目层保存的最近录屏路径，不删除平台文件。</summary>
    public void RequestClearRecordedVideo()
    {
        if (TryGetGameRecorderService(out TTGameRecorderService service))
            service.ClearLastRecording();
    }
    #endregion

    #region 能力服务内部接口
    /// <summary>
    /// 供能力服务统一执行前置检查。Blocked 时 rejection 已包含可直接回传的原因。
    /// </summary>
    public bool TryPrepareCapabilityRequest(
        string operation,
        bool requiresInitialization,
        out TTPlatformInvocationMode invocationMode,
        out TTPlatformRequestResult rejection)
    {
        invocationMode = TTPlatformInvocationMode.Blocked;
        rejection = null;

        if (ExecutionMode == TTPlatformExecutionMode.Disabled)
        {
            rejection = TTPlatformRequestResult.Skipped(
                operation,
                "Execution Mode 为 Disabled，未调用 TTSDK。" );
            return false;
        }

        if (requiresInitialization && !IsInitialized)
        {
            rejection = TTPlatformRequestResult.Rejected(
                operation,
                "TTSDK 尚未初始化，请先调用 RequestInitialize。" );
            return false;
        }

        if (ExecutionMode == TTPlatformExecutionMode.Simulated)
        {
            invocationMode = TTPlatformInvocationMode.Simulated;
            return true;
        }

        if (!CanUseLiveSdk(operation, out rejection))
            return false;

        invocationMode = TTPlatformInvocationMode.Live;
        return true;
    }

    /// <summary>
    /// 能力服务将结果汇入总事件。公开此方法是为了后续业务适配器也能复用统一结果通道。
    /// </summary>
    public void PublishRequestResult(TTPlatformRequestResult result)
    {
        if (result == null)
            return;

        if (logRequestResults)
        {
            if (result.Status == TTPlatformRequestStatus.Failed)
                Debug.LogError($"[TTPlatformManager] {result}", this);
            else if (result.Status == TTPlatformRequestStatus.Rejected ||
                     result.Status == TTPlatformRequestStatus.Unavailable)
                Debug.LogWarning($"[TTPlatformManager] {result}", this);
            else
                Debug.Log($"[TTPlatformManager] {result}", this);
        }

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
    #endregion

    #region 私有实现
    private void PrepareLifecycleIfNeeded()
    {
        if (!prepareLifecycleAfterInitialization)
            return;

        RequestSubscribeAppShow();
    }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
    private void PrepareLiveLifecycleIfNeeded(ContainerEnv containerEnvironment)
    {
        if (!prepareLifecycleAfterInitialization)
            return;

        // 先监听后读取冷启动参数，避免两者之间发生回前台事件时漏记最新参数。
        RequestSubscribeAppShow();

        if (containerEnvironment == null)
        {
            RequestRefreshLaunchOptions();
            return;
        }

        try
        {
            LaunchOption launchOption = containerEnvironment.GetLaunchOptionsSync();
            DispatchLaunchContext(new TTPlatformLaunchContext(
                launchOption?.Scene,
                containerEnvironment.GetLaunchFrom(),
                containerEnvironment.GetLocation(),
                string.Empty,
                initialLaunch: true));
        }
        catch (Exception exception)
        {
            PublishRequestResult(TTPlatformRequestResult.Failure(
                TTPlatformOperation.RefreshLaunchOptions,
                $"从 ContainerEnv 读取冷启动参数时发生异常：{exception.Message}"));
        }
    }
#endif

    private bool CanUseLiveSdk(
        string operation,
        out TTPlatformRequestResult rejection)
    {
        rejection = null;

        if (ExecutionMode != TTPlatformExecutionMode.Live)
        {
            rejection = TTPlatformRequestResult.Rejected(
                operation,
                "当前不是 Live 模式。" );
            return false;
        }

#if UNITY_EDITOR || !(UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        rejection = TTPlatformRequestResult.Unavailable(
            operation,
            "当前构建平台未启用 TTSDK 运行时；Editor 请使用 Simulated 模式。" );
        return false;
#else
        return true;
#endif
    }

    private void SetInitializationState(TTPlatformInitializationState state)
    {
        if (_initializationState == state)
            return;

        _initializationState = state;
        TTPlatformEventUtility.InvokeSafely(
            InitializationStateChanged,
            state,
            this,
            nameof(InitializationStateChanged));
        TTPlatformEventUtility.InvokeSafely(
            initializationStateChanged,
            state,
            this,
            nameof(initializationStateChanged));
    }

    private void HandleAppShow(Dictionary<string, object> launchData)
    {
        DispatchLaunchContext(new TTPlatformLaunchContext(
            GetObjectDictionaryString(launchData, "scene"),
            GetObjectDictionaryString(launchData, "launch_from", "launchFrom"),
            GetObjectDictionaryString(launchData, "location"),
            GetObjectDictionaryString(launchData, "showFrom", "show_from"),
            initialLaunch: false));
    }

    private void DispatchLaunchContext(TTPlatformLaunchContext context)
    {
        if (context == null)
            return;

        _latestLaunchContext = context;
        TTPlatformEventUtility.InvokeSafely(
            AppShown,
            context,
            this,
            nameof(AppShown));
        TTPlatformEventUtility.InvokeSafely(
            appShown,
            context,
            this,
            nameof(appShown));
    }

    private void UnsubscribeAppShowWithoutResult()
    {
        if (!_isAppShowSubscribed)
            return;

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        // 是否需要注销取决于曾经是否建立了真实订阅，而不是当前 Inspector 模式值。
        if (_appLifeCycle != null)
        {
            _appLifeCycle.OnShow -= HandleAppShow;
        }

        _appLifeCycle = null;
#endif
        _isAppShowSubscribed = false;
    }

    private void ResolveCapabilityServices()
    {
        if (sidebarService == null)
            sidebarService = GetComponent<TTSidebarService>();

        if (gameRecorderService == null)
            gameRecorderService = GetComponent<TTGameRecorderService>();
    }

    private bool TryGetSidebarService(out TTSidebarService service)
    {
        ResolveCapabilityServices();
        service = sidebarService;
        if (service != null)
            return true;

        PublishRequestResult(TTPlatformRequestResult.Unavailable(
            TTPlatformOperation.CheckSidebarSupport,
            "未找到 TTSidebarService，请把它挂到 TTPlatformManager 所在对象。"));
        return false;
    }

    private bool TryGetGameRecorderService(out TTGameRecorderService service)
    {
        ResolveCapabilityServices();
        service = gameRecorderService;
        if (service != null)
            return true;

        PublishRequestResult(TTPlatformRequestResult.Unavailable(
            TTPlatformOperation.StartGameRecording,
            "未找到 TTGameRecorderService，请把它挂到 TTPlatformManager 所在对象。"));
        return false;
    }

    private static string GetObjectDictionaryString(
        IReadOnlyDictionary<string, object> dictionary,
        params string[] keys)
    {
        if (dictionary == null || keys == null)
            return string.Empty;

        foreach (string key in keys)
        {
            if (string.IsNullOrEmpty(key) ||
                !dictionary.TryGetValue(key, out object value) ||
                value == null)
            {
                continue;
            }

            return Convert.ToString(value) ?? string.Empty;
        }

        return string.Empty;
    }

    private static string GetDictionaryString(
        IReadOnlyDictionary<string, string> dictionary,
        params string[] keys)
    {
        if (dictionary == null || keys == null)
            return string.Empty;

        foreach (string key in keys)
        {
            if (!string.IsNullOrEmpty(key) &&
                dictionary.TryGetValue(key, out string value) &&
                value != null)
            {
                return value;
            }
        }

        return string.Empty;
    }
    #endregion
}
