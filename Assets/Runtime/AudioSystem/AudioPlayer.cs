using UnityEngine;

/// <summary>
/// 音效播放器，挂载在需要播放音效的预制体上。
/// 外部脚本通过调用 Play() 触发播放。
/// priority 直接通过 AudioSource 组件配置（0 最高，256 最低）。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioPlayer : MonoBehaviour
{
    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }

    /// <summary>
    /// 请求播放音效，由外部脚本调用。
    /// </summary>
    /// <returns>是否成功发起播放</returns>
    public bool Play()
    {
        if (AudioManager.Instance == null || _audioSource.clip == null) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        // 摄像机剔除：超出正交摄像机横向视野则跳过
        float halfWidth = cam.orthographicSize * cam.aspect;
        if (Mathf.Abs(transform.position.x - cam.transform.position.x) > halfWidth)
            return false;

        // 动态计算距离，转发给 AudioManager
        float distance = Vector3.Distance(transform.position, cam.transform.position);
        return AudioManager.Instance.PlayEffect(_audioSource, (uint)_audioSource.priority, distance);
    }
}
