using UnityEngine;
using UnityEngine.UI;

public class EconomicUI : MonoBehaviour
{
    public Text coins;                                       // 显示当前金币总量的文本。
    public Text coinPerSec;                                  // 显示每秒金币产量的文本。

    #region 生命周期与回调
    /// <summary>
    /// 校验文本和金币系统引用，订阅金币增减事件，并立即刷新当前经济数据。
    /// </summary>
    private void OnEnable()
    {
        if (coins == null)
        {
            Debug.LogError("EconomicUI 的 coins Text 未赋值。", this);
        }

        if (coinPerSec == null)
        {
            Debug.LogError("EconomicUI 的 coinPerSec Text 未赋值。", this);
        }

        if (Coins.Instance == null)
        {
            Debug.LogError("Coins.Instance 为空，请确认场景中已挂载 Coins 单例。", this);
            return;
        }

        Coins.Instance.OnGainCoins += RefreshText;
        Coins.Instance.OnConsumeCoins += RefreshText;
        RefreshText(0);
    }

    /// <summary>
    /// 组件停用时解除金币事件订阅，避免重复回调或引用已停用界面。
    /// </summary>
    private void OnDisable()
    {
        if (Coins.Instance != null)
        {
            Coins.Instance.OnGainCoins -= RefreshText;
            Coins.Instance.OnConsumeCoins -= RefreshText;
        }
    }
    #endregion

    #region 内部辅助
    /// <summary>
    /// 从金币系统读取最新总量和每秒产量，并同步到两个文本控件。
    /// </summary>
    /// <param name="_">金币事件携带的变化量；界面直接读取最新状态，因此不使用该值。</param>
    private void RefreshText(int _)
    {
        if (coins == null || coinPerSec == null || Coins.Instance == null)
        {
            return;
        }

        coins.text = Coins.Instance.CurrentCoins.ToString();
        coinPerSec.text = Coins.Instance.CurrentCoinPerSec.ToString();
    }
    #endregion
}
