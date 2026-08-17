using UnityEngine;

public class MinerRatScript : MonoBehaviour
{
    // 供动画事件调用。
    public void GainCoin()
    {
        if (Coins.Instance == null)
        {
            Debug.LogWarning(
                "场景中没有找到 Coins 组件。",
                this
            );

            return;
        }

        Coins.Instance.GainCoins(1);
    }
}