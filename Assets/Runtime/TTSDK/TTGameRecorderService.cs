using System;
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
using TTSDK;
#endif
using UnityEngine;

/// <summary>
/// 游戏录屏与视频分享封装。
///
/// 本项目使用的 TTSDK 6.7.3 实际接口是：
/// TT.GetGameRecorder() -> TTGameRecorder.Start / Stop / ShareVideo。
/// 官网其他版本中出现的 GetGameRecorderManager 并不适用于当前插件。
///
/// 录屏生命周期应由玩法流程驱动（开战时 Start、结算前 Stop）；
/// 分享必须由用户主动点击触发，不能在 Stop 成功后自动弹出发布页。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TTPlatformManager))]
public sealed class TTGameRecorderService : MonoBehaviour
{
    private const float PlatformMinimumShareSeconds = 3.2f;

    [Header("依赖")]
    [SerializeField] private TTPlatformManager platformManager;

    [Header("录制设置")]
    [Tooltip("是否录制游戏声音。不是麦克风授权开关。")]
    [SerializeField] private bool recordAudio = true;

    [Tooltip("单段最长录制时间（秒）。审核建议最长不超过 300 秒。")]
    [SerializeField, Min(1)] private int maxRecordTimeSeconds = 300;

    [Tooltip("Start / Stop 被受理后等待回调的超时时间。分享交互不使用短超时。")]
    [SerializeField, Min(1f)] private float recordCallbackTimeoutSeconds = 30f;

    [Tooltip("分享页可能停留很久；只在超过该时长后把项目层恢复为可再次分享。")]
    [SerializeField, Min(60f)] private float shareCallbackTimeoutSeconds = 600f;

    [Header("Inspector 事件")]
    [SerializeField] private TTGameRecordingStateUnityEvent
        recordingStateChanged = new TTGameRecordingStateUnityEvent();
    [SerializeField] private TTPlatformRequestResultUnityEvent
        requestCompleted = new TTPlatformRequestResultUnityEvent();

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
    // TTSDK DLL 在当前项目中不参与 Editor 编译，厂商类型只能存在于 Player 条件区。
    private TTGameRecorder _recorder;
#endif

    private TTGameRecordingState _state = TTGameRecordingState.Idle;
    private string _lastVideoPath = string.Empty;
    private double _recordingRequestRealtime;
    private double _recordingStartedRealtime;
    private double _lastRecordingDurationSeconds;
    private double _callbackDeadlineRealtime;
    private double _shareDeadlineRealtime;
    private int _recordingSessionGeneration;
    private int _shareGeneration;

    public TTGameRecordingState State => _state;
    public string LastVideoPath => _lastVideoPath;
    public double LastRecordingDurationSeconds => _lastRecordingDurationSeconds;
    public bool HasShareableVideo =>
        _state == TTGameRecordingState.ReadyToShare &&
        !string.IsNullOrWhiteSpace(_lastVideoPath);

    /// <summary>录屏状态改变时触发。</summary>
    public event Action<TTGameRecordingState> RecordingStateChanged;

    /// <summary>本服务请求完成时触发。</summary>
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

    private void Update()
    {
        double now = Time.realtimeSinceStartupAsDouble;

        if ((_state == TTGameRecordingState.Starting ||
             _state == TTGameRecordingState.Stopping) &&
            _callbackDeadlineRealtime > 0d &&
            now >= _callbackDeadlineRealtime)
        {
            string operation = _state == TTGameRecordingState.Starting
                ? TTPlatformOperation.StartGameRecording
                : TTPlatformOperation.StopGameRecording;

            // 不废弃 generation，也不改成可重试状态：真实请求可能已经执行，迟到回调仍需收口。
            _callbackDeadlineRealtime = 0d;
            if (TryReconcileRecordStateAfterTimeout(operation))
                return;

            Complete(TTPlatformRequestResult.Failure(
                operation,
                $"等待回调超过 {recordCallbackTimeoutSeconds:0.#} 秒；仍会接收迟到回调，请勿重复开始录屏。"));
        }

        if (_state == TTGameRecordingState.Sharing &&
            _shareDeadlineRealtime > 0d &&
            now >= _shareDeadlineRealtime)
        {
            // 分享交互不给短超时；极长时间无回调时只恢复本地 UI，保留视频供用户重试。
            _shareDeadlineRealtime = 0d;
            _shareGeneration++;
            SetState(TTGameRecordingState.ReadyToShare);
            Complete(TTPlatformRequestResult.Failure(
                TTPlatformOperation.ShareRecordedVideo,
                $"等待分享结果超过 {shareCallbackTimeoutSeconds / 60f:0.#} 分钟，已恢复为可分享状态。"));
        }
    }

    private void OnDestroy()
    {
        // 临时 Disable 时仍接收平台回调；只有对象真正销毁后才让旧回调失效。
        _recordingSessionGeneration++;
        _shareGeneration++;
        _callbackDeadlineRealtime = 0d;
        _shareDeadlineRealtime = 0d;
    }

    private void OnValidate()
    {
        maxRecordTimeSeconds = Mathf.Clamp(maxRecordTimeSeconds, 1, 300);
        recordCallbackTimeoutSeconds = Mathf.Max(1f, recordCallbackTimeoutSeconds);
        shareCallbackTimeoutSeconds = Mathf.Max(60f, shareCallbackTimeoutSeconds);
    }
    #endregion

    #region 对外请求
    /// <summary>
    /// 开始录屏。适合由 GameplayState / 战斗流程调用，不建议让 UI 按钮持有录屏生命周期。
    /// </summary>
    public void RequestStartRecording()
    {
        if (_state == TTGameRecordingState.Starting ||
            _state == TTGameRecordingState.Recording ||
            _state == TTGameRecordingState.Stopping ||
            _state == TTGameRecordingState.Sharing)
        {
            Complete(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.StartGameRecording,
                $"当前录屏状态为 {_state}，不能重复开始。"));
            return;
        }

        if (!TryPrepareRequest(
                TTPlatformOperation.StartGameRecording,
                out TTPlatformInvocationMode invocationMode,
                out TTPlatformRequestResult blockedResult))
        {
            Complete(blockedResult);
            return;
        }

        // 新一局开始时放弃项目层对旧视频的引用；不会删除平台文件。
        _lastVideoPath = string.Empty;
        _lastRecordingDurationSeconds = 0d;
        _recordingRequestRealtime = Time.realtimeSinceStartupAsDouble;
        _recordingStartedRealtime = 0d;

        int sessionGeneration = ++_recordingSessionGeneration;
        SetState(TTGameRecordingState.Starting);
        ArmRecordCallbackTimeout();

        if (invocationMode == TTPlatformInvocationMode.Simulated)
        {
            HandleRecordingStarted(sessionGeneration, simulated: true);
            return;
        }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        try
        {
            _recorder ??= TT.GetGameRecorder();
            if (_recorder == null)
            {
                HandleRecordingError(
                    sessionGeneration,
                    TTPlatformOperation.StartGameRecording,
                    -1,
                    "TT.GetGameRecorder 返回空。" );
                return;
            }

            bool accepted = _recorder.Start(
                recordAudio,
                maxRecordTimeSeconds,
                () => HandleRecordingStarted(sessionGeneration, simulated: false),
                (errorCode, errorMessage) => HandleRecordingError(
                    sessionGeneration,
                    TTPlatformOperation.StartGameRecording,
                    errorCode,
                    errorMessage),
                videoPath => HandleRecordingAutoCompleted(
                    sessionGeneration,
                    videoPath));

            // TTGameRecorder 的 bool 只表示调用是否受理；false 时不保证还有异步回调。
            if (!accepted && _state == TTGameRecordingState.Starting)
            {
                HandleRecordingError(
                    sessionGeneration,
                    TTPlatformOperation.StartGameRecording,
                    -1,
                    "TTGameRecorder.Start 未受理请求。" );
            }
        }
        catch (Exception exception)
        {
            HandleRecordingError(
                sessionGeneration,
                TTPlatformOperation.StartGameRecording,
                -1,
                exception.Message);
        }
#else
        HandleRecordingError(
            sessionGeneration,
            TTPlatformOperation.StartGameRecording,
            -1,
            "当前构建平台未启用 TTSDK 运行时。" );
#endif
    }

    /// <summary>
    /// 停止当前录屏。只有拿到非空 videoPath 后，状态才会进入 ReadyToShare。
    /// </summary>
    public void RequestStopRecording()
    {
        if (_state != TTGameRecordingState.Recording)
        {
            Complete(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.StopGameRecording,
                $"当前录屏状态为 {_state}，没有可停止的录制。"));
            return;
        }

        if (!TryPrepareRequest(
                TTPlatformOperation.StopGameRecording,
                out TTPlatformInvocationMode invocationMode,
                out TTPlatformRequestResult blockedResult))
        {
            Complete(blockedResult);
            return;
        }

        int sessionGeneration = _recordingSessionGeneration;
        _lastRecordingDurationSeconds = Math.Max(
            0d,
            Time.realtimeSinceStartupAsDouble - _recordingStartedRealtime);
        SetState(TTGameRecordingState.Stopping);
        ArmRecordCallbackTimeout();

        if (invocationMode == TTPlatformInvocationMode.Simulated)
        {
            // 让模拟视频满足平台分享最短时长，便于完整测试 UI 流程。
            _lastRecordingDurationSeconds = Math.Max(
                _lastRecordingDurationSeconds,
                PlatformMinimumShareSeconds);
            HandleRecordingStopped(
                sessionGeneration,
                "simulation://ttsdk/last-recording.mp4",
                simulated: true);
            return;
        }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        try
        {
            if (_recorder == null)
            {
                HandleRecordingError(
                    sessionGeneration,
                    TTPlatformOperation.StopGameRecording,
                    -1,
                    "录屏实例为空。" );
                return;
            }

            bool accepted = _recorder.Stop(
                videoPath => HandleRecordingStopped(
                    sessionGeneration,
                    videoPath,
                    simulated: false),
                (errorCode, errorMessage) => HandleRecordingError(
                    sessionGeneration,
                    TTPlatformOperation.StopGameRecording,
                    errorCode,
                    errorMessage),
                clipRanges: null,
                autoMerge: true);

            if (!accepted && _state == TTGameRecordingState.Stopping)
            {
                HandleRecordingError(
                    sessionGeneration,
                    TTPlatformOperation.StopGameRecording,
                    -1,
                    "TTGameRecorder.Stop 未受理请求。" );
            }
        }
        catch (Exception exception)
        {
            HandleRecordingError(
                sessionGeneration,
                TTPlatformOperation.StopGameRecording,
                -1,
                exception.Message);
        }
#else
        HandleRecordingError(
            sessionGeneration,
            TTPlatformOperation.StopGameRecording,
            -1,
            "当前构建平台未启用 TTSDK 运行时。" );
#endif
    }

    /// <summary>
    /// 分享最近一次录屏。应仅从明确的用户点击回调调用。
    /// 平台技术限制约为 3 秒；这里使用 3.2 秒安全阈值。审核流程通常还要求更完整的对局片段。
    /// </summary>
    public void RequestShareLastRecording()
    {
        if (!HasShareableVideo)
        {
            Complete(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.ShareRecordedVideo,
                "当前没有已完成且可分享的录屏。"));
            return;
        }

        if (_lastRecordingDurationSeconds < PlatformMinimumShareSeconds)
        {
            Complete(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.ShareRecordedVideo,
                $"录屏仅 {_lastRecordingDurationSeconds:0.0} 秒，短于平台安全阈值 {PlatformMinimumShareSeconds:0.0} 秒。"));
            return;
        }

        if (!TryPrepareRequest(
                TTPlatformOperation.ShareRecordedVideo,
                out TTPlatformInvocationMode invocationMode,
                out TTPlatformRequestResult blockedResult))
        {
            Complete(blockedResult);
            return;
        }

        int shareGeneration = ++_shareGeneration;
        SetState(TTGameRecordingState.Sharing);
        _shareDeadlineRealtime =
            Time.realtimeSinceStartupAsDouble + shareCallbackTimeoutSeconds;

        if (invocationMode == TTPlatformInvocationMode.Simulated)
        {
            HandleShareSuccess(shareGeneration, simulated: true);
            return;
        }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        try
        {
            if (_recorder == null)
            {
                HandleShareFailure(shareGeneration, "录屏实例为空。" );
                return;
            }

            bool accepted = _recorder.ShareVideo(
                result => HandleShareSuccess(shareGeneration, simulated: false),
                errorMessage => HandleShareFailure(shareGeneration, errorMessage),
                () => HandleShareCancelled(shareGeneration));

            if (!accepted && _state == TTGameRecordingState.Sharing)
                HandleShareFailure(shareGeneration, "TTGameRecorder.ShareVideo 未受理请求。" );
        }
        catch (Exception exception)
        {
            HandleShareFailure(shareGeneration, exception.Message);
        }
#else
        HandleShareFailure(shareGeneration, "当前构建平台未启用 TTSDK 运行时。" );
#endif
    }

    /// <summary>
    /// 清除项目层保存的视频信息，不删除 TTSDK 生成的物理文件，也不会调用平台 API。
    /// </summary>
    public void ClearLastRecording()
    {
        if (_state == TTGameRecordingState.Starting ||
            _state == TTGameRecordingState.Recording ||
            _state == TTGameRecordingState.Stopping ||
            _state == TTGameRecordingState.Sharing)
        {
            Complete(TTPlatformRequestResult.Rejected(
                TTPlatformOperation.ClearRecordedVideo,
                $"当前状态为 {_state}，不能清除录屏信息。"));
            return;
        }

        _lastVideoPath = string.Empty;
        _lastRecordingDurationSeconds = 0d;
        _recordingRequestRealtime = 0d;
        _recordingStartedRealtime = 0d;
        _shareDeadlineRealtime = 0d;
        SetState(TTGameRecordingState.Idle);
        Complete(TTPlatformRequestResult.Success(
            TTPlatformOperation.ClearRecordedVideo,
            "已清除项目层的最近录屏信息；未调用 TTSDK，也未删除平台文件。"));
    }
    #endregion

    #region 回调处理
    private void HandleRecordingStarted(int sessionGeneration, bool simulated)
    {
        if (!IsCurrentSession(sessionGeneration) ||
            _state != TTGameRecordingState.Starting)
        {
            return;
        }

        _callbackDeadlineRealtime = 0d;
        _recordingStartedRealtime = Time.realtimeSinceStartupAsDouble;
        SetState(TTGameRecordingState.Recording);
        Complete(TTPlatformRequestResult.Success(
            TTPlatformOperation.StartGameRecording,
            simulated
                ? $"已开始本地模拟录屏（录制游戏声音：{recordAudio}）。"
                : "TTGameRecorder 已开始录屏。",
            simulated));
    }

    private void HandleRecordingAutoCompleted(int sessionGeneration, string videoPath)
    {
        if (!IsCurrentSession(sessionGeneration) ||
            (_state != TTGameRecordingState.Starting &&
             _state != TTGameRecordingState.Recording &&
             _state != TTGameRecordingState.Stopping))
        {
            return;
        }

        _lastRecordingDurationSeconds = Math.Max(
            0d,
            Time.realtimeSinceStartupAsDouble - GetRecordingStartRealtime());
        FinishRecording(
            TTPlatformOperation.RecordingAutoCompleted,
            videoPath,
            simulated: false);
    }

    private void HandleRecordingStopped(
        int sessionGeneration,
        string videoPath,
        bool simulated)
    {
        if (!IsCurrentSession(sessionGeneration) ||
            _state != TTGameRecordingState.Stopping)
        {
            return;
        }

        FinishRecording(
            TTPlatformOperation.StopGameRecording,
            videoPath,
            simulated);
    }

    private void FinishRecording(string operation, string videoPath, bool simulated)
    {
        _callbackDeadlineRealtime = 0d;

        if (string.IsNullOrWhiteSpace(videoPath))
        {
            _lastVideoPath = string.Empty;
            SetState(TTGameRecordingState.Failed);
            Complete(TTPlatformRequestResult.Failure(
                operation,
                "录屏回调没有返回有效 videoPath，不能进入分享流程。"));
            return;
        }

        _lastVideoPath = videoPath;
        SetState(TTGameRecordingState.ReadyToShare);
        Complete(TTPlatformRequestResult.Success(
            operation,
            simulated ? "本地模拟录屏已完成。" : "录屏已完成，可以由用户主动分享。",
            simulated));
    }

    private void HandleRecordingError(
        int sessionGeneration,
        string operation,
        int errorCode,
        string errorMessage)
    {
        if (!IsCurrentSession(sessionGeneration) ||
            (_state != TTGameRecordingState.Starting &&
             _state != TTGameRecordingState.Recording &&
             _state != TTGameRecordingState.Stopping))
        {
            return;
        }

        _callbackDeadlineRealtime = 0d;
        SetState(TTGameRecordingState.Failed);
        Complete(TTPlatformRequestResult.Failure(
            operation,
            string.IsNullOrWhiteSpace(errorMessage) ? "录屏请求失败。" : errorMessage,
            errorCode));
    }

    private void HandleShareSuccess(int shareGeneration, bool simulated)
    {
        if (!IsCurrentShare(shareGeneration))
            return;

        _shareDeadlineRealtime = 0d;
        // 保留视频路径，允许业务决定是否展示再次分享按钮。
        SetState(TTGameRecordingState.ReadyToShare);
        Complete(TTPlatformRequestResult.Success(
            TTPlatformOperation.ShareRecordedVideo,
            simulated ? "已完成本地模拟分享。" : "录屏视频分享成功。",
            simulated));
    }

    private void HandleShareFailure(int shareGeneration, string errorMessage)
    {
        if (!IsCurrentShare(shareGeneration))
            return;

        _shareDeadlineRealtime = 0d;
        SetState(TTGameRecordingState.ReadyToShare);
        Complete(TTPlatformRequestResult.Failure(
            TTPlatformOperation.ShareRecordedVideo,
            string.IsNullOrWhiteSpace(errorMessage) ? "录屏视频分享失败。" : errorMessage));
    }

    private void HandleShareCancelled(int shareGeneration)
    {
        if (!IsCurrentShare(shareGeneration))
            return;

        _shareDeadlineRealtime = 0d;
        SetState(TTGameRecordingState.ReadyToShare);
        Complete(TTPlatformRequestResult.Cancelled(
            TTPlatformOperation.ShareRecordedVideo,
            "用户取消了录屏视频分享。"));
    }

    private bool IsCurrentSession(int generation)
    {
        return this != null && generation == _recordingSessionGeneration;
    }

    private bool IsCurrentShare(int generation)
    {
        return this != null &&
               generation == _shareGeneration &&
               _state == TTGameRecordingState.Sharing;
    }
    #endregion

    #region 工具方法
    private bool TryPrepareRequest(
        string operation,
        out TTPlatformInvocationMode invocationMode,
        out TTPlatformRequestResult blockedResult)
    {
        ResolveManager();
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

    private void SetState(TTGameRecordingState state)
    {
        if (_state == state)
            return;

        _state = state;
        TTPlatformEventUtility.InvokeSafely(
            RecordingStateChanged,
            state,
            this,
            nameof(RecordingStateChanged));
        TTPlatformEventUtility.InvokeSafely(
            recordingStateChanged,
            state,
            this,
            nameof(recordingStateChanged));
    }

    private void ArmRecordCallbackTimeout()
    {
        _callbackDeadlineRealtime =
            Time.realtimeSinceStartupAsDouble + recordCallbackTimeoutSeconds;
    }

    private double GetRecordingStartRealtime()
    {
        if (_recordingStartedRealtime > 0d)
            return _recordingStartedRealtime;

        return _recordingRequestRealtime > 0d
            ? _recordingRequestRealtime
            : Time.realtimeSinceStartupAsDouble;
    }

    /// <summary>
    /// 6.7.3 的部分异常路径会更新 SDK 状态却漏掉业务回调。
    /// 超时后只读取状态做保守收口，不会自动重发 Start / Stop。
    /// </summary>
    private bool TryReconcileRecordStateAfterTimeout(string operation)
    {
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        if (platformManager == null ||
            platformManager.ExecutionMode != TTPlatformExecutionMode.Live ||
            _recorder == null)
        {
            return false;
        }

        try
        {
            TTGameRecorder.VideoRecordState sdkState = _recorder.GetVideoRecordState();
            switch (sdkState)
            {
                case TTGameRecorder.VideoRecordState.RECORD_STARTED:
                    // Start 回调可能丢失，或 Stop 请求并未真正进入停止阶段。
                    if (_recordingStartedRealtime <= 0d)
                        _recordingStartedRealtime = GetRecordingStartRealtime();
                    SetState(TTGameRecordingState.Recording);
                    Complete(TTPlatformRequestResult.Failure(
                        operation,
                        "平台未按时回调，但状态显示仍在录制；已恢复为 Recording，可稍后再次请求 Stop。"));
                    return true;

                case TTGameRecorder.VideoRecordState.RECORD_STOPED:
                case TTGameRecorder.VideoRecordState.RECORD_COMPLETED:
                    // 没拿到 videoPath 就不能伪造 ReadyToShare，但平台已是终态，可安全结束本次会话。
                    _lastVideoPath = string.Empty;
                    SetState(TTGameRecordingState.Failed);
                    Complete(TTPlatformRequestResult.Failure(
                        operation,
                        $"平台录屏已结束（{sdkState}），但未返回 videoPath，本段视频不可分享。"));
                    return true;

                case TTGameRecorder.VideoRecordState.RECORD_ERROR:
                case TTGameRecorder.VideoRecordState.RECORD_VIDEO_TOO_SHORT:
                    _lastVideoPath = string.Empty;
                    SetState(TTGameRecordingState.Failed);
                    Complete(TTPlatformRequestResult.Failure(
                        operation,
                        $"平台录屏进入终止状态：{sdkState}。"));
                    return true;

                // STARTING / STOPING / PAUSING / PAUSED 的真实副作用仍不确定，继续锁住请求，
                // 等待迟到回调，避免自动发起第二次 Start / Stop。
                default:
                    return false;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[TTGameRecorderService] 超时后读取平台录屏状态失败：{exception.Message}",
                this);
            return false;
        }
#else
        return false;
#endif
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
            Debug.LogError($"[TTGameRecorderService] {result}", this);
        else
            Debug.LogWarning($"[TTGameRecorderService] {result}", this);

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

    private void ResolveManager()
    {
        if (platformManager == null)
            platformManager = GetComponent<TTPlatformManager>();

        if (platformManager == null)
            platformManager = TTPlatformManager.Instance;
    }
    #endregion
}
