using System;
using UnityEngine;

public class Coins : MonoBehaviour
{
    public static Coins Instance { get; private set; }

    [SerializeField]int coins = 0;
    [SerializeField]int coinPerSec = 0;

    public int CurrentCoins => coins;
    public int CurrentCoinPerSec => coinPerSec;
    public Action<int> OnGainCoins;
    public Action<int> OnConsumeCoins;

    float nextGainTime = -1;

    // 初始化金币单例。
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

    // 每秒结算一次金币增长。
    private void Update()
    {
        if(Time.time < nextGainTime) return;
        nextGainTime = Time.time + 1f;
        GainCoins(coinPerSec);
    }

    // 增加金币数量。
    public void GainCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        coins += amount;
        OnGainCoins?.Invoke(amount);
    }

    // 消耗指定金币。
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
}
