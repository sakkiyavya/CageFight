using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>完整观看激励广告后，增加玩家全局金币。</summary>
[DisallowMultipleComponent]
public sealed class RewardedGoldButton : MonoBehaviour, IPointerClickHandler
{
    private const int RewardAmount = 100;

    [SerializeField, Tooltip("抖音后台的激励视频广告位 ID")]
    private string adUnitId;

    public void OnPointerClick(PointerEventData eventData)
    {
        var info = UserGlobalInfo.Instance;
        if (info == null) return;
        var persistence = info.GetComponent<UserGlobalInfoPersistence>();
        if (persistence != null && !persistence.IsLoaded) return;
        DouyinRewardedVideoAd.Show(adUnitId, GrantReward);
    }

    private static void GrantReward()
    {
        var info = UserGlobalInfo.Instance;
        if (info == null) return;
        // 使用 long 相加，避免余额溢出变成负数。
        info.SetGoldBarCount((int)System.Math.Min(int.MaxValue, (long)info.GoldBarCount + RewardAmount));
    }
}
