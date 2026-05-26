using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全局宏观流程状态机（协程驱动）
/// 各状态为场景中的真实 GameObject，在 Inspector 中直接引用，
/// 不再使用工厂方法 new 实例。
/// </summary>
public class SceneFSM : MonoBehaviour
{
    static SceneFSM _instance;
    public static SceneFSM Instance => _instance;

    [Header("状态对象引用（对应场景中各状态 GameObject）")]
    [SerializeField] SceneStateBase menuState;
    [SerializeField] SceneStateBase loadingState;
    [SerializeField] SceneStateBase gameplayState;
    [SerializeField] SceneStateBase gameOverState;

    Dictionary<GameState, SceneStateBase> _stateMap;
    SceneStateBase _currentState;
    GameState _currentStateEnum;
    bool _isTransitioning;

    public GameState CurrentStateEnum => _currentStateEnum;
    public bool IsTransitioning => _isTransitioning;

    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // 构建状态映射表
        _stateMap = new Dictionary<GameState, SceneStateBase>
        {
            { GameState.Menu,     menuState     },
            { GameState.Loading,  loadingState  },
            { GameState.Gameplay, gameplayState },
            { GameState.GameOver, gameOverState },
        };
    }

    void Start()
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
    /// 状态切换协程核心管线：
    /// 1. 等待当前状态 Exit() 完成
    /// 2. 从场景引用中找到目标状态对象
    /// 3. 等待新状态 Enter() 完成
    /// </summary>
    IEnumerator TransitionToStateRoutine(GameState targetState)
    {
        _isTransitioning = true;

        // 1. 调用当前状态的退出协程并等待完成
        if (_currentState != null)
        {
            yield return StartCoroutine(_currentState.Exit());
        }

        // 2. 从场景引用映射表中查找目标状态
        if (!_stateMap.TryGetValue(targetState, out SceneStateBase nextState))
        {
            Debug.LogError($"[SceneFSM] 找不到状态对象：{targetState}，请检查 Inspector 中的引用是否已赋值！");
            _isTransitioning = false;
            yield break;
        }

        _currentStateEnum = targetState;
        _currentState = nextState;

        // 3. 调用新状态的进入协程并等待完成
        yield return StartCoroutine(_currentState.Enter());

        _isTransitioning = false;
    }
}
