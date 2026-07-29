using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gold : MonoBehaviour
{
    public static Gold Instance { get; private set; }

    [SerializeField] private int gold = 0;    // 当前持有的黄金数量。

    #region 生命周期与回调
    /// <summary>
    /// 建立黄金系统单例；场景中存在重复实例时销毁后创建的对象。
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
    #endregion

    #region 公开接口
    /// <summary>
    /// 在数量为正时增加当前黄金；零或负数请求会被忽略。
    /// </summary>
    /// <param name="amount">需要增加的黄金数量。</param>
    public void GainGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        gold += amount;
    }

    /// <summary>
    /// 在数量合法且余额充足时扣除黄金。
    /// </summary>
    /// <param name="amount">准备消耗的黄金数量，不能为负数。</param>
    /// <returns>余额是否足够且扣除成功。</returns>
    public bool ConsumeGold(int amount)
    {
        if (amount < 0)
        {
            return false;
        }

        if (gold < amount)
        {
            return false;
        }

        gold -= amount;
        return true;
    }
    #endregion
}
