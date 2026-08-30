using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneFSM : MonoBehaviour
{
    private static SceneFSM _instance;                           // 场景流程状态机单例。
    public static SceneFSM Instance => _instance;                // 当前可访问的状态机实例。

    [SerializeField] private SceneStateBase menuState;           // 主菜单状态组件。
    [SerializeField] private SceneStateBase stageSelectState;    // 关卡选择状态组件。
    [SerializeField] private SceneStateBase loadingState;        // 资源加载状态组件。
    [SerializeField] private SceneStateBase gameplayState;       // 局内游戏状态组件。
    [SerializeField] private SceneStateBase gameOverState;       // 游戏结算状态组件。

    private Dictionary<GameState, SceneStateBase> _stateMap;     // 状态枚举到场景状态组件的映射。
    private SceneStateBase _currentState;                        // 当前激活的状态组件。
    private GameState _currentStateEnum;                         // 当前状态的枚举值。
    private bool _isTransitioning;                               // 当前是否正在执行状态退出或进入协程。
    private bool _hasCurrentState;                               // 状态机是否已经进入过初始状态。
    private GameState? _queuedState;                             // 转换期间收到的下一状态请求。

    public GameState CurrentStateEnum => _currentStateEnum;      // 当前流程状态。
    public bool IsTransitioning => _isTransitioning;             // 状态切换协程是否正在运行。
    public StageConfig CurrentStageConfig { get; private set; }

    #region 生命周期与回调
    /// <summary>
    /// 建立状态机单例，并根据 Inspector 引用构建状态枚举到状态组件的映射。
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        _stateMap = new Dictionary<GameState, SceneStateBase>
        {
            { GameState.Menu, menuState },
            { GameState.StageSelect, stageSelectState },
            { GameState.Loading, loadingState },
            { GameState.Gameplay, gameplayState },
            { GameState.GameOver, gameOverState }
        };
    }

    /// <summary>
    /// 当前对象销毁时清除静态单例引用，避免保留失效对象。
    /// </summary>
    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>
    /// 启动场景流程并请求进入主菜单状态。
    /// </summary>
    private void Start()
    {
        LoadState(GameState.Menu);
    }
    #endregion

    #region 关卡加载入口
    /// <summary>
    /// 校验当前流程和关卡配置，保存所选关卡并请求进入加载状态。
    /// </summary>
    /// <param name="stageConfig">玩家选择、即将加载的关卡配置。</param>
    public void BeginStageLoad(StageConfig stageConfig)
    {
        if (stageConfig == null)
        {
            Debug.LogError("[SceneFSM] 无法开始关卡：StageConfig 为空！");
            return;
        }

        if (_isTransitioning || (_hasCurrentState &&
            _currentStateEnum != GameState.Menu &&
            _currentStateEnum != GameState.StageSelect))
        {
            Debug.LogWarning("[SceneFSM] 当前不在可开始关卡的菜单状态，忽略本次点击。");
            return;
        }

        CurrentStageConfig = stageConfig;
        if (UserGlobalInfo.Instance != null)
            UserGlobalInfo.Instance.SetCurrentStageType(stageConfig.stageType);

        MenuAmbientAudio.NotifyBeginStage();
        LoadState(GameState.Loading);
    }
    #endregion

    #region 选关入口
    /// <summary>
    /// 在主菜单空闲状态下请求打开关卡选择流程。
    /// </summary>
    public void OpenStageSelect()
    {
        if (_isTransitioning)
            return;

        if (_hasCurrentState && _currentStateEnum != GameState.Menu)
        {
            Debug.LogWarning("[SceneFSM] 只有在 MenuState 中才能打开关卡选择界面。");
            return;
        }

        LoadState(GameState.StageSelect);
    }
    #endregion

    #region 状态切换请求
    /// <summary>
    /// 请求切换到指定流程状态；切换期间收到的新请求会排队并在当前切换完成后继续执行。
    /// </summary>
    /// <param name="targetState">需要进入的目标状态。</param>
    public void LoadState(GameState targetState)
    {
        if (_stateMap == null || !_stateMap.TryGetValue(targetState, out var targetStateObject) || targetStateObject == null)
        {
            Debug.LogError($"[SceneFSM] 状态 {targetState} 未配置，请检查 Inspector 引用！");
            return;
        }

        if (_isTransitioning)
        {
            _queuedState = targetState;
            return;
        }

        if (_hasCurrentState && _currentStateEnum == targetState)
            return;

        StartCoroutine(TransitionToStateRoutine(targetState));
    }
    #endregion

    #region 状态切换协程
    /// <summary>
    /// 退出当前状态、向目标状态注入关卡配置并执行其进入流程；随后继续处理切换期间排队的请求。
    /// </summary>
    /// <param name="targetState">本轮首先要进入的目标状态。</param>
    /// <returns>等待状态退出、进入和所有排队切换完成的协程。</returns>
    private IEnumerator TransitionToStateRoutine(GameState targetState)
    {
        _isTransitioning = true;

        while (true)
        {
            _queuedState = null;

            if (_currentState != null)
                yield return StartCoroutine(_currentState.Exit());

            if (!_stateMap.TryGetValue(targetState, out var nextState) || nextState == null)
            {
                Debug.LogError($"[SceneFSM] 状态 {targetState} 未配置，请检查 Inspector 引用！");
                break;
            }

            _currentStateEnum = targetState;
            _currentState = nextState;
            _hasCurrentState = true;
            _currentState.SetStageConfig(CurrentStageConfig);

            yield return StartCoroutine(_currentState.Enter());

            if (!_queuedState.HasValue)
                break;

            targetState = _queuedState.Value;
        }

        _isTransitioning = false;
    }
    #endregion
}
