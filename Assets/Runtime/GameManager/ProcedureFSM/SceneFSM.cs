using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 全局宏观流程状态机（抖音小游戏专属精简版，无异步多线程依赖，协程驱动）
/// </summary>
public class SceneFSM : MonoBehaviour
{
    private static SceneFSM _instance;
    public static SceneFSM Instance => _instance;

    private SceneStateBase _currentState;
    private GameState _currentStateEnum;
    private bool _isTransitioning;

    public GameState CurrentStateEnum => _currentStateEnum;
    public bool IsTransitioning => _isTransitioning;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // 游戏启动，默认加载进入 Menu 状态
        LoadState(GameState.Menu);
    }

    /// <summary>
    /// 对外唯一暴露的加载状态入口，隐藏一切切换与加载内部实现细节。
    /// </summary>
    /// <param name="targetState">目标状态枚举</param>
    public void LoadState(GameState targetState)
    {
        if (_isTransitioning)
        {
            Debug.LogWarning($"[SceneFSM] 正在进行状态转换，请勿重复调用 LoadState！当前目标: {targetState}");
            return;
        }

        StartCoroutine(TransitionToStateRoutine(targetState));
    }

    /// <summary>
    /// 状态切换协程核心管线
    /// </summary>
    private IEnumerator TransitionToStateRoutine(GameState targetState)
    {
        _isTransitioning = true;

        // 1. 调用当前状态的退出（Exit）协程逻辑
        if (_currentState != null)
        {
            yield return StartCoroutine(_currentState.Exit());
        }

        // 2. 映射新状态实例
        _currentStateEnum = targetState;
        _currentState = CreateStateInstance(targetState);

        // 3. 调用新状态的进入（Enter）协程逻辑
        if (_currentState != null)
        {
            yield return StartCoroutine(_currentState.Enter());
        }

        _isTransitioning = false;
    }

    /// <summary>
    /// 状态工厂方法
    /// </summary>
    private SceneStateBase CreateStateInstance(GameState state)
    {
        switch (state)
        {
            case GameState.Menu:
                return new MenuState(this);
            case GameState.Loading:
                return new LoadingState(this);
            case GameState.Gameplay:
                return new GameplayState(this);
            case GameState.GameOver:
                return new GameOverState(this);
            default:
                throw new ArgumentOutOfRangeException(nameof(state), state, null);
                
        }
    }
}
