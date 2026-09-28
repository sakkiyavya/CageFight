using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 结算奖励（挂在 GameOverCanvas 的 X3 按钮上）：
/// - 胜利时按关卡配置发放通关奖励：普通模式发 50%，大师模式发全部
///   （普通已领过一半时，大师补发剩下的 50%）；已通关的模式不再重复发放；
/// - Gold/Diamond 文本显示本次实际获得的资源数量；
/// - 点击 X3：播放激励视频，完整观看后本次奖励补发 2 倍（合计 3 倍）。
/// </summary>
[DisallowMultipleComponent]
public sealed class GameOverRewards : MonoBehaviour, IPointerDownHandler,
    IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Tooltip("金条奖励文本（TMP）")]
    private TMP_Text goldText;

    [SerializeField, Tooltip("钻石奖励文本（TMP）")]
    private TMP_Text diamondText;

    [SerializeField, Tooltip("金条展示根节点（失败时隐藏）")]
    private GameObject goldRoot;

    [SerializeField, Tooltip("钻石展示根节点（失败时隐藏）")]
    private GameObject diamondRoot;

    [SerializeField, Tooltip("抖音后台的激励视频广告位 ID")]
    private string adUnitId;

    [SerializeField, Range(0.5f, 1f), Tooltip("按下时的缩放谷值（0.88 = 缩小到 88%），提供点按反馈")]
    private float pressedScale = 0.88f;

    private int _baseGold;          // 本次已发放的基础金条奖励（X3 成功后再补 2 倍）。
    private int _baseDiamond;       // 本次已发放的基础钻石奖励。
    private bool _x3Used;           // 本次结算是否已使用过 X3。
    private bool _rewardGranted;    // 本次结算是否已发放基础奖励（幂等）。
    private Vector3 originalScale;
    private int pointerId;
    private bool pressing;
    private bool scaled;

    private void OnEnable()
    {
        Refresh();
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
        _x3Used = false;
        _rewardGranted = false;
        if (pressing)
        {
            RestoreScale();
            pressing = false;
        }
    }

    /// <summary>按本局结果计算并发放奖励，刷新 Gold/Diamond 文本与各元素可见性。</summary>
    private void Refresh()
    {
        bool victory = GameOverManager.Instance != null && GameOverManager.Instance.IsVictory;
        if (!victory)
        {
            if (goldRoot != null) goldRoot.SetActive(false);
            if (diamondRoot != null) diamondRoot.SetActive(false);
            gameObject.SetActive(false);
            return;
        }

        GrantVictoryReward();

        if (goldRoot != null) goldRoot.SetActive(_baseGold > 0);
        if (diamondRoot != null) diamondRoot.SetActive(_baseDiamond > 0);

        if (goldText != null) goldText.text = "+" + _baseGold;
        if (diamondText != null) diamondText.text = "+" + _baseDiamond;
        gameObject.SetActive(!_x3Used && (_baseGold > 0 || _baseDiamond > 0));
    }

    /// <summary>
    /// 发放基础通关奖励（幂等）：普通 50%、大师全量（普通已领时补剩下 50%），
    /// 已通关的模式不再重复发放；奖励经受控入口写入 UserGlobalInfo。
    /// </summary>
    private void GrantVictoryReward()
    {
        if (_rewardGranted)
            return;
        _rewardGranted = true;

        UserGlobalInfo info = UserGlobalInfo.Instance;
        SceneFSM fsm = SceneFSM.Instance;
        StageConfig config = fsm != null ? fsm.CurrentStageConfig : null;
        if (info == null || config == null)
            return;

        bool master = GlobalRuleManager.IsMasterMode;
        bool normalCleared = info.IsStageCleared(config.stageId, false);
        bool masterCleared = info.IsStageCleared(config.stageId, true);

        if (master)
        {
            if (!masterCleared)
            {
                // 大师难度：普通已领一半 → 补剩下 50%；否则给全量 100%。
                int portion = normalCleared ? 1 : 2;
                _baseGold = config.VictoryGoldBars * portion / 2;
                _baseDiamond = config.VictoryDiamonds * portion / 2;
                info.MarkStageCleared(config.stageId, true);
            }
        }
        else if (!normalCleared && !masterCleared)
        {
            _baseGold = config.VictoryGoldBars / 2;
            _baseDiamond = config.VictoryDiamonds / 2;
            info.MarkStageCleared(config.stageId, false);
        }

        if (_baseGold > 0)
            info.SetGoldBarCount((int)System.Math.Min(int.MaxValue, (long)info.GoldBarCount + _baseGold));
        if (_baseDiamond > 0)
            info.SetDiamondCount((int)System.Math.Min(int.MaxValue, (long)info.DiamondCount + _baseDiamond));
    }

    /// <summary>X3：播放激励视频，完整观看后补发 2 倍基础奖励（合计 3 倍）。按下时先给点按反馈。</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left || pressing)
            return;

        // 点按反馈：缩放回弹 + UI Click 音效（与结算面板其它按钮一致）。
        originalScale = transform.localScale;
        pointerId = eventData.pointerId;
        pressing = true;
        scaled = true;
        transform.localScale = originalScale * pressedScale;
        MenuClickFeedback.PlayUiClickSound(this);

        if (_x3Used || (_baseGold <= 0 && _baseDiamond <= 0))
            return;

        if (string.IsNullOrEmpty(adUnitId))
        {
            Debug.LogWarning("[GameOverRewards] 未配置广告位 ID，无法播放 X3 激励视频。", this);
            return;
        }

        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
            return;

        var persistence = info.GetComponent<UserGlobalInfoPersistence>();
        if (persistence != null && !persistence.IsLoaded)
            return;

        _x3Used = true;
        gameObject.SetActive(false);

        DouyinRewardedVideoAd.Show(adUnitId, () =>
        {
            if (info == null)
                return;

            if (_baseGold > 0)
            {
                info.SetGoldBarCount((int)System.Math.Min(int.MaxValue, (long)info.GoldBarCount + _baseGold * 2));
                if (goldText != null) goldText.text = "+" + (_baseGold * 3);
            }

            if (_baseDiamond > 0)
            {
                info.SetDiamondCount((int)System.Math.Min(int.MaxValue, (long)info.DiamondCount + _baseDiamond * 2));
                if (diamondText != null) diamondText.text = "+" + (_baseDiamond * 3);
            }
        });
    }
}
