using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音频管理器 — 全局单例
/// 管理音乐通道（1个，渐变切换）和音效通道对象池（默认8个，上限16个）
/// 音效通道每帧根据声源 X 轴距离实时更新音量（二次衰减）：
///   距中心 0 → volume = 1；距中心 cullRadius（视野半宽 + 半屏宽）→ volume = 0 并立即回收。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    private const int CHANNEL_DEFAULT = 8;
    private const int POOL_MAX = 16;

    [Tooltip("音乐淡入淡出时长（秒）")]
    [SerializeField] private float fadeDuration = 1f;

    // 音乐通道
    private AudioSource _musicSource;
    private Coroutine _fadeCo;

    // 对象池：所有已创建的音效 AudioSource（空闲 + 活跃）
    private readonly List<AudioSource> _pool = new List<AudioSource>();
    // 活跃通道
    private readonly List<AudioSource> _activeChannels = new List<AudioSource>();
    // 每个活跃通道的声源 Transform 与原始 volume（用于实时音量衰减与距离比较）
    private readonly Dictionary<AudioSource, (Transform origin, float baseVolume)> _channelData
        = new Dictionary<AudioSource, (Transform, float)>();

    // ─────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _musicSource = CreateSource("MusicChannel");
        for (int i = 0; i < CHANNEL_DEFAULT; i++)
            _pool.Add(CreateSource($"SFX_{i}"));
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float halfWidth  = cam.orthographicSize * cam.aspect;
        // 剔除边界 = 视野半宽 + 半屏宽（即 halfWidth 的 1.5 倍）
        float cullRadius = halfWidth * 1.5f;
        float camX = cam.transform.position.x;

        for (int i = _activeChannels.Count - 1; i >= 0; i--)
        {
            var ch = _activeChannels[i];
            var (origin, baseVolume) = _channelData[ch];

            // 声源已销毁 或 播放结束 → 回收
            if (origin == null || !ch.isPlaying)
            {
                Recycle(ch, i);
                continue;
            }

            float dx = Mathf.Abs(origin.position.x - camX);

            // 超出剔除边界 → 立即停止并回收
            if (dx >= cullRadius)
            {
                ch.Stop();
                Recycle(ch, i);
                continue;
            }

            // 二次衰减：t = 1 - dx/cullRadius，volume = baseVolume * t²
            float t = 1f - dx / cullRadius;
            ch.volume = baseVolume * t * t;
        }
    }

    // ─── 内部辅助 ────────────────────────────────────────────

    private AudioSource CreateSource(string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform);
        return go.AddComponent<AudioSource>();
    }

    private void Recycle(AudioSource ch, int index)
    {
        _channelData.Remove(ch);
        _activeChannels.RemoveAt(index);
    }

    /// <summary>从池中取空闲通道；池未满时自动扩容，否则返回 null</summary>
    private AudioSource GetIdleChannel()
    {
        foreach (var src in _pool)
            if (!_activeChannels.Contains(src)) return src;

        if (_pool.Count < POOL_MAX)
        {
            var newSrc = CreateSource($"SFX_{_pool.Count}");
            _pool.Add(newSrc);
            return newSrc;
        }
        return null;
    }

    private void AssignAndPlay(AudioSource channel, AudioSource request, Transform origin)
    {
        channel.clip = request.clip;
        channel.volume = request.volume;
        channel.pitch = request.pitch;
        channel.priority = request.priority;
        channel.loop = false;
        channel.Play();
        _activeChannels.Add(channel);
        _channelData[channel] = (origin, request.volume);
    }

    /// <summary>活跃通道中 priority 最大（优先级最低）的通道</summary>
    private AudioSource GetLowestPriorityChannel()
    {
        AudioSource worst = null;
        int worstVal = -1;
        foreach (var ch in _activeChannels)
            if (ch.priority > worstVal) { worstVal = ch.priority; worst = ch; }
        return worst;
    }

    /// <summary>活跃通道中 priority 最小（优先级最高）的数值</summary>
    private int GetBestPriorityValue()
    {
        int best = int.MaxValue;
        foreach (var ch in _activeChannels)
            if (ch.priority < best) best = ch.priority;
        return best;
    }

    /// <summary>活跃通道中距摄像机 X 轴的最大距离</summary>
    private float GetMaxActiveDistance()
    {
        Camera cam = Camera.main;
        if (cam == null) return 0f;
        float camX = cam.transform.position.x;
        float max = 0f;
        foreach (var (origin, _) in _channelData.Values)
        {
            if (origin == null) continue;
            float dx = Mathf.Abs(origin.position.x - camX);
            if (dx > max) max = dx;
        }
        return max;
    }

    // ─── 对外接口 ─────────────────────────────────────────────

    /// <summary>请求播放背景音乐，自动渐变过渡</summary>
    public bool PlayMusic(AudioSource source)
    {
        if (source == null || source.clip == null) return false;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeMusicTo(source.clip, source.volume));
        return true;
    }

    private IEnumerator FadeMusicTo(AudioClip newClip, float targetVolume)
    {
        // FadeOut
        float startVol = _musicSource.volume;
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            _musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeDuration);
            yield return null;
        }
        _musicSource.Stop();

        // FadeIn
        _musicSource.clip = newClip;
        _musicSource.Play();
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            _musicSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeDuration);
            yield return null;
        }
        _musicSource.volume = targetVolume;
    }

    /// <summary>
    /// 请求播放音效
    /// </summary>
    /// <param name="source">携带 clip 与参数的 AudioSource</param>
    /// <param name="priority">优先级（uint，0 最高）</param>
    /// <param name="distance">请求者距摄像机的 X 轴距离（由 AudioPlayer 传入）</param>
    /// <param name="origin">声源的 Transform，用于实时追踪位置</param>
    public bool PlayEffect(AudioSource source, uint priority, float distance, Transform origin)
    {
        if (source == null || source.clip == null) return false;

        // 分支 1：紧急抢占（priority == 0）
        if (priority == 0 && _activeChannels.Count > 0)
        {
            AudioSource target = GetLowestPriorityChannel();
            target.Stop();
            int idx = _activeChannels.IndexOf(target);
            Recycle(target, idx);
            AssignAndPlay(target, source, origin);
            return true;
        }

        // 分支 2：通道未满
        if (_activeChannels.Count < CHANNEL_DEFAULT)
        {
            AudioSource channel = GetIdleChannel();
            if (channel == null) return false;
            AssignAndPlay(channel, source, origin);
            return true;
        }

        // 分支 3+：通道已满
        if (_activeChannels.Count >= POOL_MAX) return false;

        int bestPriorityVal  = GetBestPriorityValue();
        int worstPriorityVal = GetLowestPriorityChannel().priority;

        // 分支 3a：比最高优先级还高 且 在绝对阈值（10）内
        if ((int)priority < bestPriorityVal && priority < 10)
        {
            AudioSource channel = GetIdleChannel();
            if (channel == null) return false;
            AssignAndPlay(channel, source, origin);
            return true;
        }

        // 分支 3b：优先级介于最高与最低之间 且 距离比最远的近
        if ((int)priority > bestPriorityVal && (int)priority < worstPriorityVal
            && distance < GetMaxActiveDistance())
        {
            AudioSource channel = GetIdleChannel();
            if (channel == null) return false;
            AssignAndPlay(channel, source, origin);
            return true;
        }

        return false;
    }
}
