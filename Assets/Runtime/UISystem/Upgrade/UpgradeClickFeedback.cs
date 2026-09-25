using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 升级面板按钮的点击反馈：缩放弹动 + “UI Click”点击音效。
/// 挂在升级面板各按钮（页签/UP）上，与升级逻辑互不干扰（各组件独立接收 PointerDown）。
/// 点击音效经 AudioManager.PlayEffectClip 统一播放入口（规范禁止运行时 AddComponent）。
/// </summary>
public class UpgradeClickFeedback : MonoBehaviour, IPointerDownHandler
{
    private const string ClickSoundKey = "UI Click";

    [SerializeField, Min(0.05f)] private float bounceDuration = 0.16f;
    [SerializeField, Min(0f)] private float bounceScale = 1.15f;

    private Vector3 _baseScale;
    private Coroutine _bounceRoutine;

    private void Awake()
    {
        _baseScale = transform.localScale;

        // 预载点击音效（幂等；已缓存直接返回）。
        if (ResourceManager.Instance != null &&
            ResourceManager.Instance.GetAudio(ClickSoundKey) == null)
        {
            ResourceManager.Instance.LoadExtraResourceAsync<AudioClip>(ClickSoundKey);
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[UpgradeClickFeedback] 按钮 {name} 收到点击。", this);
        PlayBounce();
        PlayClickSound();
    }

    /// <summary>缩放弹动：先放大到 bounceScale 再回落原尺寸。</summary>
    private void PlayBounce()
    {
        if (_bounceRoutine != null)
            StopCoroutine(_bounceRoutine);
        _bounceRoutine = StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        float elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / bounceDuration);
            float s = 1f + (bounceScale - 1f) * Mathf.Sin(t * Mathf.PI);
            transform.localScale = _baseScale * s;
            yield return null;
        }

        transform.localScale = _baseScale;
        _bounceRoutine = null;
    }

    private void PlayClickSound()
    {
        if (ResourceManager.Instance == null || AudioManager.Instance == null)
            return;

        AudioClip clip = ResourceManager.Instance.GetAudio(ClickSoundKey);
        if (clip != null)
            AudioManager.Instance.PlayEffectClip(clip, 32, transform);
    }
}
