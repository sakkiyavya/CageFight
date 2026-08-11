using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// TTSDK 的执行模式。
/// 默认使用 Disabled，组件即使被挂进场景、按钮即使被误点，也不会调用平台 API。
/// </summary>
public enum TTPlatformExecutionMode
{
    Disabled = 0,
    Simulated = 1,
    Live = 2
}

/// <summary>
/// TTSDK 初始化状态。
/// </summary>
public enum TTPlatformInitializationState
{
    NotInitialized = 0,
    Initializing = 1,
    Initialized = 2,
    Failed = 3
}

/// <summary>
/// 一次平台请求的统一结果状态。
/// </summary>
public enum TTPlatformRequestStatus
{
    Succeeded = 0,
    Skipped = 1,
    Unavailable = 2,
    Rejected = 3,
    Failed = 4,
    Cancelled = 5
}

/// <summary>
/// 能力服务在发起请求前得到的执行决策。
/// </summary>
public enum TTPlatformInvocationMode
{
    Blocked = 0,
    Simulated = 1,
    Live = 2
}

/// <summary>
/// 侧边栏能力检测状态。Unknown 表示尚未完成检测，不能据此展示入口。
/// </summary>
public enum TTSidebarSupportState
{
    Unknown = 0,
    Supported = 1,
    Unsupported = 2
}

/// <summary>
/// 游戏录屏流程状态。
/// </summary>
public enum TTGameRecordingState
{
    Idle = 0,
    Starting = 1,
    Recording = 2,
    Stopping = 3,
    ReadyToShare = 4,
    Sharing = 5,
    Failed = 6
}

/// <summary>
/// 供 TTSDKRequestRelay 选择的无参数请求类型。
/// 可从 UGUI Button、动画事件、Timeline Signal 或其他 UnityEvent 调用。
/// </summary>
public enum TTPlatformRequestType
{
    Initialize = 0,
    SubscribeAppShow = 1,
    RefreshLaunchOptions = 2,
    CheckSidebarSupport = 3,
    StartGameRecording = 4,
    StopGameRecording = 5,
    ClearRecordedVideo = 6,
    SimulateSidebarReturn = 7
}

/// <summary>
/// 项目层的平台请求结果。业务代码只依赖本类型，不需要依赖厂商回调类型。
/// </summary>
[Serializable]
public sealed class TTPlatformRequestResult
{
    [SerializeField] private string operation;
    [SerializeField] private TTPlatformRequestStatus status;
    [SerializeField] private int errorCode;
    [SerializeField] private string message;
    [SerializeField] private bool simulated;

    public string Operation => operation;
    public TTPlatformRequestStatus Status => status;
    public int ErrorCode => errorCode;
    public string Message => message;
    public bool IsSimulated => simulated;
    public bool IsSuccess => status == TTPlatformRequestStatus.Succeeded;

    public TTPlatformRequestResult(
        string operation,
        TTPlatformRequestStatus status,
        string message,
        int errorCode = 0,
        bool simulated = false)
    {
        this.operation = operation ?? string.Empty;
        this.status = status;
        this.errorCode = errorCode;
        this.message = message ?? string.Empty;
        this.simulated = simulated;
    }

    public static TTPlatformRequestResult Success(
        string operation,
        string message,
        bool simulated = false)
    {
        return new TTPlatformRequestResult(
            operation,
            TTPlatformRequestStatus.Succeeded,
            message,
            simulated: simulated);
    }

    public static TTPlatformRequestResult Skipped(string operation, string message)
    {
        return new TTPlatformRequestResult(
            operation,
            TTPlatformRequestStatus.Skipped,
            message);
    }

    public static TTPlatformRequestResult Unavailable(string operation, string message)
    {
        return new TTPlatformRequestResult(
            operation,
            TTPlatformRequestStatus.Unavailable,
            message);
    }

    public static TTPlatformRequestResult Rejected(string operation, string message)
    {
        return new TTPlatformRequestResult(
            operation,
            TTPlatformRequestStatus.Rejected,
            message);
    }

    public static TTPlatformRequestResult Failure(
        string operation,
        string message,
        int errorCode = -1)
    {
        return new TTPlatformRequestResult(
            operation,
            TTPlatformRequestStatus.Failed,
            message,
            errorCode);
    }

    public static TTPlatformRequestResult Cancelled(string operation, string message)
    {
        return new TTPlatformRequestResult(
            operation,
            TTPlatformRequestStatus.Cancelled,
            message);
    }

    public override string ToString()
    {
        string simulationLabel = simulated ? "，模拟" : string.Empty;
        string errorLabel = errorCode == 0 ? string.Empty : $"，错误码：{errorCode}";
        return $"{operation} -> {status}{simulationLabel}{errorLabel}：{message}";
    }
}

/// <summary>
/// 项目层的启动/回前台参数快照。
/// 为避免业务层持有厂商 Dictionary，只保留侧边栏判断所需字段。
/// </summary>
[Serializable]
public sealed class TTPlatformLaunchContext
{
    [SerializeField] private string scene;
    [SerializeField] private string launchFrom;
    [SerializeField] private string location;
    [SerializeField] private string showFrom;
    [SerializeField] private bool initialLaunch;

    public string Scene => scene;
    public string LaunchFrom => launchFrom;
    public string Location => location;
    public string ShowFrom => showFrom;
    public bool IsInitialLaunch => initialLaunch;

    /// <summary>
    /// 只按官方返回的 homepage + sidebar_card 判断可发奖的侧边栏回流。
    /// scene=1036 还可能对应其他入口，不能单独作为任务完成依据。
    /// </summary>
    public bool IsSidebarEntry
    {
        get
        {
            return string.Equals(
                       launchFrom,
                       "homepage",
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       location,
                       "sidebar_card",
                       StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// 仅供日志/诊断使用的场景候选，不得用于发奖或任务完成判断。
    /// </summary>
    public bool HasSidebarSceneSuffix =>
        !string.IsNullOrWhiteSpace(scene) &&
        scene.EndsWith("1036", StringComparison.Ordinal);

    public TTPlatformLaunchContext(
        string scene,
        string launchFrom,
        string location,
        string showFrom,
        bool initialLaunch)
    {
        this.scene = scene ?? string.Empty;
        this.launchFrom = launchFrom ?? string.Empty;
        this.location = location ?? string.Empty;
        this.showFrom = showFrom ?? string.Empty;
        this.initialLaunch = initialLaunch;
    }

    public override string ToString()
    {
        return $"scene={scene}, launchFrom={launchFrom}, location={location}, " +
               $"showFrom={showFrom}, initial={initialLaunch}, sidebar={IsSidebarEntry}, " +
               $"sceneCandidate={HasSidebarSceneSuffix}";
    }
}

// 下列具名 UnityEvent 让 Inspector 能稳定序列化自定义参数事件。
[Serializable]
public sealed class TTPlatformRequestResultUnityEvent : UnityEvent<TTPlatformRequestResult>
{
}

[Serializable]
public sealed class TTPlatformLaunchContextUnityEvent : UnityEvent<TTPlatformLaunchContext>
{
}

[Serializable]
public sealed class TTPlatformBoolUnityEvent : UnityEvent<bool>
{
}

[Serializable]
public sealed class TTPlatformInitializationStateUnityEvent : UnityEvent<TTPlatformInitializationState>
{
}

[Serializable]
public sealed class TTGameRecordingStateUnityEvent : UnityEvent<TTGameRecordingState>
{
}

/// <summary>
/// 统一操作名，便于日志、埋点或调试面板按稳定字符串分类。
/// </summary>
internal static class TTPlatformOperation
{
    public const string Initialize = "TTSDK.Initialize";
    public const string SubscribeAppShow = "TTSDK.SubscribeAppShow";
    public const string UnsubscribeAppShow = "TTSDK.UnsubscribeAppShow";
    public const string RefreshLaunchOptions = "TTSDK.RefreshLaunchOptions";
    public const string SimulateSidebarReturn = "TTSDK.SimulateSidebarReturn";
    public const string CheckSidebarSupport = "TTSDK.CheckSidebarSupport";
    public const string NavigateToSidebar = "TTSDK.NavigateToSidebar";
    public const string StartGameRecording = "TTSDK.StartGameRecording";
    public const string StopGameRecording = "TTSDK.StopGameRecording";
    public const string RecordingAutoCompleted = "TTSDK.RecordingAutoCompleted";
    public const string ShareRecordedVideo = "TTSDK.ShareRecordedVideo";
    public const string ClearRecordedVideo = "TTSDK.ClearRecordedVideo";
}

/// <summary>
/// 平台回调不能被某个业务监听器的异常打断；否则可能出现“内部已成功，但后续监听/清理未执行”。
/// </summary>
internal static class TTPlatformEventUtility
{
    public static void InvokeSafely<T>(
        Action<T> handlers,
        T argument,
        UnityEngine.Object context,
        string eventName)
    {
        if (handlers == null)
            return;

        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<T>)handler).Invoke(argument);
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[TTPlatform] 监听器在 {eventName} 中抛出异常：{exception}",
                    context);
            }
        }
    }

    public static void InvokeSafely<T>(
        UnityEvent<T> unityEvent,
        T argument,
        UnityEngine.Object context,
        string eventName)
    {
        if (unityEvent == null)
            return;

        try
        {
            unityEvent.Invoke(argument);
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[TTPlatform] Inspector 监听器在 {eventName} 中抛出异常：{exception}",
                context);
        }
    }
}
