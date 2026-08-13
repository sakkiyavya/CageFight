using System.Collections.Generic;
using UnityEngine;

public class BuildingUpgradeButton : MonoBehaviour
{
    static readonly List<SentryTower> towers =
        new List<SentryTower>();

    public static bool Active { get; private set; }

    // 升级按钮的 OnClick 调用
    public void ToggleUpgrade()
    {
        Active = !Active;
        RefreshAll();
    }

    public void CloseUpgrade()
    {
        Active = false;
        RefreshAll();
    }

    public static void Register(SentryTower tower)
    {
        if (tower && !towers.Contains(tower))
            towers.Add(tower);
    }

    public static void Unregister(SentryTower tower)
    {
        towers.Remove(tower);
    }

    void OnEnable()
    {
        if (!Coins.Instance) return;

        Coins.Instance.OnGainCoins += OnCoinsChanged;
        Coins.Instance.OnConsumeCoins += OnCoinsChanged;
    }

    void OnDisable()
    {
        if (!Coins.Instance) return;

        Coins.Instance.OnGainCoins -= OnCoinsChanged;
        Coins.Instance.OnConsumeCoins -= OnCoinsChanged;
    }

    void OnCoinsChanged(int amount)
    {
        if (Active)
            RefreshAll();
    }

    static void RefreshAll()
    {
        for (int i = towers.Count - 1; i >= 0; i--)
        {
            if (!towers[i])
            {
                towers.RemoveAt(i);
                continue;
            }

            towers[i].ShowUpgrade(Active);
        }
    }
}