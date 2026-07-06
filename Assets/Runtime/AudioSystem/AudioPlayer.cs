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
    public bool PlayEffect()
    {
        if (AudioManager.Instance == null || _audioSource.clip == null) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        // 摄像机剔除：超出正交摄像机视野 + 额外半屏宽（cullRadius = 1.5 × halfWidth）才跳过
        float halfWidth  = cam.orthographicSize * cam.aspect;
        float cullRadius = halfWidth * 1.5f;
        float dx = Mathf.Abs(transform.position.x - cam.transform.position.x);
        if (dx >= cullRadius) return false;

        // 动态计算距离，连同 Transform 一起传给 AudioManager（用于实时追踪位置）
        float distance = Vector3.Distance(transform.position, cam.transform.position);
        return AudioManager.Instance.PlayEffect(_audioSource, (uint)_audioSource.priority, distance, transform);
    }

    /// <summary>
    /// 请求播放音效（按索引从同级 GameObjectProperty.audioClips 中取片段）。
    /// 由外部脚本调用，index 对应 StageAudio 配置的 audioKey 顺序（从 0 开始）。
    /// </summary>
    /// <param name="index">audioClips 列表中的索引</param>
    /// <returns>是否成功发起播放</returns>
    public bool PlayEffect(int index)
    {
        var prop = GetComponent<GameObjectProperty>();
        if (prop == null)
        {
            Debug.LogWarning("[AudioPlayer] 未找到同级 GameObjectProperty，无法按索引播放。", this);
            return false;
        }

        if (prop.audioClips == null || prop.audioClips.Count == 0)
        {
            Debug.LogWarning("[AudioPlayer] GameObjectProperty.audioClips 为空，请确认 StageAudio 已完成资源注入。", this);
            return false;
        }

        if (index < 0 || index >= prop.audioClips.Count)
        {
            Debug.LogWarning($"[AudioPlayer] 索引 {index} 超出 audioClips 范围（共 {prop.audioClips.Count} 个）。", this);
            return false;
        }

        AudioClip clip = prop.audioClips[index];
        if (clip == null)
        {
            Debug.LogWarning($"[AudioPlayer] audioClips[{index}] 为 null，跳过播放。", this);
            return false;
        }

        _audioSource.clip = clip;
        return PlayEffect();
    }
}
