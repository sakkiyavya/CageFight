using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 图鉴 Card 展示器：挂在 Card.prefab 根对象上。
/// 按对象名自动定位元素并填充数据；附带纯协程动画：
/// 入场 = 柔和升起 + 轻回弹定身 + 内部元素分层错峰（立绘柔入、星/种族轻弹、文本级联滑入）；
/// 切换 = 带倾斜的翻页压扁（缓入）→ 展开轻回弹（缓出）；
/// 退场 = 加速收缩上飘。
/// 根节点是普通 Transform（动画只操作 Transform），内部元素用 as 防护。
/// 错峰动画一律恢复到缓存时记录的原始缩放/位置。
/// </summary>
[DisallowMultipleComponent]
public sealed class CardView : MonoBehaviour
{
    private Image _roleImage;
    private TMP_Text _nameText;
    private TMP_Text _repelText;
    private TMP_Text _quantityText;
    private TMP_Text _scopeText;
    private TMP_Text _timeText;
    private TMP_Text _weightText;
    private TMP_Text _coinText;
    private TMP_Text _explainText;
    private readonly List<Transform> _starSlots = new List<Transform>();
    private readonly Dictionary<string, Transform> _raceIcons = new Dictionary<string, Transform>();
    private bool _cached;

    private Vector3 _roleOriginalScale = Vector3.one;
    private readonly List<Vector3> _starOriginalScales = new List<Vector3>();
    private readonly Dictionary<string, Vector3> _raceOriginalScales = new Dictionary<string, Vector3>();
    private readonly Dictionary<TMP_Text, Vector3> _textOriginalPositions = new Dictionary<TMP_Text, Vector3>();

    private Coroutine _animCoroutine;
    private Vector3 _basePosition;
    private Quaternion _baseRotation;
    private bool _basePoseCached;

    private void Awake()
    {
        CacheElements();
    }

    #region 数据填充
    /// <summary>按条目填充 Card：立绘、名字、六行文本、简介、星级与种族图标亮起。</summary>
    public void Fill(BookEntry entry)
    {
        if (entry == null)
            return;

        CacheElements();

        if (_roleImage != null)
        {
            _roleImage.preserveAspect = true;
            if (!string.IsNullOrEmpty(entry.roleSpriteKey) && ResourceManager.Instance != null)
            {
                ResourceManager.Instance.LoadExtraResourceAsync<Sprite>(entry.roleSpriteKey, sprite =>
                {
                    if (sprite != null && _roleImage != null)
                        _roleImage.sprite = sprite;
                });
            }
            else
            {
                Sprite fallback = entry.RoleEditorFallback;
                if (fallback != null)
                    _roleImage.sprite = fallback;
            }
        }

        if (_nameText != null) _nameText.text = entry.displayName;
        if (_repelText != null) _repelText.text = entry.repelText;
        if (_quantityText != null) _quantityText.text = entry.quantityText;
        if (_scopeText != null) _scopeText.text = entry.scopeText;
        if (_timeText != null) _timeText.text = entry.timeText;
        if (_weightText != null) _weightText.text = entry.weightText;
        if (_coinText != null) _coinText.text = entry.coinText;
        if (_explainText != null) _explainText.text = entry.explainText;

        // 星级：只亮选中的那一颗。
        for (int i = 0; i < _starSlots.Count; i++)
        {
            if (_starSlots[i] != null)
                _starSlots[i].gameObject.SetActive((i + 1) == entry.star);
        }

        // 种族图标：选中亮起、其余全灭。
        foreach (KeyValuePair<string, Transform> pair in _raceIcons)
        {
            if (pair.Value != null)
                pair.Value.gameObject.SetActive(pair.Key == entry.raceId);
        }
    }
    #endregion

    #region 出场 / 退场 / 切换动画
    /// <summary>出场：柔和升起 + 轻回弹定身 + 内部元素分层错峰。</summary>
    public void PlayEnter(BookEntry entry)
    {
        StopCurrentAnim();
        CacheBasePose();

        // 根节点是普通 Transform，不可强转 RectTransform。
        Transform root = transform;
        root.localScale = Vector3.zero;
        Fill(entry);
        PrepareStaggerInitialState();

        _animCoroutine = StartCoroutine(EnterRoutine());
    }

    /// <summary>切换：带倾斜的翻页压扁（缓入）→ 换数据 → 展开轻回弹（缓出）+ 错峰。</summary>
    public void PlaySwitch(BookEntry entry)
    {
        StopCurrentAnim();
        CacheBasePose();
        _animCoroutine = StartCoroutine(SwitchRoutine(entry));
    }

    /// <summary>退场：加速收缩上飘，结束后回调。</summary>
    public void PlayExit(Action onDone)
    {
        StopCurrentAnim();
        CacheBasePose();
        _animCoroutine = StartCoroutine(ExitRoutine(onDone));
    }

    private void StopCurrentAnim()
    {
        if (_animCoroutine != null)
        {
            StopCoroutine(_animCoroutine);
            _animCoroutine = null;
        }
        ResetCardPose();
    }

    private void CacheBasePose()
    {
        if (_basePoseCached)
            return;

        _basePoseCached = true;
        _basePosition = transform.localPosition;
        _baseRotation = transform.localRotation;
    }

    private void ResetCardPose()
    {
        if (!_basePoseCached)
            return;

        transform.localScale = Vector3.one;
        transform.localPosition = _basePosition;
        transform.localRotation = _baseRotation;
    }

    private IEnumerator EnterRoutine()
    {
        Transform root = transform;

        // 主段：0.86 -> 1 轻回弹（峰值约 1.03），-4° 摆正，下方 56px 升起。全部缓出，不生硬。
        float duration = 0.5f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float p = Mathf.Clamp01(t);
            root.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, EaseOutBackSoft(p));
            root.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, -4f * (1f - EaseOutQuint(p)));
            root.localPosition = _basePosition + Vector3.up * (56f * (1f - EaseOutQuint(p)));
            yield return null;
        }

        ResetCardPose();
        yield return StartCoroutine(StaggerInnerRoutine());
    }

    private IEnumerator SwitchRoutine(BookEntry entry)
    {
        Transform root = transform;

        // 压扁段：缓入 + 轻微右倾（翻页手感）。
        float squash = 0.1f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / squash;
            float p = Mathf.Clamp01(t);
            float e = EaseInCubic(p);
            root.localScale = new Vector3(1f - e, 1f + e * 0.06f, 1f);
            root.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, 2.5f * e);
            yield return null;
        }

        Fill(entry);
        PrepareStaggerInitialState();

        // 展开段：缓出 + 尾端轻回弹，倾斜归正。
        float expand = 0.18f;
        t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / expand;
            float p = Mathf.Clamp01(t);
            float e = EaseOutBackSoft(p);
            root.localScale = new Vector3(e, 1f + (1f - p) * 0.08f, 1f);
            root.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, 2.5f * (1f - EaseOutQuint(p)));
            yield return null;
        }

        root.localScale = Vector3.one;
        root.localRotation = _baseRotation;
        yield return StartCoroutine(StaggerInnerRoutine());
    }

    private IEnumerator ExitRoutine(Action onDone)
    {
        Transform root = transform;

        // 加速收缩 + 上升 + 轻微左倾，像被吸走。
        float duration = 0.2f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float p = Mathf.Clamp01(t);
            float e = EaseInCubic(p);
            root.localScale = Vector3.one * (1f - e * 0.96f);
            root.localPosition = _basePosition + Vector3.up * (46f * e);
            root.localRotation = _baseRotation * Quaternion.Euler(0f, 0f, -3f * e);
            yield return null;
        }

        ResetCardPose();
        onDone?.Invoke();
    }

    /// <summary>内部分层错峰：立绘柔入 → 星轻弹 → 种族轻弹 → 文本级联滑入（全部缓出）。</summary>
    private IEnumerator StaggerInnerRoutine()
    {
        if (_roleImage != null)
        {
            // 立绘柔入：从 0.92 柔和放大，无过冲。
            RectTransform roleRt = _roleImage.rectTransform;
            float duration = 0.22f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / duration;
                float p = Mathf.Clamp01(t);
                roleRt.localScale = _roleOriginalScale * Mathf.Lerp(0.92f, 1f, EaseOutQuint(p));
                yield return null;
            }
            roleRt.localScale = _roleOriginalScale;
            yield return WaitFrames(2);
        }

        // 星：轻弹（小过冲 ~1.08）。
        for (int i = 0; i < _starSlots.Count; i++)
        {
            Transform slot = _starSlots[i];
            if (slot == null || !slot.gameObject.activeSelf)
                continue;

            Vector3 target = i < _starOriginalScales.Count ? _starOriginalScales[i] : Vector3.one;
            yield return StartCoroutine(PopRoutine(slot as RectTransform, target, 0.35f, 0.13f, EaseOutBackSoft));
            yield return WaitFrames(3);
        }

        // 种族：轻弹。
        foreach (KeyValuePair<string, Transform> pair in _raceIcons)
        {
            Transform icon = pair.Value;
            if (icon == null || !icon.gameObject.activeSelf)
                continue;

            Vector3 target = _raceOriginalScales.ContainsKey(pair.Key) ? _raceOriginalScales[pair.Key] : Vector3.one;
            yield return StartCoroutine(PopRoutine(icon as RectTransform, target, 0.35f, 0.13f, EaseOutBackSoft));
            yield return WaitFrames(3);
        }

        // 文本：级联滑入（20px，缓出五阶，无过冲）。
        TMP_Text[] texts = { _nameText, _repelText, _quantityText, _scopeText, _timeText, _weightText, _coinText, _explainText };
        foreach (TMP_Text text in texts)
        {
            if (text == null)
                continue;

            Vector3 target = _textOriginalPositions.ContainsKey(text) ? _textOriginalPositions[text] : text.rectTransform.localPosition;
            yield return StartCoroutine(SlideInRoutine(text.rectTransform, target));
            yield return WaitFrames(3);
        }
    }

    private IEnumerator PopRoutine(RectTransform rt, Vector3 targetScale, float fromScale, float duration, Func<float, float> ease)
    {
        if (rt == null)
            yield break;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float p = Mathf.Clamp01(t);
            rt.localScale = targetScale * Mathf.Lerp(fromScale, 1f, ease(p));
            yield return null;
        }
        rt.localScale = targetScale;
    }

    private IEnumerator SlideInRoutine(RectTransform rt, Vector3 targetPosition)
    {
        if (rt == null)
            yield break;

        float duration = 0.14f;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            float p = Mathf.Clamp01(t);
            rt.localPosition = targetPosition + Vector3.right * (20f * (1f - EaseOutQuint(p)));
            yield return null;
        }
        rt.localPosition = targetPosition;
    }

    private static IEnumerator WaitFrames(int frames)
    {
        for (int i = 0; i < frames; i++)
            yield return null;
    }

    private void PrepareStaggerInitialState()
    {
        if (_roleImage != null)
            _roleImage.rectTransform.localScale = Vector3.zero;

        for (int i = 0; i < _starSlots.Count; i++)
        {
            Transform slot = _starSlots[i];
            if (slot != null && slot.gameObject.activeSelf)
                slot.localScale = Vector3.zero;
        }

        foreach (KeyValuePair<string, Transform> pair in _raceIcons)
        {
            Transform icon = pair.Value;
            if (icon != null && icon.gameObject.activeSelf)
                icon.localScale = Vector3.zero;
        }

        TMP_Text[] texts = { _nameText, _repelText, _quantityText, _scopeText, _timeText, _weightText, _coinText, _explainText };
        foreach (TMP_Text text in texts)
        {
            if (text == null)
                continue;

            Vector3 original = _textOriginalPositions.ContainsKey(text) ? _textOriginalPositions[text] : text.rectTransform.localPosition;
            text.rectTransform.localPosition = original + Vector3.right * 20f;
        }
    }

    #region 缓动曲线
    private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    private static float EaseOutQuint(float t) => 1f - Mathf.Pow(1f - t, 5f);
    private static float EaseInCubic(float t) => t * t * t;

    /// <summary>轻回弹（峰值约 1.03，适合卡面定身）。</summary>
    private static float EaseOutBackSoft(float t)
    {
        const float c1 = 1.35f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    #endregion
    #endregion

    #region 元素缓存
    private void CacheElements()
    {
        if (_cached)
            return;

        _cached = true;

        Transform role = FindDeep("Role");
        if (role != null)
        {
            _roleImage = role.GetComponentInChildren<Image>(true);
            if (_roleImage != null)
                _roleOriginalScale = _roleImage.rectTransform.localScale;
        }

        Transform back1 = FindDeep("back1");
        if (back1 != null)
        {
            Transform nameNode = back1.Find("Image");
            if (nameNode != null)
                _nameText = nameNode.GetComponentInChildren<TMP_Text>(true);
        }

        _repelText = FindTextIn("Repel");
        _quantityText = FindTextIn("Quiantity");
        _scopeText = FindTextIn("Scope");
        _timeText = FindTextIn("time");
        _weightText = FindTextIn("weight");
        _coinText = FindTextIn("coin");
        _explainText = FindTextIn("Explain");

        TMP_Text[] texts = { _nameText, _repelText, _quantityText, _scopeText, _timeText, _weightText, _coinText, _explainText };
        foreach (TMP_Text text in texts)
        {
            if (text != null)
                _textOriginalPositions[text] = text.rectTransform.localPosition;
        }

        _starSlots.Clear();
        _starOriginalScales.Clear();
        for (int i = 1; i <= 7; i++)
        {
            Transform star = FindDeep(i.ToString());
            _starSlots.Add(star);
            _starOriginalScales.Add(star != null ? star.localScale : Vector3.one);
        }

        _raceIcons.Clear();
        _raceOriginalScales.Clear();
        Transform raceRoot = FindDeep("Race");
        if (raceRoot != null)
        {
            for (int i = 0; i < raceRoot.childCount; i++)
            {
                Transform child = raceRoot.GetChild(i);
                _raceIcons[child.name] = child;
                _raceOriginalScales[child.name] = child.localScale;
            }
        }
    }

    private TMP_Text FindTextIn(string objectName)
    {
        Transform node = FindDeep(objectName);
        return node != null ? node.GetComponentInChildren<TMP_Text>(true) : null;
    }

    private Transform FindDeep(string objectName)
    {
        if (string.IsNullOrEmpty(objectName))
            return null;

        Transform[] all = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == objectName)
                return all[i];
        }
        return null;
    }
    #endregion
}