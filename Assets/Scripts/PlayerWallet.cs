using System;
using UnityEngine;

/// <summary>
/// Manages player currency and persists across scenes.
/// </summary>
public class PlayerWallet : MonoBehaviour
{
    public static PlayerWallet Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private int defaultStartingBalance = 500;

    private int currentBalance;

    public int CurrentBalance => currentBalance;

    // Event fired whenever balance changes so UI elements update instantly
    public event Action<int> OnBalanceChanged;

    private void Awake()
    {
        // Keep this instance alive across scene loads (e.g. StartScene -> GameScene)
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        currentBalance = defaultStartingBalance;
    }

    /// <summary>
    /// Adds funds from the start scene or in-game top-up.
    /// </summary>
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        currentBalance += amount;
        OnBalanceChanged?.Invoke(currentBalance);
    }

    /// <summary>
    /// Checks if player has sufficient funds for a bet.
    /// </summary>
    public bool CanAfford(int amount)
    {
        return currentBalance >= amount;
    }

    /// <summary>
    /// Deducts bet amount from balance. Returns false if insufficient funds.
    /// </summary>
    public bool DeductBet(int amount)
    {
        if (amount <= 0 || currentBalance < amount)
        {
            return false;
        }

        currentBalance -= amount;
        OnBalanceChanged?.Invoke(currentBalance);
        return true;
    }

    /// <summary>
    /// Awards winning payout to player balance.
    /// </summary>
    public void AddWin(int amount)
    {
        if (amount <= 0) return;

        currentBalance += amount;
        OnBalanceChanged?.Invoke(currentBalance);
    }

    /// <summary>
    /// Resets balance back to default.
    /// </summary>
    public void ResetBalance()
    {
        currentBalance = defaultStartingBalance;
        OnBalanceChanged?.Invoke(currentBalance);
    }
}
