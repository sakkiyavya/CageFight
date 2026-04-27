using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Gold : MonoBehaviour
{
    public static Gold Instance { get; private set; }

    [SerializeField] private int gold = 0;

    // 初始化黄金单例。
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

    // 增加黄金数量。
    public void GainGold(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        gold += amount;
    }

    // 消耗指定黄金。
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
}
