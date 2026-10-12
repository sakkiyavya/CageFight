using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 结算奖励（挂在 GameOverCanvas 的 X3 按钮上）：
/// - 胜利时按关卡配置发放通关奖励：**普通模式与大师模式的数值分别来自关卡配置**
///   （普通默认是大师的一半，也可以在关卡里单独自定义；大师模式按「大师值 − 普通已领的部分」补发，
///   所以两种模式加起来正好等于大师值）；已通关的模式不再重复发放；
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
    /// 发放基础通关奖励（幂等）：
    /// · 普通模式：发关卡配置的「普通模式奖励」（默认 = 大师的一半，可在关卡里自定义）；
    /// · 大师模式：发「大师奖励 − 普通模式已领的部分」（普通没领过就是全额，不足 0 按 0）；
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
                // 大师难度：补发差额 —— 普通已领过就只补「大师值 − 普通实际发的量」，没领过就发全量。
                int paidGold = normalCleared ? config.NormalVictoryGoldBars : 0;
                int paidDiamond = normalCleared ? config.NormalVictoryDiamonds : 0;
                _baseGold = Mathf.Max(0, config.VictoryGoldBars - paidGold);
                _baseDiamond = Mathf.Max(0, config.VictoryDiamonds - paidDiamond);
                info.MarkStageCleared(config.stageId, true);
            }
        }
        else if (!normalCleared && !masterCleared)
        {
            _baseGold = config.NormalVictoryGoldBars;
            _baseDiamond = config.NormalVictoryDiamonds;
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
