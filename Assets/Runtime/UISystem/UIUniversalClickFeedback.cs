using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>全局 UI 点击反馈：可点击图标弹动，并播放一次 UI Click。</summary>
public sealed class UIUniversalClickFeedback : MonoBehaviour
{
    private static readonly List<RaycastResult> results = new List<RaycastResult>(8);

    [SerializeField] private float scaleAmount = .1f;
    [SerializeField] private float duration = .12f;
    [SerializeField] private string clickAudioKey = "UI Click";

    private AudioSource source;
    private PointerEventData pointer;
    private RectTransform pressed;
    private Vector3 baseScale;
    private float endTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        EventSystem system = Object.FindObjectOfType<EventSystem>();
        if (system && !system.GetComponent<UIUniversalClickFeedback>())
            system.gameObject.AddComponent<UIUniversalClickFeedback>();
    }

    private void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.priority = 5;
        pointer = new PointerEventData(GetComponent<EventSystem>());
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0)) TryPress();
        if (!pressed) return;

        float t = 1f - Mathf.Clamp01((endTime - Time.unscaledTime) / duration);
        pressed.localScale = baseScale * (1f + Mathf.Sin(t * Mathf.PI) * scaleAmount);
        if (t >= 1f)
        {
            pressed.localScale = baseScale;
            pressed = null;
        }
    }

    private void TryPress()
    {
        EventSystem system = EventSystem.current;
        if (!system) return;

        results.Clear();
        pointer.Reset();
        pointer.position = Input.mousePosition;
        system.RaycastAll(pointer, results);
        for (int i = 0; i < results.Count; i++)
        {
            GameObject target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(results[i].gameObject);
            if (!target) target = ExecuteEvents.GetEventHandler<IPointerDownHandler>(results[i].gameObject);
            if (!target || target.GetComponent<JoyStick>()) continue;

            RectTransform rect = target.GetComponent<RectTransform>();
            if (rect)
            {
                if (pressed) pressed.localScale = baseScale;
                pressed = rect;
                baseScale = rect.localScale;
                endTime = Time.unscaledTime + duration;
            }
            PlayClick();
            return;
        }
    }

    private void PlayClick()
    {
        if (SceneFSM.Instance && SceneFSM.Instance.CurrentStateEnum == GameState.Gameplay) return;
        AudioClip clip = ResourceManager.Instance ? ResourceManager.Instance.GetAudio(clickAudioKey) : null;
        if (!clip || !AudioManager.Instance) return;
        source.clip = clip;
        Transform origin = Camera.main ? Camera.main.transform : transform;
        AudioManager.Instance.PlayEffect(source, 5, 0f, origin);
    }
}
