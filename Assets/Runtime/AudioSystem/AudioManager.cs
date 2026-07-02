using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 音频管理器 — 全局单例
/// 管理音乐通道（1个，渐变切换）和音效通道对象池（默认8个，上限16个）
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
    // 每个活跃通道播放时对应的请求距离（用于满通道时的距离比较）
    private readonly Dictionary<AudioSource, float> _channelDistances = new Dictionary<AudioSource, float>();

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
        // 回收播放结束的通道
        for (int i = _activeChannels.Count - 1; i >= 0; i--)
        {
            if (!_activeChannels[i].isPlaying)
            {
                _channelDistances.Remove(_activeChannels[i]);
                _activeChannels.RemoveAt(i);
            }
        }
    }

    // ─── 内部辅助 ────────────────────────────────────────────

    private AudioSource CreateSource(string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform);
        return go.AddComponent<AudioSource>();
    }

    /// <summary>从池中取一个空闲通道；池未满时自动扩容，否则返回 null</summary>
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

    private void AssignAndPlay(AudioSource channel, AudioSource request, float distance)
    {
        channel.clip = request.clip;
        channel.volume = request.volume;
        channel.pitch = request.pitch;
        channel.priority = request.priority;
        channel.loop = false;
        channel.Play();
        _activeChannels.Add(channel);
        _channelDistances[channel] = distance;
    }

    /// <summary>返回当前活跃通道中 priority 值最大（优先级最低）的通道</summary>
    private AudioSource GetLowestPriorityChannel()
    {
        AudioSource worst = null;
        int worstVal = -1;
        foreach (var ch in _activeChannels)
        {
            if (ch.priority > worstVal) { worstVal = ch.priority; worst = ch; }
        }
        return worst;
    }

    /// <summary>当前活跃通道中 priority 值最小（优先级最高）的数值</summary>
    private int GetBestPriorityValue()
    {
        int best = int.MaxValue;
        foreach (var ch in _activeChannels)
            if (ch.priority < best) best = ch.priority;
        return best;
    }

    /// <summary>当前活跃通道中记录的最大请求距离</summary>
    private float GetMaxActiveDistance()
    {
        float max = 0f;
        foreach (var d in _channelDistances.Values)
            if (d > max) max = d;
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
    /// <param name="distance">请求者距主摄像机的距离</param>
    public bool PlayEffect(AudioSource source, uint priority, float distance)
    {
        if (source == null || source.clip == null) return false;

        // 分支 1：紧急抢占（priority == 0）
        if (priority == 0 && _activeChannels.Count > 0)
        {
            AudioSource target = GetLowestPriorityChannel();
            target.Stop();
            _channelDistances.Remove(target);
            _activeChannels.Remove(target);
            AssignAndPlay(target, source, distance);
            return true;
        }

        // 分支 2：通道未满，直接分配
        if (_activeChannels.Count < CHANNEL_DEFAULT)
        {
            AudioSource channel = GetIdleChannel();
            if (channel == null) return false;
            AssignAndPlay(channel, source, distance);
            return true;
        }

        // 分支 3+：通道已满，先检查池是否耗尽
        if (_activeChannels.Count >= POOL_MAX) return false;

        int bestPriorityVal  = GetBestPriorityValue();
        int worstPriorityVal = GetLowestPriorityChannel().priority;

        // 分支 3a：比最高优先级还高 且 在绝对阈值（10）内
        if ((int)priority < bestPriorityVal && priority < 10)
        {
            AudioSource channel = GetIdleChannel();
            if (channel == null) return false;
            AssignAndPlay(channel, source, distance);
            return true;
        }

        // 分支 3b：优先级介于最高与最低之间 且 距离比当前最远的近
        if ((int)priority > bestPriorityVal && (int)priority < worstPriorityVal
            && distance < GetMaxActiveDistance())
        {
            AudioSource channel = GetIdleChannel();
            if (channel == null) return false;
            AssignAndPlay(channel, source, distance);
            return true;
        }

        return false;
    }
}
