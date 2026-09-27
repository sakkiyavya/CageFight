using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 选关界面的大师难度开关（挂在可点击区域上）：
/// 点击切换大师模式——开启时亮起 Difficult 图标并弹出字幕“欢迎开启大师难度”，
/// 关闭时熄灭图标并弹出字幕“回归应有的旅途”。
/// 字幕带出场（回弹弹入 + 淡入）与退场（缩小 + 淡出）动画，停留时长结束后自动收起。
/// 大师难度开启时：本局敌方等级 +2，并在关卡已有规则上追加随机抽取的
/// 1 个负面全局规则 + 1 个中立全局规则（来自规则池，本局开始前抽取）。
/// 点击经 EventSystem（IPointerDownHandler），状态存 UserGlobalInfo 临时字段。
/// </summary>
[DisallowMultipleComponent]
public sealed class MasterModeButton : MonoBehaviour, IPointerDownHandler
{
    [SerializeField, Tooltip("大师难度图标（亮起/关闭由本组件控制）")]
    private GameObject difficultIcon;

    [SerializeField, Tooltip("切换时弹出的字幕文本（TMP）")]
    private TextMeshProUGUI subtitleText;

    [SerializeField, Min(0.5f), Tooltip("字幕停留时长（秒，不含出场/退场动画）")]
    private float subtitleDuration = 2f;

    [SerializeField, Min(0.05f), Tooltip("字幕出场动画时长（秒）")]
    private float subtitleEnterDuration = 0.25f;

    [SerializeField, Min(0.05f), Tooltip("字幕退场动画时长（秒）")]
    private float subtitleExitDuration = 0.2f;

    private const string OnSubtitle = "欢迎开启大师难度";
    private const string OffSubtitle = "回归应有的旅途";

    private Coroutine _subtitleRoutine;

    private void Awake()
    {
        RefreshVisual();
        GlobalRuleManager.LoadMasterRulePool();
    }

    private void OnEnable()
    {
        RefreshVisual();
        GlobalRuleManager.LoadMasterRulePool();
    }

    private void OnDisable()
    {
        // 协程与字幕状态复位（对称退订/停协程）。
        if (_subtitleRoutine != null)
        {
            StopCoroutine(_subtitleRoutine);
            _subtitleRoutine = null;
        }

        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(false);
            ResetSubtitleVisual();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        bool enabled = !GlobalRuleManager.IsMasterMode;
        if (UserGlobalInfo.Instance != null)
            UserGlobalInfo.Instance.SetMasterModeEnabled(enabled);

        RefreshVisual();
        ShowSubtitle(enabled ? OnSubtitle : OffSubtitle);
        GlobalRuleManager.LoadMasterRulePool();   // 点击时重试加载（幂等）。
    }

    /// <summary>按当前大师难度状态刷新图标亮/灭。</summary>
    private void RefreshVisual()
    {
        if (difficultIcon != null)
            difficultIcon.SetActive(GlobalRuleManager.IsMasterMode);
    }

    /// <summary>显示切换字幕：出场动画 → 停留 → 退场动画后自动隐藏。</summary>
    private void ShowSubtitle(string text)
    {
        if (subtitleText == null)
            return;

        if (_subtitleRoutine != null)
            StopCoroutine(_subtitleRoutine);

        ResetSubtitleVisual();
        subtitleText.text = text;
        subtitleText.gameObject.SetActive(true);
        _subtitleRoutine = StartCoroutine(SubtitleSequenceRoutine());
    }

    /// <summary>出场（回弹弹入 + 淡入）→ 停留 → 退场（缩小 + 淡出）→ 隐藏并复位。</summary>
    private IEnumerator SubtitleSequenceRoutine()
    {
        RectTransform rt = subtitleText.rectTransform;

        // 出场：0 → 1 回弹过冲弹入 + 淡入。
        float elapsed = 0f;
        while (elapsed < subtitleEnterDuration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / subtitleEnterDuration);
            float back = 1f + 1.70158f * Mathf.Pow(p - 1f, 3f) + 1.70158f * Mathf.Pow(p - 1f, 2f);
            rt.localScale = Vector3.one * Mathf.Lerp(0f, 1f, Mathf.Clamp01(back));
            SetSubtitleAlpha(p);
            yield return null;
        }

        rt.localScale = Vector3.one;
        SetSubtitleAlpha(1f);

        // 停留。
        yield return new WaitForSeconds(subtitleDuration);

        // 退场：缩小到 0 + 淡出。
        elapsed = 0f;
        while (elapsed < subtitleExitDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / subtitleExitDuration);
            rt.localScale = Vector3.one * (1f - t);
            SetSubtitleAlpha(1f - t);
            yield return null;
        }

        subtitleText.gameObject.SetActive(false);
        ResetSubtitleVisual();
        _subtitleRoutine = null;
    }

    /// <summary>字幕文本整体透明度（只动 alpha，不动色相）。</summary>
    private void SetSubtitleAlpha(float alpha)
    {
        if (subtitleText == null)
            return;

        Color color = subtitleText.color;
        color.a = alpha;
        subtitleText.color = color;
    }

    /// <summary>字幕缩放与透明度复位（白色不透明、原始缩放）。</summary>
    private void ResetSubtitleVisual()
    {
        if (subtitleText == null)
            return;

        subtitleText.rectTransform.localScale = Vector3.one;
        Color color = subtitleText.color;
        color.a = 1f;
        subtitleText.color = color;
    }
}
