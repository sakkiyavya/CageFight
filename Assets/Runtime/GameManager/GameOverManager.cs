using UnityEngine;

/// <summary>
/// 统一接收局内结束信号，并切换到游戏结算流程。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    /// <summary>本局是否已经触发结束，避免重复切换流程。</summary>
    public bool IsGameOver { get; private set; }

    /// <summary>本局结束时玩家是否获胜。</summary>
    public bool IsVictory { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// 标记本局胜负并进入结算流程。
    /// </summary>
    public void TriggerGameOver(bool isVictory)
    {
        if (IsGameOver)
            return;

        IsGameOver = true;
        IsVictory = isVictory;

        if (SceneFSM.Instance != null)
            SceneFSM.Instance.LoadState(GameState.GameOver);
    }

    /// <summary>
    /// 新一局开始时清除上一局的结算状态。
    /// </summary>
    public void ResetGameOverState()
    {
        IsGameOver = false;
        IsVictory = false;
    }
}
