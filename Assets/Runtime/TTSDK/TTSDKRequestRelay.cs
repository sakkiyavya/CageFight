using UnityEngine;

/// <summary>
/// 无厂商依赖的请求转发器。
///
/// 用法一：为组件选择 Configured Request，把 Button.onClick 绑定到 InvokeConfiguredRequest；
/// 用法二：直接绑定下方任意一个具名 public 方法；
/// 用法三：由动画事件、Timeline Signal、关卡流程或其他代码调用非交互请求。
///
/// 本组件不保存业务状态，也不直接访问 TTSDK。真实调用是否允许完全由
/// TTPlatformManager.ExecutionMode 控制；默认 Disabled 因此误触也不会调用平台。
/// Navigate / Share 必须来自用户主动点击，所以不放入通用 Configured Request 枚举；
/// 请只把对应的 FromUserClick 方法绑定到真实可见按钮的 onClick。
/// </summary>
[DisallowMultipleComponent]
public sealed class TTSDKRequestRelay : MonoBehaviour
{
    [SerializeField] private TTPlatformManager platformManager;

    [Tooltip("InvokeConfiguredRequest 被调用时要转发的请求。")]
    [SerializeField] private TTPlatformRequestType configuredRequest =
        TTPlatformRequestType.Initialize;

    public TTPlatformRequestType ConfiguredRequest => configuredRequest;

    private void Reset()
    {
        ResolveManager();
    }

    /// <summary>
    /// 按 Inspector 中的 Configured Request 转发一次无参数请求。
    /// </summary>
    public void InvokeConfiguredRequest()
    {
        if (!TryGetManager(out TTPlatformManager manager))
            return;

        switch (configuredRequest)
        {
            case TTPlatformRequestType.Initialize:
                manager.RequestInitialize();
                break;
            case TTPlatformRequestType.SubscribeAppShow:
                manager.RequestSubscribeAppShow();
                break;
            case TTPlatformRequestType.RefreshLaunchOptions:
                manager.RequestRefreshLaunchOptions();
                break;
            case TTPlatformRequestType.CheckSidebarSupport:
                manager.RequestCheckSidebarSupport();
                break;
            case TTPlatformRequestType.StartGameRecording:
                manager.RequestStartGameRecording();
                break;
            case TTPlatformRequestType.StopGameRecording:
                manager.RequestStopGameRecording();
                break;
            case TTPlatformRequestType.ClearRecordedVideo:
                manager.RequestClearRecordedVideo();
                break;
            case TTPlatformRequestType.SimulateSidebarReturn:
                manager.SimulateSidebarReturn();
                break;
            default:
                Debug.LogWarning(
                    $"[TTSDKRequestRelay] 未处理的请求类型：{configuredRequest}。",
                    this);
                break;
        }
    }

    // 具名方法方便在 Button.onClick 的函数列表中直接选择。
    public void RequestInitialize() => Invoke(manager => manager.RequestInitialize());
    public void RequestSubscribeAppShow() => Invoke(manager => manager.RequestSubscribeAppShow());
    public void RequestUnsubscribeAppShow() => Invoke(manager => manager.RequestUnsubscribeAppShow());
    public void RequestRefreshLaunchOptions() => Invoke(manager => manager.RequestRefreshLaunchOptions());
    public void RequestCheckSidebarSupport() => Invoke(manager => manager.RequestCheckSidebarSupport());
    public void RequestStartGameRecording() => Invoke(manager => manager.RequestStartGameRecording());
    public void RequestStopGameRecording() => Invoke(manager => manager.RequestStopGameRecording());
    public void RequestClearRecordedVideo() => Invoke(manager => manager.RequestClearRecordedVideo());
    public void SimulateSidebarReturn() => Invoke(manager => manager.SimulateSidebarReturn());

    /// <summary>仅绑定真实可见按钮的 onClick，不要从动画、计时器或自动流程调用。</summary>
    public void RequestNavigateToSidebarFromUserClick() =>
        Invoke(manager => manager.RequestNavigateToSidebar());

    /// <summary>仅绑定真实可见按钮的 onClick；平台要求分享由用户主动触发。</summary>
    public void RequestShareRecordedVideoFromUserClick() =>
        Invoke(manager => manager.RequestShareRecordedVideo());

    private void Invoke(System.Action<TTPlatformManager> request)
    {
        if (request == null || !TryGetManager(out TTPlatformManager manager))
            return;

        request.Invoke(manager);
    }

    private bool TryGetManager(out TTPlatformManager manager)
    {
        ResolveManager();
        manager = platformManager;
        if (manager != null)
            return true;

        Debug.LogWarning(
            "[TTSDKRequestRelay] 未找到 TTPlatformManager，无法转发请求。",
            this);
        return false;
    }

    private void ResolveManager()
    {
        if (platformManager == null)
            platformManager = GetComponentInParent<TTPlatformManager>(includeInactive: true);

        if (platformManager == null)
            platformManager = TTPlatformManager.Instance;
    }
}
