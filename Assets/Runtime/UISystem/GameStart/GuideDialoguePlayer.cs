using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 选关界面的导游对话：点击关卡后按该关 StageConfig.guideDialogues 依次弹出。
/// 时序（每条）：导游头像弹入并换成条目头像 → 对话背景+文本弹出并播放条目音频
/// → 文本渐上飘、淡出 → 隐藏背景 → 下一条。多句台词逐条重复；无对话的关卡不弹。
/// 订阅 StageButton.Selected（与 StageSelectSelection 共用同一事件源）；
/// 离开关卡选择界面（FSM 状态变化）时自动停止并隐藏。
/// </summary>
[DisallowMultipleComponent]
public sealed class GuideDialoguePlayer : MonoBehaviour
{
    [Header("表现对象")]
    [SerializeField] private Image guideAvatar;             // Guide 导游头像（按条目换 Sprite）。
    [SerializeField] private Image guideSayBackground;      // Guide say 对话背景。
    [SerializeField] private TextMeshProUGUI dialogueText;  // 背景内的对话文本。

    [Header("动画参数")]
    [SerializeField, Min(0.05f)] private float avatarPopDuration = 0.35f;   // 头像弹入时长。
    [SerializeField, Min(0f)] private float avatarHoldDelay = 0.15f;        // 头像弹出后到对话出现的间隔。
    [SerializeField, Min(0.5f)] private float lineDuration = 1.8f;          // 单条对话上飘 + 淡出总时长。
    [SerializeField, Min(0f)] private float riseDistance = 60f;             // 文本上飘距离（画布单位）。
    [SerializeField, Range(0f, 1f)] private float fadeStart = 0.55f;        // 从总时长多少比例开始淡出。
    [SerializeField, Min(0f)] private float lineInterval = 0.25f;           // 两条对话之间的间隔。

    private StageConfig _config;
    private Coroutine _sequence;
    private bool _playing;
    private Vector3 _avatarBaseScale;
    private Vector3 _sayBaseScale;
    private Vector2 _sayBasePos;
    private CanvasGroup _sayGroup;   // Guide say 的 CanvasGroup（整体淡出用；缺失时退化到分别淡出）。

    private void Awake()
    {
        StageButton.Selected += OnStageSelected;
        if (guideAvatar != null)
            _avatarBaseScale = guideAvatar.rectTransform.localScale;
        if (guideSayBackground != null)
        {
            _sayBaseScale = guideSayBackground.rectTransform.localScale;
            _sayBasePos = guideSayBackground.rectTransform.anchoredPosition;
            _sayGroup = guideSayBackground.GetComponent<CanvasGroup>();
        }
        HideAll();
    }

    private void OnDestroy()
    {
        StageButton.Selected -= OnStageSelected;
    }

    private void Update()
    {
        // 离开关卡选择界面（点 GO 进加载/局内、X 回主菜单）时停止对话并隐藏。
        if (!_playing)
            return;

        SceneFSM fsm = SceneFSM.Instance;
        if (fsm != null && fsm.CurrentStateEnum != GameState.StageSelect)
            StopSequence();
    }

    private void OnStageSelected(StageButton button)
    {
        if (button == null)
            return;

        StageConfig config = button.Config;
        if (config == null || config.guideDialogues == null || config.guideDialogues.Count == 0)
        {
            StopSequence();   // 无对话的关卡：清掉正在播放的对话。
            return;
        }

        if (_config == config && _playing)
            return;   // 同一关播放中重复点击不重播。

        _config = config;
        if (_sequence != null)
            StopCoroutine(_sequence);
        _sequence = StartCoroutine(PlaySequence(config));
    }

    private IEnumerator PlaySequence(StageConfig config)
    {
        _playing = true;

        for (int i = 0; i < config.guideDialogues.Count; i++)
        {
            GuideDialogueEntry entry = config.guideDialogues[i];
            if (entry == null || string.IsNullOrEmpty(entry.text))
                continue;

            // 1) 头像弹入并按条目换头像。
            if (guideAvatar != null)
            {
                Sprite avatar = null;
                if (!string.IsNullOrEmpty(entry.avatarSpriteKey) && ResourceManager.Instance != null)
                    avatar = ResourceManager.Instance.GetSprite(entry.avatarSpriteKey);

                if (avatar != null)
                {
                    guideAvatar.sprite = avatar;
                    guideAvatar.gameObject.SetActive(true);
                    yield return AvatarEntrance(guideAvatar.rectTransform, _avatarBaseScale, avatarPopDuration);
                }
                else
                {
                    guideAvatar.gameObject.SetActive(false);
                }
            }

            if (avatarHoldDelay > 0f)
                yield return new WaitForSeconds(avatarHoldDelay);

            // 2) 对话背景 + 文本弹出（文本黑色），播放本条音频；随后整个对话框上飘淡出。
            if (guideSayBackground != null && dialogueText != null)
            {
                dialogueText.text = entry.text;
                dialogueText.color = Color.black;   // 对话文本为黑色。
                if (_sayGroup != null)
                    _sayGroup.alpha = 1f;
                guideSayBackground.rectTransform.anchoredPosition = _sayBasePos;
                guideSayBackground.gameObject.SetActive(true);
                yield return PopIn(guideSayBackground.rectTransform, _sayBaseScale, 0.22f);

                PlayAudio(entry.audioKey);

                // 3) 整个对话框（背景 + 文本）渐上飘、整体淡出，然后隐藏背景。
                yield return RiseAndFade();
                guideSayBackground.gameObject.SetActive(false);
            }
            else
            {
                PlayAudio(entry.audioKey);
            }

            if (lineInterval > 0f)
                yield return new WaitForSeconds(lineInterval);
        }

        if (guideAvatar != null)
            guideAvatar.gameObject.SetActive(false);

        _playing = false;
        _sequence = null;
    }

    /// <summary>缩放弹入：0 倍带回弹过冲弹到基准缩放。</summary>
    private static IEnumerator PopIn(RectTransform rt, Vector3 baseScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            float back = 1f + 1.70158f * Mathf.Pow(p - 1f, 3f) + 1.70158f * Mathf.Pow(p - 1f, 2f);
            rt.localScale = baseScale * Mathf.Lerp(0f, 1f, Mathf.Clamp01(back));
            yield return null;
        }
        rt.localScale = baseScale;
    }

    /// <summary>
    /// 头像趣味出场：回弹过冲放大 + 衰减的挤压拉伸 + 左右摇摆，最后落回基准。
    /// </summary>
    private static IEnumerator AvatarEntrance(RectTransform rt, Vector3 baseScale, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);

            // 回弹过冲缩放（easeOutBack）。
            float back = 1f + 1.70158f * Mathf.Pow(p - 1f, 3f) + 1.70158f * Mathf.Pow(p - 1f, 2f);
            float grow = Mathf.Lerp(0f, 1f, Mathf.Clamp01(back));

            // 挤压拉伸（衰减振荡）与左右摇摆。
            float wave = Mathf.Sin(p * Mathf.PI * 3f) * (1f - p);
            float squash = 1f + 0.16f * wave;
            float stretch = 1f - 0.12f * wave;
            float wobble = wave * 14f;

            rt.localScale = new Vector3(
                baseScale.x * grow * squash,
                baseScale.y * grow * stretch,
                baseScale.z * grow);
            rt.localRotation = Quaternion.Euler(0f, 0f, wobble);
            yield return null;
        }

        rt.localScale = baseScale;
        rt.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// 整个对话框（Guide say 背景 + 内部文本）向上飘动并整体渐变淡出，结束复位。
    /// 优先用 CanvasGroup 统一控制透明度；缺失时退化到分别淡出背景与文本。
    /// </summary>
    private IEnumerator RiseAndFade()
    {
        if (guideSayBackground == null)
            yield break;

        RectTransform rt = guideSayBackground.rectTransform;
        Vector2 startPos = rt.anchoredPosition;
        Image bgImage = guideSayBackground;
        Color bgBase = bgImage != null ? bgImage.color : Color.white;
        Color textBase = dialogueText != null ? dialogueText.color : Color.black;

        float elapsed = 0f;
        float fadeSpan = Mathf.Max(0.001f, 1f - Mathf.Clamp01(fadeStart));

        while (elapsed < lineDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / lineDuration);

            // 整个对话框向上飘。
            rt.anchoredPosition = startPos + Vector2.up * (riseDistance * t);

            // 后半段整体淡出。
            float alpha = 1f;
            if (t > fadeStart)
                alpha = 1f - (t - fadeStart) / fadeSpan;
            alpha = Mathf.Clamp01(alpha);

            if (_sayGroup != null)
            {
                _sayGroup.alpha = alpha;
            }
            else
            {
                if (bgImage != null)
                    bgImage.color = new Color(bgBase.r, bgBase.g, bgBase.b, bgBase.a * alpha);
                if (dialogueText != null)
                    dialogueText.color = new Color(textBase.r, textBase.g, textBase.b, textBase.a * alpha);
            }

            yield return null;
        }

        rt.anchoredPosition = startPos;
        if (_sayGroup != null)
            _sayGroup.alpha = 1f;
        else
        {
            if (bgImage != null)
                bgImage.color = bgBase;
            if (dialogueText != null)
                dialogueText.color = textBase;
        }
    }

    private void PlayAudio(string audioKey)
    {
        if (string.IsNullOrEmpty(audioKey) || ResourceManager.Instance == null || AudioManager.Instance == null)
            return;

        AudioClip clip = ResourceManager.Instance.GetAudio(audioKey);
        if (clip != null)
            AudioManager.Instance.PlayEffectClip(clip, 32, transform);
    }

    private void StopSequence()
    {
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }
        _playing = false;
        _config = null;
        HideAll();
    }

    private void HideAll()
    {
        if (guideAvatar != null)
            guideAvatar.gameObject.SetActive(false);
        if (guideSayBackground != null)
            guideSayBackground.gameObject.SetActive(false);
    }

    /// <summary>关卡配置加载完成后预载导游对话资源（头像精灵 + 音频，幂等）。</summary>
    public static void Preload(StageConfig config)
    {
        if (config == null || config.guideDialogues == null || ResourceManager.Instance == null)
            return;

        for (int i = 0; i < config.guideDialogues.Count; i++)
        {
            GuideDialogueEntry entry = config.guideDialogues[i];
            if (entry == null)
                continue;

            if (!string.IsNullOrEmpty(entry.avatarSpriteKey))
                ResourceManager.Instance.LoadExtraResourceAsync<Sprite>(entry.avatarSpriteKey);
            if (!string.IsNullOrEmpty(entry.audioKey))
                ResourceManager.Instance.LoadExtraResourceAsync<AudioClip>(entry.audioKey);
        }
    }
}
