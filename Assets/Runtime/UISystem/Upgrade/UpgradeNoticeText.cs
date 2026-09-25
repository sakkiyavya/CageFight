using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 升级面板的提示文本：货币不足等提示。
/// 表现：红色字幕从屏幕中间上方出现，逐渐向上飘动，后半段渐变淡出后隐藏。
/// 挂在 Upgrade Canvas 下的提示 TMP 对象上（默认隐藏）。
/// </summary>
public class UpgradeNoticeText : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float displaySeconds = 1.5f;   // 飘动总时长（含淡出段）。
    [SerializeField, Min(0f)] private float riseDistance = 60f;        // 向上飘动距离（画布单位）。
    [SerializeField, Range(0f, 1f)] private float fadeStart = 0.55f;   // 从总时长多少比例处开始淡出（之前保持不透明）。

    private static UpgradeNoticeText _instance;
    private TMP_Text _text;
    private Coroutine _hideRoutine;
    private Vector2 _baseAnchoredPosition;

    private static readonly Color NoticeRed = new Color(1f, 0.25f, 0.25f, 1f);

    private void Awake()
    {
        _instance = this;
        _text = GetComponent<TMP_Text>();
        RectTransform rt = transform as RectTransform;
        _baseAnchoredPosition = rt != null ? rt.anchoredPosition : Vector2.zero;
        if (_text != null)
            gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    /// <summary>显示一条红色提示：向上飘动并渐变淡出后自动隐藏。</summary>
    public static void Show(string message)
    {
        UpgradeNoticeText notice = _instance;
        if (notice == null)
        {
            notice = FindObjectOfType<UpgradeNoticeText>();
        }

        // 兜底：FindObjectOfType 找不到未激活对象，改用全量查找（含未激活）。
        if (notice == null)
        {
            UpgradeNoticeText[] all = Resources.FindObjectsOfTypeAll<UpgradeNoticeText>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.scene.IsValid())
                {
                    notice = all[i];
                    break;
                }
            }
            _instance = notice;
        }

        if (notice == null)
        {
            Debug.LogError($"[UpgradeNoticeText] 场景中找不到 UpgradeNotice 提示对象，无法显示：{message}");
            return;
        }

        if (notice._text == null)
            notice._text = notice.GetComponent<TMP_Text>();

        if (notice._text == null)
        {
            Debug.LogError("[UpgradeNoticeText] UpgradeNotice 上缺少 TMP 文本组件，无法显示。", notice);
            return;
        }

        notice._text.text = message;
        notice._text.color = NoticeRed;
        if (notice._hideRoutine != null)
            notice.StopCoroutine(notice._hideRoutine);

        // 必须先激活再启动协程：未激活对象上的协程不会执行（否则字幕永远不出现）。
        notice.gameObject.SetActive(true);
        notice._hideRoutine = notice.StartCoroutine(notice.RiseAndFadeRoutine());
    }

    /// <summary>上飘 + 淡出：从基准位置开始上浮 riseDistance，后半段透明度渐变到 0，结束复位隐藏。</summary>
    private IEnumerator RiseAndFadeRoutine()
    {
        RectTransform rt = transform as RectTransform;
        if (rt != null)
            rt.anchoredPosition = _baseAnchoredPosition;   // 每次从基准位置开始（重复提示不叠加偏移）。

        float elapsed = 0f;
        float fadeSpan = Mathf.Max(0.001f, 1f - Mathf.Clamp01(fadeStart));
        while (elapsed < displaySeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / displaySeconds);

            if (rt != null)
                rt.anchoredPosition = _baseAnchoredPosition + Vector2.up * (riseDistance * t);

            float alpha = 1f;
            if (t > fadeStart)
                alpha = 1f - (t - fadeStart) / fadeSpan;
            _text.color = new Color(NoticeRed.r, NoticeRed.g, NoticeRed.b, Mathf.Clamp01(alpha));

            yield return null;
        }

        if (rt != null)
            rt.anchoredPosition = _baseAnchoredPosition;
        _text.color = NoticeRed;
        gameObject.SetActive(false);
        _hideRoutine = null;
    }
}
