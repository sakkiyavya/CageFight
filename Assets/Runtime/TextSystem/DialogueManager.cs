using System;
using System.Collections;
using UnityEngine;

public enum DialogueState
{
    Hidden,
    WaitingForResources,
    Entering,
    Visible,
    Exiting
}

public enum DialogueSeriesCancelReason
{
    Replaced,
    Hidden,
    Requested,
    InvalidResource,
    ResourceUnloaded
}

/// <summary>
/// 全局唯一的对话调度器。普通请求采用 latest-wins，系列内部按配置顺序推进。
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("框架引用")]
    [Tooltip("由上层 UI 框架实例化并管理生命周期的 DialogueView。该引用不是 Prefab 资源引用。")]
    [SerializeField] private DialogueView dialogueView;

    [Tooltip("等待关卡资源预加载完成的最长实时秒数。设为 0 表示不限制。")]
    [SerializeField, Min(0f)] private float resourceWaitTimeout = 30f;

    [Header("模态行为")]
    [SerializeField] private bool pauseGameplay = true;
    [Tooltip("UIStack 会直接轮询原始输入；显示对话时暂时禁用它，避免点击对话框同时关闭底层 UI。")]
    [SerializeField] private bool suppressUIStack = true;

    private DialogueView _view;

    private DialogueConfigSO _desiredConfig;
    private ulong _desiredPresentationId;
    private DialogueConfigSO _currentConfig;
    private ulong _currentPresentationId;
    private bool _currentShownEventRaised;

    private Sprite _preparedSprite;
    private ulong _preparedPresentationId;
    private ulong _nextPresentationId;
    private Coroutine _worker;

    private DialogueSeriesSO _activeSeries;
    private int _seriesIndex = -1;
    private DialogueSeriesSO _seriesAwaitingCompletion;
    private ulong _seriesCompletionPresentationId;

    private bool _ownsPause;
    private float _previousTimeScale = 1f;
    private UIStack _suppressedUIStack;
    private bool _suppressedUIStackWasEnabled;

    private ResourceManager _subscribedResourceManager;

    public DialogueState State { get; private set; } = DialogueState.Hidden;
    public DialogueConfigSO CurrentConfig => _currentConfig;
    public DialogueSeriesSO ActiveSeries => _activeSeries;
    public int ActiveSeriesIndex => _seriesIndex;
    public bool IsVisible => State == DialogueState.Visible;
    public static bool IsInputBlocked => Instance != null && Instance._ownsPause;

    public event Action<DialogueConfigSO> OnDialogueShown;
    public event Action<DialogueConfigSO> OnDialogueHidden;
    public event Action<DialogueSeriesSO> OnSeriesCompleted;
    public event Action<DialogueSeriesSO, DialogueSeriesCancelReason> OnSeriesCancelled;
    public event Action<DialogueConfigSO, string> OnDialogueFailed;

    #region 生命周期
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        if (_view == null && dialogueView != null)
            AttachView(dialogueView);

        RefreshResourceManagerSubscription();
        if (!ActualMatchesDesired())
            EnsureWorker();
    }

    private void Update()
    {
        RefreshResourceManagerSubscription();

        if (_ownsPause && suppressUIStack && _suppressedUIStack == null)
            SuppressCurrentUIStack();
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;

        ResetImmediately(DialogueSeriesCancelReason.Hidden);
        DetachView();
        UnsubscribeResourceManager();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        if (_worker != null)
        {
            StopCoroutine(_worker);
            _worker = null;
        }

        if (_view != null)
        {
            _view.ClearContent();
            _view.SnapHidden();
        }

        DetachView();
        UnsubscribeResourceManager();
        ReleaseModalState();
        Instance = null;
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 请求显示单条对话。若正在播放系列，会取消该系列。
    /// </summary>
    public void Show(DialogueConfigSO config)
    {
        if (!CanAcceptRequests())
            return;

        if (config == null)
        {
            Debug.LogWarning("[DialogueManager] Show 收到空配置，已忽略。", this);
            return;
        }

        DialogueSeriesSO cancelledSeries = DetachActiveSeries();
        SetDesired(config);
        RaiseSeriesCancelled(cancelledSeries, DialogueSeriesCancelReason.Replaced);
    }

    /// <summary>
    /// 请求当前对话完整退场。重复调用是安全的。
    /// </summary>
    public void Hide()
    {
        if (!CanAcceptRequests())
            return;

        DialogueSeriesSO cancelledSeries = DetachActiveSeries();
        SetDesired(null);
        RaiseSeriesCancelled(cancelledSeries, DialogueSeriesCancelReason.Hidden);
    }

    /// <summary>
    /// 从系列中的第一条有效配置开始播放。
    /// </summary>
    public void PlaySeries(DialogueSeriesSO series)
    {
        if (!CanAcceptRequests())
            return;

        if (series == null || !series.TryGetNextValid(0, out int index, out DialogueConfigSO config))
        {
            Debug.LogWarning("[DialogueManager] 无法播放空系列或不含有效配置的系列。", series);
            return;
        }

        DialogueSeriesSO cancelledSeries = DetachActiveSeries();
        _activeSeries = series;
        _seriesIndex = index;
        SetDesired(config);
        RaiseSeriesCancelled(cancelledSeries, DialogueSeriesCancelReason.Replaced);
    }

    /// <summary>
    /// 推进系列；单条对话则请求退场。进退场期间的点击会被忽略。
    /// </summary>
    public bool Advance()
    {
        if (!CanAcceptRequests())
            return false;

        if (State != DialogueState.Visible ||
            _currentConfig == null ||
            _currentPresentationId != _desiredPresentationId)
        {
            return false;
        }

        if (_activeSeries == null)
        {
            Hide();
            return true;
        }

        if (_activeSeries.TryGetNextValid(_seriesIndex + 1, out int nextIndex, out DialogueConfigSO nextConfig))
        {
            _seriesIndex = nextIndex;
            SetDesired(nextConfig);
            return true;
        }

        _seriesAwaitingCompletion = _activeSeries;
        _seriesCompletionPresentationId = _currentPresentationId;
        _activeSeries = null;
        _seriesIndex = -1;
        SetDesired(null);
        return true;
    }

    /// <summary>
    /// 取消当前系列并请求对话框退场。没有活动系列时不影响单条对话。
    /// </summary>
    public bool CancelSeries()
    {
        if (!CanAcceptRequests())
            return false;

        if (_activeSeries == null)
            return false;

        DialogueSeriesSO cancelledSeries = DetachActiveSeries();
        SetDesired(null);
        RaiseSeriesCancelled(cancelledSeries, DialogueSeriesCancelReason.Requested);
        return true;
    }
    #endregion

    #region 请求调度
    private bool CanAcceptRequests()
    {
        if (Instance == this && isActiveAndEnabled)
            return true;

        Debug.LogWarning("[DialogueManager] 上层框架尚未启用对话模块，请求已忽略。", this);
        return false;
    }

    private void SetDesired(DialogueConfigSO config)
    {
        _desiredConfig = config;
        _desiredPresentationId = config != null ? NextPresentationId() : 0;
        ClearPrepared();
        EnsureWorker();
    }

    private ulong NextPresentationId()
    {
        _nextPresentationId++;
        if (_nextPresentationId == 0)
            _nextPresentationId++;
        return _nextPresentationId;
    }

    private void EnsureWorker()
    {
        if (_worker == null && isActiveAndEnabled)
            _worker = StartCoroutine(ProcessRequestsRoutine());
    }

    private IEnumerator ProcessRequestsRoutine()
    {
        while (!ActualMatchesDesired())
        {
            if (_desiredConfig == null)
            {
                ClearPrepared();
                if (_currentConfig != null)
                    yield return ExitCurrentRoutine();
                else
                    State = DialogueState.Hidden;
                continue;
            }

            if (_preparedPresentationId != _desiredPresentationId)
            {
                yield return PrepareDesiredRoutine(_desiredConfig, _desiredPresentationId);
                continue;
            }

            if (_currentConfig != null)
            {
                yield return ExitCurrentRoutine();
                continue;
            }

            yield return EnterPreparedRoutine();
        }

        _worker = null;

        if (_currentConfig == null && _desiredConfig == null)
            ReleaseModalState();

        if (!ActualMatchesDesired())
            EnsureWorker();
    }

    private IEnumerator PrepareDesiredRoutine(DialogueConfigSO capturedConfig, ulong capturedId)
    {
        if (_currentConfig == null)
            State = DialogueState.WaitingForResources;

        ResourceManager resourceManager = null;
        float waitStartedAt = Time.realtimeSinceStartup;
        while (IsStillDesired(capturedConfig, capturedId))
        {
            resourceManager = ResourceManager.Instance;
            if (resourceManager != null && resourceManager.CurrentState == ResourceState.LoadComplete)
                break;

            if (resourceWaitTimeout > 0f &&
                Time.realtimeSinceStartup - waitStartedAt >= resourceWaitTimeout)
            {
                RejectDesired(
                    capturedConfig,
                    capturedId,
                    $"等待关卡资源预加载完成超过 {resourceWaitTimeout:0.##} 秒");
                yield break;
            }

            yield return null;
        }

        if (!IsStillDesired(capturedConfig, capturedId))
        {
            if (_currentConfig == null)
                State = DialogueState.Hidden;
            yield break;
        }

        string spriteKey = capturedConfig.PortraitSpriteKey;
        if (string.IsNullOrWhiteSpace(spriteKey))
        {
            RejectDesired(capturedConfig, capturedId, "人物 Sprite Key 为空");
            yield break;
        }

        Sprite portrait = resourceManager.GetSprite(spriteKey);
        if (portrait == null)
        {
            RejectDesired(
                capturedConfig,
                capturedId,
                $"未在当前关卡预加载缓存中找到 Sprite Key：{spriteKey}");
            yield break;
        }

        if (!EnsureView(out string viewError))
        {
            RejectDesired(capturedConfig, capturedId, viewError);
            yield break;
        }

        if (!IsStillDesired(capturedConfig, capturedId))
            yield break;

        _preparedSprite = portrait;
        _preparedPresentationId = capturedId;
        if (_currentConfig == null)
            State = DialogueState.Hidden;
    }

    private IEnumerator EnterPreparedRoutine()
    {
        DialogueConfigSO capturedConfig = _desiredConfig;
        ulong capturedId = _desiredPresentationId;
        Sprite capturedSprite = _preparedSprite;

        if (capturedConfig == null || capturedId == 0 || _view == null)
            yield break;

        AcquireModalState();
        _view.Bind(capturedSprite, capturedConfig.Text);
        _view.SetInputBlocked(true);

        _currentConfig = capturedConfig;
        _currentPresentationId = capturedId;
        _currentShownEventRaised = false;
        State = DialogueState.Entering;

        yield return _view.PlayEnter();

        State = DialogueState.Visible;
        ClearPrepared(capturedId);

        if (IsStillDesired(capturedConfig, capturedId))
        {
            _currentShownEventRaised = true;
            InvokeSafely(OnDialogueShown, capturedConfig);
        }
    }

    private IEnumerator ExitCurrentRoutine()
    {
        DialogueConfigSO oldConfig = _currentConfig;
        ulong oldPresentationId = _currentPresentationId;
        bool shouldRaiseHidden = _currentShownEventRaised;

        State = DialogueState.Exiting;
        if (_view != null)
        {
            _view.SetInputBlocked(true);
            yield return _view.PlayExit();
            _view.ClearContent();
            _view.SnapHidden();
        }

        _currentConfig = null;
        _currentPresentationId = 0;
        _currentShownEventRaised = false;
        State = DialogueState.Hidden;

        if (shouldRaiseHidden)
            InvokeSafely(OnDialogueHidden, oldConfig);

        if (_seriesAwaitingCompletion != null &&
            _seriesCompletionPresentationId == oldPresentationId)
        {
            DialogueSeriesSO completedSeries = _seriesAwaitingCompletion;
            _seriesAwaitingCompletion = null;
            _seriesCompletionPresentationId = 0;
            InvokeSafely(OnSeriesCompleted, completedSeries);
        }

        if (_desiredConfig == null)
        {
            ReleaseModalState();
        }
        else if (_view != null)
        {
            // 两条对话切换之间保持全屏模态拦截。
            _view.SetInputBlocked(true);
        }
    }

    private bool ActualMatchesDesired()
    {
        if (_desiredConfig == null)
            return _currentConfig == null && State == DialogueState.Hidden;

        return _currentConfig == _desiredConfig &&
               _currentPresentationId == _desiredPresentationId &&
               State == DialogueState.Visible;
    }

    private bool IsStillDesired(DialogueConfigSO config, ulong presentationId)
    {
        return _desiredConfig == config && _desiredPresentationId == presentationId;
    }

    private void RejectDesired(DialogueConfigSO config, ulong presentationId, string reason)
    {
        if (!IsStillDesired(config, presentationId))
            return;

        Debug.LogError($"[DialogueManager] 对话请求失败：{reason}。Config: {config.name}", config);

        DialogueSeriesSO failedSeries = _activeSeries;
        _activeSeries = null;
        _seriesIndex = -1;
        ClearPrepared();

        bool currentIsCompletingSeries =
            _seriesAwaitingCompletion != null &&
            _seriesCompletionPresentationId == _currentPresentationId;
        bool shouldRaiseRecoveredShown = false;

        if (_currentConfig != null && !currentIsCompletingSeries)
        {
            _desiredConfig = _currentConfig;
            _desiredPresentationId = _currentPresentationId;

            if (State == DialogueState.Visible && !_currentShownEventRaised)
            {
                _currentShownEventRaised = true;
                shouldRaiseRecoveredShown = true;
            }
        }
        else
        {
            _desiredConfig = null;
            _desiredPresentationId = 0;
            if (_currentConfig == null)
                State = DialogueState.Hidden;
        }

        if (shouldRaiseRecoveredShown)
            InvokeSafely(OnDialogueShown, _currentConfig);
        InvokeSafely(OnDialogueFailed, config, reason);
        if (failedSeries != null)
            InvokeSafely(OnSeriesCancelled, failedSeries, DialogueSeriesCancelReason.InvalidResource);
    }

    private void ClearPrepared(ulong onlyIfPresentationId = 0)
    {
        if (onlyIfPresentationId != 0 && _preparedPresentationId != onlyIfPresentationId)
            return;

        _preparedSprite = null;
        _preparedPresentationId = 0;
    }
    #endregion

    #region 模板与资源生命周期
    private bool EnsureView(out string error)
    {
        if (_view == null && dialogueView != null)
            AttachView(dialogueView);

        if (_view == null)
        {
            error = "上层 UI 框架未给 DialogueManager 配置 DialogueView 实例";
            return false;
        }

        if (!_view.TryValidate(out error))
            return false;

        if (!_view.gameObject.activeInHierarchy)
        {
            error = "DialogueView 当前未被上层 UI 框架激活";
            return false;
        }

        error = null;
        return true;
    }

    private void AttachView(DialogueView view)
    {
        DetachView();
        _view = view;

        if (_view == null)
            return;

        dialogueView = _view;
        _view.AdvanceRequested += HandleAdvanceRequested;
        _view.SnapHidden();
    }

    private void DetachView()
    {
        if (_view != null)
            _view.AdvanceRequested -= HandleAdvanceRequested;
        _view = null;
    }

    private void RefreshResourceManagerSubscription()
    {
        ResourceManager current = ResourceManager.Instance;
        if (ReferenceEquals(_subscribedResourceManager, current))
            return;

        bool hadPreviousManager = !ReferenceEquals(_subscribedResourceManager, null);
        UnsubscribeResourceManager();

        if (hadPreviousManager)
            ResetImmediately(DialogueSeriesCancelReason.ResourceUnloaded);

        _subscribedResourceManager = current;
        if (_subscribedResourceManager != null)
            _subscribedResourceManager.OnUnloadComplete += HandleResourcesUnloaded;
    }

    private void UnsubscribeResourceManager()
    {
        if (_subscribedResourceManager != null)
            _subscribedResourceManager.OnUnloadComplete -= HandleResourcesUnloaded;
        _subscribedResourceManager = null;
    }

    private void HandleResourcesUnloaded()
    {
        ResetImmediately(DialogueSeriesCancelReason.ResourceUnloaded);
    }

    private void ResetImmediately(DialogueSeriesCancelReason reason)
    {
        if (_worker != null)
        {
            StopCoroutine(_worker);
            _worker = null;
        }

        DialogueConfigSO oldConfig = _currentConfig;
        bool shouldRaiseHidden = _currentShownEventRaised;
        DialogueSeriesSO cancelledActiveSeries = _activeSeries;
        DialogueSeriesSO cancelledCompletingSeries = _seriesAwaitingCompletion;

        _desiredConfig = null;
        _desiredPresentationId = 0;
        _currentConfig = null;
        _currentPresentationId = 0;
        _currentShownEventRaised = false;
        _activeSeries = null;
        _seriesIndex = -1;
        _seriesAwaitingCompletion = null;
        _seriesCompletionPresentationId = 0;
        ClearPrepared();
        State = DialogueState.Hidden;

        if (_view != null)
        {
            _view.ClearContent();
            _view.SnapHidden();
        }

        ReleaseModalState();

        if (shouldRaiseHidden && oldConfig != null)
            InvokeSafely(OnDialogueHidden, oldConfig);
        if (cancelledActiveSeries != null)
            InvokeSafely(OnSeriesCancelled, cancelledActiveSeries, reason);
        if (cancelledCompletingSeries != null)
            InvokeSafely(OnSeriesCancelled, cancelledCompletingSeries, reason);
    }
    #endregion

    #region 暂停与输入拦截
    private void AcquireModalState()
    {
        if (_ownsPause)
            return;

        _ownsPause = true;
        if (pauseGameplay)
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        SuppressCurrentUIStack();
    }

    private void ReleaseModalState()
    {
        if (!_ownsPause)
            return;

        if (_view != null)
            _view.SetInputBlocked(false);

        RestoreSuppressedUIStack();

        if (pauseGameplay && Mathf.Approximately(Time.timeScale, 0f))
            Time.timeScale = _previousTimeScale;

        _ownsPause = false;
    }

    private void SuppressCurrentUIStack()
    {
        if (!suppressUIStack || _suppressedUIStack != null || UIStack.Instance == null)
            return;

        _suppressedUIStack = UIStack.Instance;
        _suppressedUIStackWasEnabled = _suppressedUIStack.enabled;
        _suppressedUIStack.enabled = false;
    }

    private void RestoreSuppressedUIStack()
    {
        if (_suppressedUIStack != null && _suppressedUIStackWasEnabled)
            _suppressedUIStack.enabled = true;

        _suppressedUIStack = null;
        _suppressedUIStackWasEnabled = false;
    }
    #endregion

    #region 系列与事件
    private void HandleAdvanceRequested()
    {
        Advance();
    }

    private DialogueSeriesSO DetachActiveSeries()
    {
        if (_activeSeries == null)
            return null;

        DialogueSeriesSO cancelled = _activeSeries;
        _activeSeries = null;
        _seriesIndex = -1;
        return cancelled;
    }

    private void RaiseSeriesCancelled(DialogueSeriesSO series, DialogueSeriesCancelReason reason)
    {
        if (series != null)
            InvokeSafely(OnSeriesCancelled, series, reason);
    }

    private static void InvokeSafely<T>(Action<T> callback, T arg)
    {
        if (callback == null)
            return;

        foreach (Delegate subscriber in callback.GetInvocationList())
        {
            try
            {
                ((Action<T>)subscriber).Invoke(arg);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    private static void InvokeSafely<T1, T2>(Action<T1, T2> callback, T1 arg1, T2 arg2)
    {
        if (callback == null)
            return;

        foreach (Delegate subscriber in callback.GetInvocationList())
        {
            try
            {
                ((Action<T1, T2>)subscriber).Invoke(arg1, arg2);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
    #endregion
}
