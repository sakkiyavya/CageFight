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

    private const int CHANNEL_DEFAULT = 8;                                           // 默认并发音效通道数量。
    private const int POOL_MAX = 16;                                                 // 音效通道池允许扩展到的上限。

    [Tooltip("音乐淡入淡出时长（秒）")]
    [SerializeField] private float fadeDuration = 1f;                                // 背景音乐淡出和淡入各自使用的时长。

    // 音乐通道
    private AudioSource _musicSource;                                                // 专用于播放背景音乐的通道。
    private Coroutine _fadeCo;                                                       // 当前正在执行的音乐切换协程。

    // 对象池：所有已创建的音效 AudioSource（空闲 + 活跃）
    private readonly List<AudioSource> _pool = new List<AudioSource>();              // 已创建的全部音效通道。
    // 活跃通道
    private readonly List<AudioSource> _activeChannels = new List<AudioSource>();    // 当前正在播放的音效通道。
    // 每个活跃通道的声源 Transform 与原始 volume（用于实时音量衰减与距离比较）
    private readonly Dictionary<AudioSource, (Transform origin, float baseVolume)> _channelData
        = new Dictionary<AudioSource, (Transform, float)>();                         // 活跃通道到声源位置及原始音量的跟踪数据。

    // ─────────────────────────────────────────────────────────

    #region 生命周期与回调
    /// <summary>
    /// 建立持久化单例，创建背景音乐通道，并预热默认数量的音效通道。
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // MenuAmbientAudio 由场景显式挂载（规范：禁止核心单例 Awake 隐式自动安装外部模块）。

        _musicSource = CreateSource("MusicChannel");
        for (int i = 0; i < CHANNEL_DEFAULT; i++)
            _pool.Add(CreateSource($"SFX_{i}"));
    }

    /// <summary>
    /// 每帧回收播放结束或失去来源对象的音效通道，并按声源与主摄像机的水平距离更新音量。
    /// 超出剔除半径的音效会立即停止。
    /// </summary>
    private void Update()
    {
        Camera cam = MainCamera;                                                     // 当前用于距离衰减的主摄像机（本服务缓存）。
        if (cam == null) return;

        float halfWidth  = cam.orthographicSize * cam.aspect;                        // 正交摄像机的可视半宽。
        // 剔除边界 = 视野半宽 + 半屏宽（即 halfWidth 的 1.5 倍）
        float cullRadius = halfWidth * 1.5f;                                         // 音效停止播放的水平距离阈值。
        float camX = cam.transform.position.x;                                       // 摄像机当前的水平坐标。

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

            float dx = Mathf.Abs(origin.position.x - camX);                          // 声源与摄像机的水平距离。

            // 超出剔除边界 → 立即停止并回收
            if (dx >= cullRadius)
            {
                ch.Stop();
                Recycle(ch, i);
                continue;
            }

            // 二次衰减：t = 1 - dx/cullRadius，volume = baseVolume * t²
            float t = 1f - dx / cullRadius;                                          // 归一化后的剩余可听比例。
            ch.volume = baseVolume * t * t;
        }
    }
    #endregion

    // ─── 内部辅助 ────────────────────────────────────────────

    #region 音效通道管理
    /// <summary>
    /// 创建一个挂在音频管理器子节点下的独立音频通道。
    /// </summary>
    /// <param name="label">新通道对象在层级面板中使用的名称。</param>
    /// <returns>新对象上的 <see cref="AudioSource"/> 组件。</returns>
    private AudioSource CreateSource(string label)
    {
        var go = new GameObject(label);
        go.transform.SetParent(transform);
        return go.AddComponent<AudioSource>();
    }

    /// <summary>
    /// 从活跃集合和运行时数据表中移除已经结束的音效通道，使其可以再次被分配。
    /// </summary>
    /// <param name="ch">需要回收的音效通道。</param>
    /// <param name="index">该通道在活跃列表中的索引。</param>
    private void Recycle(AudioSource ch, int index)
    {
        _channelData.Remove(ch);
        _activeChannels.RemoveAt(index);
    }

    /// <summary>
    /// 获取一个未被占用的音效通道；没有空闲通道时会在池上限内自动扩容。
    /// </summary>
    /// <returns>可用的音效通道；通道池达到上限时返回 <see langword="null"/>。</returns>
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

    /// <summary>
    /// 将请求音频源上的播放参数复制到池化通道，开始播放并登记实时跟踪数据。
    /// </summary>
    /// <param name="channel">用于实际播放的池化音效通道。</param>
    /// <param name="request">提供音频片段、音量、音调和优先级的请求音频源。</param>
    /// <param name="origin">需要持续跟踪位置的原始声源对象。</param>
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

    /// <summary>
    /// 查找活跃通道中优先级最低的通道，即 <see cref="AudioSource.priority"/> 数值最大的通道。
    /// </summary>
    /// <returns>优先级最低的活跃通道；没有活跃通道时返回 <see langword="null"/>。</returns>
    private AudioSource GetLowestPriorityChannel()
    {
        AudioSource worst = null;                                                    // 当前找到的最低优先级通道。
        int worstVal = -1;                                                           // 当前最大的 priority 数值。
        foreach (var ch in _activeChannels)
            if (ch.priority > worstVal) { worstVal = ch.priority; worst = ch; }
        return worst;
    }

    /// <summary>
    /// 获取活跃通道中最高优先级对应的最小 priority 数值。
    /// </summary>
    /// <returns>最高优先级数值；没有活跃通道时返回 <see cref="int.MaxValue"/>。</returns>
    private int GetBestPriorityValue()
    {
        int best = int.MaxValue;                                                     // 当前最小的 priority 数值。
        foreach (var ch in _activeChannels)
            if (ch.priority < best) best = ch.priority;
        return best;
    }

    /// <summary>
    /// 计算所有有效活跃声源与主摄像机之间最大的水平距离。
    /// </summary>
    /// <returns>最远活跃声源的水平距离；没有主摄像机或有效声源时返回 0。</returns>
    private float GetMaxActiveDistance()
    {
        Camera cam = MainCamera;                                                    // 当前主摄像机（本服务缓存）。
        if (cam == null) return 0f;
        float camX = cam.transform.position.x;                                       // 摄像机水平坐标。
        float max = 0f;                                                              // 当前找到的最大水平距离。
        foreach (var (origin, _) in _channelData.Values)
        {
            if (origin == null) continue;
            float dx = Mathf.Abs(origin.position.x - camX);                          // 当前声源的水平距离。
            if (dx > max) max = dx;
        }
        return max;
    }
    #endregion

    // ─── 对外接口 ─────────────────────────────────────────────

    #region 背景音乐入口
    /// <summary>
    /// 请求切换背景音乐，通过淡出当前音乐再淡入新片段完成过渡。
    /// </summary>
    /// <param name="source">提供新音乐片段和目标音量的音频源。</param>
    /// <returns>请求是否有效并成功启动音乐切换协程。</returns>
    public bool PlayMusic(AudioSource source)
    {
        if (source == null || source.clip == null) return false;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeMusicTo(source.clip, source.volume, source.loop));
        return true;
    }

    /// <summary>
    /// 按资源键请求切换背景音乐（框架统一入口：解析音频片段后走淡入淡出通道，循环播放）。
    /// 业务脚本不得自行创建 AudioSource 或直调 AudioSource.Play。
    /// </summary>
    /// <param name="audioKey">背景音乐资源键。</param>
    /// <param name="volume">淡入完成后的目标音量。</param>
    /// <returns>片段已解析且成功启动切换协程时返回 <see langword="true"/>。</returns>
    public bool PlayMusic(string audioKey, float volume = 1f)
    {
        if (string.IsNullOrEmpty(audioKey) || ResourceManager.Instance == null)
            return false;

        AudioClip clip = ResourceManager.Instance.GetAudio(audioKey);
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] 背景音乐资源未加载：{audioKey}，请确认已列入关卡预载清单。", this);
            return false;
        }

        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeMusicTo(clip, Mathf.Clamp01(volume), true));
        return true;
    }

    /// <summary>平滑停止当前背景音乐。</summary>
    public void StopMusic()
    {
        if (_musicSource == null || !_musicSource.isPlaying) return;
        if (_fadeCo != null) StopCoroutine(_fadeCo);
        _fadeCo = StartCoroutine(FadeMusicOut());
    }
    #endregion

    #region 背景音乐过渡
    /// <summary>
    /// 将当前音乐音量逐步降至零，替换音频片段后再淡入到目标音量。
    /// </summary>
    /// <param name="newClip">淡出完成后开始播放的新音乐片段。</param>
    /// <param name="targetVolume">新音乐淡入完成后的最终音量。</param>
    /// <returns>等待旧音乐淡出并将新音乐淡入至目标音量的协程。</returns>
    private IEnumerator FadeMusicTo(AudioClip newClip, float targetVolume, bool loop)
    {
        // FadeOut
        float startVol = _musicSource.volume;                                        // 当前音乐淡出前的音量。
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            _musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeDuration);
            yield return null;
        }
        _musicSource.Stop();

        // FadeIn
        _musicSource.clip = newClip;
        _musicSource.loop = loop;
        _musicSource.Play();
        for (float t = 0; t < fadeDuration; t += Time.deltaTime)
        {
            _musicSource.volume = Mathf.Lerp(0f, targetVolume, t / fadeDuration);
            yield return null;
        }
        _musicSource.volume = targetVolume;
    }

    private IEnumerator FadeMusicOut()
    {
        float startVol = _musicSource.volume;
        for (float t = 0f; t < fadeDuration; t += Time.deltaTime)
        {
            _musicSource.volume = Mathf.Lerp(startVol, 0f, t / fadeDuration);
            yield return null;
        }
        _musicSource.Stop();
        _musicSource.volume = 0f;
    }
    #endregion

    #region 音效请求调度
    private Camera _mainCamera;                                                      // 缓存的主相机（失效时自动重新查找）。

    /// <summary>
    /// 缓存的主相机：业务脚本经本服务取得，不再直接查询 Camera.main；
    /// 缓存为空（场景切换/尚未就绪）时自动重新查找。
    /// </summary>
    public Camera MainCamera
    {
        get
        {
            if (_mainCamera == null)
                _mainCamera = Camera.main;
            return _mainCamera;
        }
    }

    /// <summary>
    /// 以声源自身位置播放效果：距离由本服务用缓存的主相机内部计算，
    /// 业务脚本无需自行查询 Camera.main。
    /// </summary>
    /// <param name="source">携带音频片段及播放参数的音频源。</param>
    /// <param name="priority">请求优先级，数值越小优先级越高。</param>
    /// <param name="origin">播放期间需要持续跟踪位置的声源变换。</param>
    /// <returns>请求是否获得通道并开始播放。</returns>
    public bool PlayEffectAt(AudioSource source, uint priority, Transform origin)
    {
        Camera cam = MainCamera;
        float distance = cam != null && origin != null
            ? Vector3.Distance(origin.position, cam.transform.position)
            : 0f;
        return PlayEffect(source, priority, distance, origin);
    }

    /// <summary>
    /// 根据优先级、当前通道占用情况和声源距离决定是否接受音效请求。
    /// 紧急请求可以抢占最低优先级通道，其余请求仅在池容量和竞争规则允许时播放。
    /// </summary>
    /// <param name="source">携带音频片段及音量、音调等播放参数的音频源。</param>
    /// <param name="priority">请求优先级，数值越小优先级越高，0 表示紧急抢占。</param>
    /// <param name="distance">请求时声源与摄像机之间的距离，用于满载时的竞争判断。</param>
    /// <param name="origin">播放期间需要持续跟踪位置的声源变换。</param>
    /// <returns>请求是否获得通道并开始播放。</returns>
    public bool PlayEffect(AudioSource source, uint priority, float distance, Transform origin)
    {
        if (source == null || source.clip == null) return false;

        // 分支 1：紧急抢占（priority == 0）
        if (priority == 0 && _activeChannels.Count > 0)
        {
            AudioSource target = GetLowestPriorityChannel();                         // 被紧急请求抢占的最低优先级通道。
            target.Stop();
            int idx = _activeChannels.IndexOf(target);                               // 被抢占通道的活跃列表索引。
            Recycle(target, idx);
            AssignAndPlay(target, source, origin);
            return true;
        }

        // 分支 2：通道未满
        if (_activeChannels.Count < CHANNEL_DEFAULT)
        {
            AudioSource channel = GetIdleChannel();                                  // 分配给当前请求的空闲通道。
            if (channel == null) return false;
            AssignAndPlay(channel, source, origin);
            return true;
        }

        // 分支 3+：通道已满
        if (_activeChannels.Count >= POOL_MAX) return false;

        int bestPriorityVal  = GetBestPriorityValue();                               // 活跃通道中的最高优先级数值。
        int worstPriorityVal = GetLowestPriorityChannel().priority;                  // 活跃通道中的最低优先级数值。

        // 分支 3a：比最高优先级还高 且 在绝对阈值（10）内
        if ((int)priority < bestPriorityVal && priority < 10)
        {
            AudioSource channel = GetIdleChannel();                                  // 分配给高优先级请求的扩展通道。
            if (channel == null) return false;
            AssignAndPlay(channel, source, origin);
            return true;
        }

        // 分支 3b：优先级介于最高与最低之间 且 距离比最远的近
        if ((int)priority > bestPriorityVal && (int)priority < worstPriorityVal
            && distance < GetMaxActiveDistance())
        {
            AudioSource channel = GetIdleChannel();                                  // 分配给较近声源请求的扩展通道。
            if (channel == null) return false;
            AssignAndPlay(channel, source, origin);
            return true;
        }

        return false;
    }
    #endregion
}
