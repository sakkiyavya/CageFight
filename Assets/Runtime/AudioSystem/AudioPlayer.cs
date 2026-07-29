using UnityEngine;

/// <summary>
/// 音效播放器，挂载在需要播放音效的预制体上。
/// 外部脚本通过调用 Play() 触发播放。
/// priority 直接通过 AudioSource 组件配置（0 最高，256 最低）。
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioPlayer : MonoBehaviour
{
    private AudioSource _audioSource;                                                     // 当前对象上用于保存播放参数的音频源。

    #region 生命周期与回调
    /// <summary>
    /// 缓存同一对象上的音频源，并禁止其在对象激活时自动播放。
    /// </summary>
    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        _audioSource.playOnAwake = false;
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 根据主摄像机位置判断音效是否在可听范围内，并将播放请求交给全局音频管理器。
    /// </summary>
    /// <returns>音效是否通过有效性和距离检查并被音频管理器接受。</returns>
    private bool Play()
    {
        if (AudioManager.Instance == null || _audioSource.clip == null) return false;

        Camera cam = Camera.main;                                                         // 用于计算剔除范围和声源距离的主摄像机。
        if (cam == null) return false;

        // 摄像机剔除：超出正交摄像机视野 + 额外半屏宽（cullRadius = 1.5 × halfWidth）才跳过
        float halfWidth  = cam.orthographicSize * cam.aspect;                             // 正交摄像机的可视半宽。
        float cullRadius = halfWidth * 1.5f;                                              // 音效请求的水平剔除半径。
        float dx = Mathf.Abs(transform.position.x - cam.transform.position.x);            // 声源与摄像机的水平距离。
        if (dx >= cullRadius) return false;

        // 动态计算距离，连同 Transform 一起传给 AudioManager（用于实时追踪位置）
        float distance = Vector3.Distance(transform.position, cam.transform.position);    // 声源与摄像机的空间距离。
        return AudioManager.Instance.PlayEffect(_audioSource, (uint)_audioSource.priority, distance, transform);
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 按 StageAudio 配置顺序，从同一对象的 <see cref="GameObjectProperty.audioClips"/> 中选择音频并请求播放。
    /// </summary>
    /// <param name="index">从 1 开始的音频序号；1 对应列表中的第一个片段。</param>
    public void PlayEffect(int index)
    {
        int i = index - 1;                                                                // 转换后的零基列表索引。
        var prop = GetComponent<GameObjectProperty>();
        if (prop == null)
        {
            Debug.LogWarning("[AudioPlayer] 未找到同级 GameObjectProperty，无法按索引播放。", this);
            return;
        }

        if (prop.audioClips == null || prop.audioClips.Count == 0)
        {
            Debug.LogWarning("[AudioPlayer] GameObjectProperty.audioClips 为空，请确认 StageAudio 已完成资源注入。", this);
            return;
        }

        if (i < 0 || i >= prop.audioClips.Count)
        {
            Debug.LogWarning($"[AudioPlayer] 索引 {i} 超出 audioClips 范围（共 {prop.audioClips.Count} 个）。", this);
            return;
        }

        AudioClip clip = prop.audioClips[i];                                              // 本次准备播放的音频片段。
        if (clip == null)
        {
            Debug.LogWarning($"[AudioPlayer] audioClips[{i}] 为 null，跳过播放。", this);
            return;
        }

        // Debug.Log("播放音效：" + clip.name);
        _audioSource.clip = clip;
        Play();
    }
    #endregion
}
