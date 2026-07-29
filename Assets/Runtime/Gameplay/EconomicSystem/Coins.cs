using System;
using UnityEngine;

public class Coins : MonoBehaviour
{
    public static Coins Instance { get; private set; }

    [SerializeField]int coins = 0;                 // 当前持有的金币数量。
    [SerializeField]int coinPerSec = 0;            // 自动结算时每秒增加的金币数量。

    public int CurrentCoins => coins;              // 对外只读的当前金币总量。
    public int CurrentCoinPerSec => coinPerSec;    // 对外只读的每秒金币产量。
    public Action<int> OnGainCoins;                // 金币增加成功后发布增加量的事件。
    public Action<int> OnConsumeCoins;             // 金币扣除成功后发布扣除量的事件。

    float nextGainTime = -1;                       // 下一次自动结算金币的游戏时间。

    #region 生命周期与回调
    /// <summary>
    /// 建立金币系统单例；场景中存在重复实例时销毁后创建的对象。
    /// </summary>
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 到达下一次结算时间时增加每秒产量，并将下一次结算安排到一秒后。
    /// </summary>
    private void Update()
    {
        if(Time.time < nextGainTime) return;
        nextGainTime = Time.time + 1f;
        GainCoins(coinPerSec);
    }
    #endregion

    #region 公开接口
    /// <summary>
    /// 在数量为正时增加金币，并向订阅者发布本次增加量。
    /// </summary>
    /// <param name="amount">需要增加的金币数量。</param>
    public void GainCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        coins += amount;
        OnGainCoins?.Invoke(amount);
    }

    /// <summary>
    /// 在数量合法且余额充足时扣除金币，并向订阅者发布本次扣除量。
    /// </summary>
    /// <param name="amount">准备消耗的金币数量，不能为负数。</param>
    /// <returns>余额是否足够且扣除成功。</returns>
    public bool ConsumeCoins(int amount)
    {
        if (amount < 0)
        {
            return false;
        }

        if (coins < amount)
        {
            return false;
        }

        coins -= amount;
        OnConsumeCoins?.Invoke(amount);
        return true;
    }
    #endregion
}
