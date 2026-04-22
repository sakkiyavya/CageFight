using UnityEngine;
using UnityEngine.UI;

public class EconomicUI : MonoBehaviour
{
    public Text coins;
    public Text coinPerSec;

    // 启用时绑定金币事件。
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

    // 禁用时解绑金币事件。
    private void OnDisable()
    {
        if (Coins.Instance != null)
        {
            Coins.Instance.OnGainCoins -= RefreshText;
            Coins.Instance.OnConsumeCoins -= RefreshText;
        }
    }

    // 刷新经济文本。
    private void RefreshText(int _)
    {
        if (coins == null || coinPerSec == null || Coins.Instance == null)
        {
            return;
        }

        coins.text = Coins.Instance.CurrentCoins.ToString();
        coinPerSec.text = Coins.Instance.CurrentCoinPerSec.ToString();
    }
}
