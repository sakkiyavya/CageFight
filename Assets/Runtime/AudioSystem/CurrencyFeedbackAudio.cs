using UnityEngine;

/// <summary>
/// 货币反馈音效（挂在与 UserGlobalInfo 同物体的常驻对象上）：
/// - 金条增长（任意来源）→ 播放 "Gold bar UI AD"；
/// - 钻石增长（任意来源）→ 播放 "Diamod UI AD"；
/// - 内容不满足要求（如升级货币不足）→ 由业务调用 PlayWrong() 播放 "Wrong UI AD"。
/// 金条/钻石增长经 UserGlobalInfo.Changed 自动侦测，无需每个发放点各自接音效；
/// 读档/首次订阅只记录基准值，不误播。
/// 全部经 AudioManager.PlayEffectClip 统一播放入口（规范禁止运行时 AddComponent）。
/// </summary>
[DisallowMultipleComponent]
public sealed class CurrencyFeedbackAudio : MonoBehaviour
{
    private const string GoldBarSoundKey = "Gold bar UI AD";
    private const string DiamondSoundKey = "Diamod UI AD";
    private const string WrongSoundKey = "Wrong UI AD";

    private static CurrencyFeedbackAudio _instance;

    private UserGlobalInfo _info;
    private int _lastGold;
    private int _lastDiamond;
    private bool _subscribed;

    public static CurrencyFeedbackAudio Instance => _instance;

    private void Awake()
    {
        _instance = this;
        SubscribeIfReady();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        // 懒订阅重试：UserGlobalInfo 后于本组件就绪时补上（每帧判空，成本可忽略）。
        SubscribeIfReady();

        // 音频未就绪时每帧重试预载：资源管理器晚于本组件初始化时，首个增长事件也能出声。
        if (ResourceManager.Instance != null)
        {
            RetryPreload(GoldBarSoundKey);
            RetryPreload(DiamondSoundKey);
            RetryPreload(WrongSoundKey);
        }
    }

    private static void RetryPreload(string key)
    {
        if (ResourceManager.Instance.GetAudio(key) == null)
            ResourceManager.Instance.LoadExtraResourceAsync<AudioClip>(key);
    }

    private void SubscribeIfReady()
    {
        if (_subscribed)
            return;

        UserGlobalInfo info = UserGlobalInfo.Instance;
        if (info == null)
            return;

        _info = info;
        _lastGold = info.GoldBarCount;       // 基准值：首次订阅（含读档）不播音效。
        _lastDiamond = info.DiamondCount;
        info.Changed += OnChanged;
        _subscribed = true;
    }

    private void OnChanged()
    {
        if (_info == null)
            return;

        int gold = _info.GoldBarCount;
        int diamond = _info.DiamondCount;

        if (gold > _lastGold)
            PlaySound(GoldBarSoundKey);
        if (diamond > _lastDiamond)
            PlaySound(DiamondSoundKey);

        _lastGold = gold;
        _lastDiamond = diamond;
    }

    /// <summary>内容不满足要求时的错误音效（如升级货币不足）。</summary>
    public static void PlayWrong()
    {
        if (_instance != null)
            _instance.PlaySound(WrongSoundKey);
    }

    private void PlaySound(string key)
    {
        if (ResourceManager.Instance == null || AudioManager.Instance == null)
        {
            Debug.LogWarning($"[CurrencyFeedbackAudio] ResourceManager/AudioManager 未就绪，跳过播放：{key}", this);
            return;
        }

        AudioClip clip = ResourceManager.Instance.GetAudio(key);
        if (clip == null)
        {
            // 尚未加载成功：立即补一次预载，下一帧重试循环会持续跟进；本次跳过。
            Debug.LogWarning($"[CurrencyFeedbackAudio] 音频未加载：{key}，已重新发起预载。", this);
            ResourceManager.Instance.LoadExtraResourceAsync<AudioClip>(key);
            return;
        }

        Debug.Log($"[CurrencyFeedbackAudio] 播放：{key}", this);
        AudioManager.Instance.PlayEffectClip(clip, 32, transform);
    }
}
