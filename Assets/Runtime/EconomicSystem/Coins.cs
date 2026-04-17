using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Coins : MonoBehaviour
{
    public static Coins Instance { get; private set; }

    [SerializeField] private int coins = 0;

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

    public void GainCoins(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        coins += amount;
    }

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
        return true;
    }
}
