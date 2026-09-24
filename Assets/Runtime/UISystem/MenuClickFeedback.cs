using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 菜单阶段全局点击反馈（框架规范内实现）：
/// 监听指针按下，命中任意可交互 Button 时：
///   1) 该按钮做一次轻回弹缩放（1 → pressedScale → 1，正弦缓动，不改变原始缩放）；
///   2) 经 AudioManager 播放 "UI Click"（框架音频键，持久预载列表内，2D 非循环）。
/// 仅在 SceneFSM 处于 Menu 状态（进入关卡之前）时生效；射线检测与 UIStack
/// 的空白点击检测使用同一套 EventSystem + GraphicRaycaster 机制，不需要逐按钮接线。
/// 音效经 AudioManager.PlayEffectClip 直接发起请求（规范禁止业务运行时 AddComponent）。
/// </summary>
[DisallowMultipleComponent]
public sealed class MenuClickFeedback : MonoBehaviour
{
    [SerializeField, Range(0.5f, 1f), Tooltip("按下时的缩放谷值（0.88 = 缩小到 88%）。")]
    private float pressedScale = 0.88f;

    [SerializeField, Min(0.02f), Tooltip("回弹时长（秒）。")]
    private float bounceDuration = 0.12f;

    [SerializeField, Tooltip("点击音效资源键（AudioRegistry 键，须在持久预载列表中）。")]
    private string clickSoundKey = "UI Click";

    private EventSystem _eventSystem;
    private readonly Dictionary<Transform, Vector3> _originalScales = new Dictionary<Transform, Vector3>();
    private readonly Dictionary<Transform, Coroutine> _activeBounces = new Dictionary<Transform, Coroutine>();

    private void Awake()
    {
        _eventSystem = EventSystem.current;
    }

    private void Update()
    {
        // 仅菜单阶段（进入关卡之前）生效。
        if (SceneFSM.Instance == null || SceneFSM.Instance.CurrentStateEnum != GameState.Menu)
            return;

        if (_eventSystem == null)
            _eventSystem = EventSystem.current;

        if (!TryGetPointerDownPosition(out Vector2 position))
            return;

        Button button = RaycastButton(position);
        if (button == null || !button.interactable || !button.enabled || !button.gameObject.activeInHierarchy)
            return;

        Bounce(button.transform);
        PlayClickSound();
    }

    /// <summary>对按钮做一次轻回弹缩放（正弦缓动：1 → 谷值 → 1），结束后恢复原始缩放。</summary>
    private void Bounce(Transform target)
    {
        if (!_originalScales.TryGetValue(target, out Vector3 original))
        {
            original = target.localScale;
            _originalScales[target] = original;
        }

        if (_activeBounces.TryGetValue(target, out Coroutine running) && running != null)
            StopCoroutine(running);

        _activeBounces[target] = StartCoroutine(BounceRoutine(target, original));
    }

    private IEnumerator BounceRoutine(Transform target, Vector3 original)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / bounceDuration;
            float p = Mathf.Clamp01(t);
            float dip = 1f - (1f - pressedScale) * Mathf.Sin(Mathf.PI * p);   // 平滑下压再弹起。
            target.localScale = original * dip;
            yield return null;
        }

        target.localScale = original;
        _activeBounces.Remove(target);
    }

    /// <summary>经框架音频调度播放 UI Click（资源键 → AudioManager，规范禁止直接 PlayOneShot 绕过）。</summary>
    private void PlayClickSound()
    {
        if (AudioManager.Instance == null || ResourceManager.Instance == null)
            return;

        AudioClip clip = ResourceManager.Instance.GetAudio(clickSoundKey);
        if (clip == null)
            return;

        AudioManager.Instance.PlayEffectClip(clip, 32, transform);
    }

    /// <summary>
    /// 对指针位置做 UI 射线，返回命中最上层的 Button。
    /// 使用 EventSystem.RaycastAll：由事件系统统一调度所有激活 Canvas 的 GraphicRaycaster
    /// 并按要求排序，避免“组件所在对象没有射线器”导致永远命中不到按钮的问题。
    /// </summary>
    private Button RaycastButton(Vector2 screenPosition)
    {
        if (_eventSystem == null)
            return null;

        var pointerData = new PointerEventData(_eventSystem) { position = screenPosition };
        var results = new List<RaycastResult>();
        _eventSystem.RaycastAll(pointerData, results);

        for (int i = 0; i < results.Count; i++)
        {
            Button button = results[i].gameObject.GetComponentInParent<Button>();
            if (button != null)
                return button;
        }
        return null;
    }

    /// <summary>鼠标左键按下或第一根手指落下时返回 true，并输出屏幕坐标（兼容鼠标与触屏）。</summary>
    private bool TryGetPointerDownPosition(out Vector2 position)
    {
        if (Input.GetMouseButtonDown(0))
        {
            position = Input.mousePosition;
            return true;
        }

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            position = Input.GetTouch(0).position;
            return true;
        }

        position = default;
        return false;
    }
}