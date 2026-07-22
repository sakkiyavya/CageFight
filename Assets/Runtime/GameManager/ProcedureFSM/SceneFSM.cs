using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SceneFSM : MonoBehaviour
{
    private static SceneFSM _instance;
    public static SceneFSM Instance => _instance;

    [SerializeField] private SceneStateBase menuState;
    [SerializeField] private SceneStateBase loadingState;
    [SerializeField] private SceneStateBase gameplayState;
    [SerializeField] private SceneStateBase gameOverState;

    private Dictionary<GameState, SceneStateBase> _stateMap;
    private SceneStateBase _currentState;
    private GameState _currentStateEnum;
    private bool _isTransitioning;
    private bool _hasCurrentState;
    private GameState? _queuedState;

    public GameState CurrentStateEnum => _currentStateEnum;
    public bool IsTransitioning => _isTransitioning;
    public LevelConfig CurrentLevelConfig { get; private set; }

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
            { GameState.Loading, loadingState },
            { GameState.Gameplay, gameplayState },
            { GameState.GameOver, gameOverState }
        };
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Start()
    {
        LoadState(GameState.Menu);
    }

    public void BeginLevelLoad(LevelConfig levelConfig)
    {
        if (levelConfig == null)
        {
            Debug.LogError("[SceneFSM] 无法开始关卡：LevelConfig 为空！");
            return;
        }

        if (_isTransitioning || (_hasCurrentState && _currentStateEnum != GameState.Menu))
        {
            Debug.LogWarning("[SceneFSM] 当前不在可开始关卡的菜单状态，忽略本次点击。");
            return;
        }

        CurrentLevelConfig = levelConfig;
        LoadState(GameState.Loading);
    }

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
            _currentState.SetLevelConfig(CurrentLevelConfig);

            yield return StartCoroutine(_currentState.Enter());

            if (!_queuedState.HasValue)
                break;

            targetState = _queuedState.Value;
        }

        _isTransitioning = false;
    }
}
