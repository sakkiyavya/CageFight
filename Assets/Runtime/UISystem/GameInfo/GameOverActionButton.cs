using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 结算界面按钮：Next（胜利 → 进入下一关）与 Try again（失败 → 重开本关）。
/// 点击经 EventSystem（IPointerDownHandler）；关卡流转统一经 SceneFSM。
/// 按下时提供与返回按钮一致的点按反馈（缩放回弹 + "UI Click" 音效）。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameOverActionButton : MonoBehaviour, IPointerDownHandler,
    IPointerUpHandler, IPointerExitHandler
{
    public enum ActionKind
    {
        Next = 0,
        TryAgain = 1,
    }

    [SerializeField, Tooltip("按钮行为：Next = 下一关；Try again = 重开本关")]
    private ActionKind action = ActionKind.Next;

    [SerializeField, Range(0.5f, 1f), Tooltip("按下时的缩放谷值（0.88 = 缩小到 88%），提供点按反馈")]
    private float pressedScale = 0.88f;

    private Vector3 originalScale;
    private int pointerId;
    private bool pressing;
    private bool scaled;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || pressing)
            return;

        // 点按反馈：先缩放回弹与音效，再执行按钮行为（与返回按钮一致的按感）。
        originalScale = transform.localScale;
        pointerId = eventData.pointerId;
        pressing = true;
        scaled = true;
        transform.localScale = originalScale * pressedScale;
        MenuClickFeedback.PlayUiClickSound(this);

        if (action == ActionKind.TryAgain)
        {
            SceneFSM fsm = SceneFSM.Instance;
            if (fsm == null)
            {
                Debug.LogWarning("[GameOverActionButton] SceneFSM 未就绪，无法重试本关。", this);
                return;
            }
            if (fsm.IsTransitioning)
            {
                Debug.LogWarning("[GameOverActionButton] 状态切换中，忽略本次重试点击。", this);
                return;
            }
            if (fsm.CurrentStageConfig == null)
            {
                Debug.LogWarning("[GameOverActionButton] 当前关卡配置为空，无法重试（请经选关流程进入关卡）。", this);
                return;
            }
            fsm.RestartCurrentStage();
            return;
        }

        TryStartNextStage();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressing || eventData.pointerId != pointerId ||
            eventData.button != PointerEventData.InputButton.Left) return;

        RestoreScale();
        pressing = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!pressing || eventData.pointerId != pointerId) return;

        RestoreScale();
    }

    private void RestoreScale()
    {
        if (!scaled) return;
        transform.localScale = originalScale;
        scaled = false;
    }

    private void OnDisable()
    {
        if (pressing)
        {
            RestoreScale();
            pressing = false;
        }
    }

    /// <summary>
    /// 胜利进入下一关：按 "Stage{当前关号+1}" 地址加载下一关配置（经 ResourceManager），
    /// 成功后交给 SceneFSM 启动；下一关不存在时返回主界面。
    /// </summary>
    private static void TryStartNextStage()
    {
        SceneFSM fsm = SceneFSM.Instance;
        StageConfig current = fsm != null ? fsm.CurrentStageConfig : null;
        if (fsm == null)
        {
            Debug.LogWarning("[GameOverActionButton] SceneFSM 未就绪，无法进入下一关。");
            return;
        }
        if (current == null)
        {
            Debug.LogWarning("[GameOverActionButton] 当前关卡配置为空，无法解析下一关（请经选关流程进入关卡）。");
            return;
        }
        if (ResourceManager.Instance == null)
        {
            Debug.LogWarning("[GameOverActionButton] ResourceManager 未就绪，无法加载下一关。");
            return;
        }

        string nextKey = "Stage" + (current.stageId + 1);
        ResourceManager.Instance.LoadExtraResourceAsync<StageConfig>(nextKey, nextConfig =>
        {
            if (nextConfig == null)
            {
                Debug.LogWarning($"[GameOverActionButton] 下一关 {nextKey} 不存在，返回主界面。");
                if (SceneFSM.Instance != null)
                    SceneFSM.Instance.LoadState(GameState.Menu);
                return;
            }

            if (SceneFSM.Instance != null)
                SceneFSM.Instance.StartNextStage(nextConfig);
        });
    }
}
