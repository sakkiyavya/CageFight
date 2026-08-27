using System.Collections.Generic;
using UnityEngine;

public class BuildingUpgradeButton : MonoBehaviour
{
    static readonly List<BuildUP> buildings =
        new List<BuildUP>();

    public static bool Active { get; private set; }

    // 升级按钮调用：开启或关闭升级模式
    public void ToggleUpgrade()
    {
        // 与拆除模式互斥：进入升级模式时先关闭拆除模式。
        if (!Active)
            BuildingRemoveButton.CloseAll();

        Active = !Active;
        RefreshAll();
    }

    // 强制关闭升级模式
    public void CloseUpgrade()
    {
        CloseAll();
    }

    public static void CloseAll()
    {
        Active = false;
        RefreshAll();
    }

    public static void Register(BuildUP building)
    {
        if (building && !buildings.Contains(building))
            buildings.Add(building);
    }

    public static void Unregister(BuildUP building)
    {
        buildings.Remove(building);
    }

    static void RefreshAll()
    {
        for (int i = buildings.Count - 1; i >= 0; i--)
        {
            if (!buildings[i])
            {
                buildings.RemoveAt(i);
                continue;
            }

            buildings[i].ShowUpgrade(Active);
        }
    }

    void OnEnable()
    {
        if (!Coins.Instance)
            return;

        Coins.Instance.OnGainCoins += OnCoinsChanged;
        Coins.Instance.OnConsumeCoins += OnCoinsChanged;
    }

    void OnDisable()
    {
        if (!Coins.Instance)
            return;

        Coins.Instance.OnGainCoins -= OnCoinsChanged;
        Coins.Instance.OnConsumeCoins -= OnCoinsChanged;
    }

    void OnCoinsChanged(int amount)
    {
        if (Active)
            RefreshAll();
    }
}
