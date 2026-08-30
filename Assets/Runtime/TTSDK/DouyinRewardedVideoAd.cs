using System;
using UnityEngine;

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
using TTSDK;
#endif

/// <summary>
/// 抖音小游戏激励视频广告封装。
///
/// 调用 <see cref="Show(string, Action)"/> 传入广告位 ID 和奖励回调。
/// 只有广告被完整观看并关闭时，才会执行奖励回调；加载失败、展示失败或提前关闭均不会执行。
/// </summary>
[DisallowMultipleComponent]
public sealed class DouyinRewardedVideoAd : MonoBehaviour
{
    private const string BootstrapObjectName = "DouyinRewardedVideoAd";

    private static DouyinRewardedVideoAd _instance;

    private bool _requestInProgress;
    private Action _onCompleted;

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
    private TTRewardedVideoAd _rewardedVideoAd;
#endif

    /// <summary>
    /// 展示激励视频广告。
    ///
    /// 此方法应由真实的用户点击触发。只有用户完整观看广告后关闭时，<paramref name="onCompleted"/>
    /// 才会执行；不要在该回调外提前发放奖励。
    /// </summary>
    /// <param name="adUnitId">抖音开发者后台创建的激励视频广告位 ID。</param>
    /// <param name="onCompleted">广告完整观看后的奖励发放逻辑。</param>
    public static void Show(string adUnitId, Action onCompleted)
    {
        if (string.IsNullOrWhiteSpace(adUnitId))
        {
            Debug.LogWarning("[DouyinRewardedVideoAd] 广告位 ID 不能为空，未展示广告。");
            return;
        }

        if (onCompleted == null)
        {
            Debug.LogWarning("[DouyinRewardedVideoAd] 奖励回调不能为空，未展示广告。");
            return;
        }

        GetOrCreateInstance().ShowInternal(adUnitId, onCompleted);
    }

    private static DouyinRewardedVideoAd GetOrCreateInstance()
    {
        if (_instance != null)
            return _instance;

        GameObject gameObject = new GameObject(BootstrapObjectName);
        DontDestroyOnLoad(gameObject);
        return gameObject.AddComponent<DouyinRewardedVideoAd>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        ReleaseAd();
#endif

        if (_instance == this)
            _instance = null;
    }

    private void ShowInternal(string adUnitId, Action onCompleted)
    {
        if (_requestInProgress)
        {
            Debug.LogWarning("[DouyinRewardedVideoAd] 已有广告请求进行中，忽略本次展示请求。", this);
            return;
        }

        // 复用已有的启动脚本，避免多个能力重复调用 TT.InitSDK。
        if (DouyinSidebarRevisit.Instance == null ||
            !DouyinSidebarRevisit.Instance.IsSdkInitialized)
        {
            Debug.LogWarning(
                "[DouyinRewardedVideoAd] TTSDK 尚未初始化完成，未展示广告；请在初始化完成后由用户再次点击。",
                this);
            return;
        }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
        _requestInProgress = true;
        _onCompleted = onCompleted;

        try
        {
            // 激励视频广告全局只允许一个实例；每次展示后都释放并在下次重新创建。
            _rewardedVideoAd = TT.CreateRewardedVideoAd(new CreateRewardedVideoAdParam
            {
                AdUnitId = adUnitId,
                Multiton = false,
                ProgressTip = false,
            });

            _rewardedVideoAd.OnLoad += HandleAdLoaded;
            _rewardedVideoAd.OnError += HandleAdError;
            _rewardedVideoAd.OnClose += HandleAdClosed;
            _rewardedVideoAd.Load();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DouyinRewardedVideoAd] 创建广告失败：{exception.Message}", this);
            FinishRequestWithoutReward();
        }
#else
        Debug.LogWarning(
            "[DouyinRewardedVideoAd] Unity Editor 不调用 TTSDK；请在抖音真机环境测试广告。",
            this);
#endif
    }

#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_ANDROID || UNITY_IOS)
    private void HandleAdLoaded()
    {
        if (!_requestInProgress || _rewardedVideoAd == null)
            return;

        try
        {
            _rewardedVideoAd.Show();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[DouyinRewardedVideoAd] 展示广告失败：{exception.Message}", this);
            FinishRequestWithoutReward();
        }
    }

    private void HandleAdError(int errorCode, string errorMessage)
    {
        Debug.LogWarning(
            $"[DouyinRewardedVideoAd] 广告加载失败 ({errorCode})：{errorMessage}",
            this);
        FinishRequestWithoutReward();
    }

    private void HandleAdClosed(bool isEnded, int count)
    {
        Action completed = _onCompleted;
        FinishRequestWithoutReward();

        if (!isEnded)
        {
            Debug.Log("[DouyinRewardedVideoAd] 用户未完整观看广告，不执行奖励回调。", this);
            return;
        }

        try
        {
            completed?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
        }
    }

    private void FinishRequestWithoutReward()
    {
        _requestInProgress = false;
        _onCompleted = null;
        ReleaseAd();
    }

    private void ReleaseAd()
    {
        if (_rewardedVideoAd == null)
            return;

        _rewardedVideoAd.OnLoad -= HandleAdLoaded;
        _rewardedVideoAd.OnError -= HandleAdError;
        _rewardedVideoAd.OnClose -= HandleAdClosed;
        _rewardedVideoAd.Destroy();
        _rewardedVideoAd = null;
    }
#endif
}
