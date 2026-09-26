using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>完整观看激励广告后，增加玩家全局钻石。</summary>
[DisallowMultipleComponent]
public sealed class RewardedDiamondButton : MonoBehaviour, IPointerClickHandler
{
    private const int RewardAmount = 10;

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
        info.SetDiamondCount((int)System.Math.Min(int.MaxValue, (long)info.DiamondCount + RewardAmount));
    }
}
