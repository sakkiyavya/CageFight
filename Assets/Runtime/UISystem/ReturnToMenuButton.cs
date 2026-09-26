using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 设置面板里的“返回主菜单”按钮。
/// 局内（Gameplay）点击：经 GameOverManager 统一入口判定本局失败，停留在结算界面，
/// 清场由 GameplayState.OnExit 经对象池统一回收执行；
/// 主菜单等非局内状态点击：直接关闭所属设置面板，返回主菜单界面。
/// 流程切换一律走框架统一入口（SceneFSM / GameOverManager），本脚本不直接操作流程对象。
/// </summary>
[DisallowMultipleComponent]
public sealed class ReturnToMenuButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IPointerExitHandler, IPointerClickHandler
{
    [SerializeField, Tooltip("点击后需要关闭的设置面板（Set Canvas）。")]
    private GameObject settingsPanel;
    [SerializeField, Range(0.5f, 1f)] private float pressedScale = 0.88f;

    private Vector3 originalScale;
    private int pointerId;
    private bool pressing;
    private bool cancelled;
    private bool clickReady;
    private bool scaled;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || pressing ||
            (SceneFSM.Instance != null && SceneFSM.Instance.IsTransitioning)) return;
        originalScale = transform.localScale;
        pointerId = eventData.pointerId;
        pressing = true;
        cancelled = false;
        clickReady = false;
        scaled = true;
        transform.localScale = originalScale * pressedScale;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!pressing || eventData.pointerId != pointerId) return;
        cancelled = true; // Moving back inside does not re-arm this press.
        RestoreScale();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressing || eventData.pointerId != pointerId ||
            eventData.button != PointerEventData.InputButton.Left) return;
        RestoreScale();
        pressing = false;
        clickReady = !cancelled && RectTransformUtility.RectangleContainsScreenPoint(
            (RectTransform)transform, eventData.position, eventData.pressEventCamera);
    }

    private void RestoreScale()
    {
        if (!scaled) return;
        transform.localScale = originalScale;
        scaled = false;
    }

    private void CancelPress()
    {
        RestoreScale();
        pressing = false;
        clickReady = false;
        cancelled = true;
    }

    private void OnDisable() => CancelPress();
    private void OnApplicationFocus(bool focused) { if (!focused) CancelPress(); }
    private void OnApplicationPause(bool paused) { if (paused) CancelPress(); }

    /// <summary>
    /// 局内点击只进入结算；结算页 Back 或选关页返回按钮才回到主菜单。
    /// </summary>
    /// <param name="eventData">本次点击的指针事件数据。</param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!clickReady || eventData.pointerId != pointerId ||
            eventData.button != PointerEventData.InputButton.Left) return;
        clickReady = false;
        SceneFSM fsm = SceneFSM.Instance;
        if (fsm != null && fsm.IsTransitioning) return;
        if (fsm != null)
        {
            if (fsm.CurrentStateEnum == GameState.Gameplay)
            {
                // 只提交一次结算请求，不再排入 Menu 请求跳过结算页面。
                if (GameOverManager.Instance != null)
                    GameOverManager.Instance.TriggerGameOver(false);

                else
                    fsm.LoadState(GameState.GameOver);
            }
            else if (fsm.CurrentStateEnum == GameState.StageSelect ||
                     fsm.CurrentStateEnum == GameState.GameOver)
            {
                // 关卡选择界面返回：必须走状态机切换，
                // 由 MenuState 重新打开菜单的 UI 模块（否则只关面板会留下空白界面）。
                // 注意：这里绝不能关闭 settingsPanel（StageSelectCanvas）——
                // 选关 UI 模块都挂在该 Canvas 下，父节点一旦被关闭，
                // 下次进入选关状态时模块无法显示，选关界面就再也打不开了。
                fsm.LoadState(GameState.Menu);
                return;
            }
        }

        // 无论哪种上下文，点击后都关闭设置面板，避免局内回主菜单后残留面板。
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }
}
