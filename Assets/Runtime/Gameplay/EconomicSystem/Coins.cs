using System;
using System.Collections.Generic;
using UnityEngine;

public class Coins : MonoBehaviour
{
    public static Coins Instance { get; private set; }

    [SerializeField] int coins = 0;                 // 当前持有的金币数量。
    [SerializeField] int coinPerSec = 100;          // 初始阶段每秒增加的金币数量。

    [Header("时间阶梯产量（按局内时间递增）")]
    [SerializeField, Tooltip("各阶段开始时刻（秒）")]
    private float[] phaseTimes = new float[] { 60f, 120f, 180f };
    [SerializeField, Tooltip("对应阶段每秒产量（依次对应各阶段）")]
    private int[] phaseCoinPerSec = new int[] { 150, 400, 600 };

    public int CurrentCoins => coins;              // 对外只读的当前金币总量。

    /// <summary>
    /// 对外只读的每秒金币产量：按局内时间在时间阶梯中取当前阶段值。
    /// 0~60s 取初始值，60~120s 取第一阶段值，依次类推。
    /// </summary>
    public int CurrentCoinPerSec
    {
        get
        {
            int value = coinPerSec;
            for (int i = 0; i < phaseTimes.Length && i < phaseCoinPerSec.Length; i++)
            {
                if (Time.time >= phaseTimes[i])
                    value = phaseCoinPerSec[i];
            }
            return value;
        }
    }

    // 维护费登记表：键为登记来源（训练建筑/哨塔等组件实例），值为该来源每秒抵扣的金币量。
    private readonly Dictionary<object, int> _upkeepSources = new Dictionary<object, int>();
    private int _totalUpkeep;                      // 全部来源维护费每秒抵扣的总量。

    /// <summary>全部维护费每秒抵扣的金币总量。</summary>
    public int TotalUpkeep => _totalUpkeep;

    /// <summary>
    /// 每秒净产量：当前阶段毛产量减去全部维护费，最小为 0
    /// （维护费只抵扣收入流速，不欠费、不动已有余额）。
    /// </summary>
    public int NetCoinPerSec => Mathf.Max(0, CurrentCoinPerSec - _totalUpkeep);

    public Action<int> OnGainCoins;                // 金币增加成功后发布增加量的事件。
    public Action<int> OnConsumeCoins;             // 金币扣除成功后发布扣除量的事件。
    /// <summary>维护费登记变化（新增/修改/注销）后发布，供经济界面即时刷新每秒产量。</summary>
    public Action OnUpkeepChanged;

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
    /// 到达下一次结算时间时按净产量（阶段产量扣除全部维护费）增加金币，并将下一次结算安排到一秒后。
    /// </summary>
    private void Update()
    {
        if(Time.time < nextGainTime) return;
        nextGainTime = Time.time + 1f;
        GainCoins(NetCoinPerSec);
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

    /// <summary>
    /// 登记一个每秒维护费抵扣来源；同一来源重复登记时按新数量覆盖并修正总量。
    /// 数量非正时按注销处理。供训练建筑（兵种维护费）与哨塔（等级维护费）等调用。
    /// </summary>
    /// <param name="source">登记来源（通常为组件实例）。</param>
    /// <param name="amount">该来源每秒抵扣的金币量。</param>
    /// <returns>维护费总量是否发生变化。</returns>
    public bool RegisterUpkeep(object source, int amount)
    {
        if (source == null)
        {
            return false;
        }

        if (amount <= 0)
        {
            return UnregisterUpkeep(source);
        }

        if (_upkeepSources.TryGetValue(source, out int oldAmount))
        {
            if (oldAmount == amount)
            {
                return false;
            }

            _totalUpkeep += amount - oldAmount;
            _upkeepSources[source] = amount;
            OnUpkeepChanged?.Invoke();
            return true;
        }

        _upkeepSources.Add(source, amount);
        _totalUpkeep += amount;
        OnUpkeepChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 注销一个维护费来源（取消训练、更换兵种、建筑被摧毁等）；未登记时无事发生。
    /// </summary>
    /// <param name="source">登记来源。</param>
    /// <returns>是否确实注销了维护费。</returns>
    public bool UnregisterUpkeep(object source)
    {
        if (source == null || !_upkeepSources.TryGetValue(source, out int amount))
        {
            return false;
        }

        _upkeepSources.Remove(source);
        _totalUpkeep -= amount;
        OnUpkeepChanged?.Invoke();
        return true;
    }
    #endregion
}
