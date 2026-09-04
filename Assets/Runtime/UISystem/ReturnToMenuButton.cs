using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 设置面板里的“返回主菜单”按钮。
/// 局内（Gameplay）点击：经 GameOverManager 统一入口判定本局失败，并请求 SceneFSM 回到主菜单，
/// 清场由 GameplayState.OnExit 经对象池统一回收执行；
/// 主菜单等非局内状态点击：直接关闭所属设置面板，返回主菜单界面。
/// 流程切换一律走框架统一入口（SceneFSM / GameOverManager），本脚本不直接操作流程对象。
/// </summary>
[DisallowMultipleComponent]
public sealed class ReturnToMenuButton : MonoBehaviour, IPointerClickHandler
{
    [SerializeField, Tooltip("点击后需要关闭的设置面板（Set Canvas）。")]
    private GameObject settingsPanel;

    /// <summary>
    /// 接收点击事件：局内判定失败并清场回主菜单；其他上下文直接关闭设置面板。
    /// </summary>
    /// <param name="eventData">本次点击的指针事件数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (SceneFSM.Instance != null &&
            SceneFSM.Instance.CurrentStateEnum == GameState.Gameplay)
        {
            // 局内：判定本局失败（统一结算入口，幂等），并请求回到主菜单。
            if (GameOverManager.Instance != null)
                GameOverManager.Instance.TriggerGameOver(false);

            SceneFSM.Instance.LoadState(GameState.Menu);
        }

        // 无论哪种上下文，点击后都关闭设置面板，避免局内回主菜单后残留面板。
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
}