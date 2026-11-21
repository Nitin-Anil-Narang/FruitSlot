using System;
using UnityEngine;

/// <summary>
/// Persistent singleton tracking real-time player session statistics:
/// total spins, win count, hit rate %, total wagered, total won, net profit, biggest win, and bonus rounds.
/// Persists across scenes so players can track their session performance anytime.
/// </summary>
public class SessionStatsManager : MonoBehaviour
{
    public static SessionStatsManager Instance { get; private set; }

    public int TotalSpins { get; private set; }
    public int TotalWins { get; private set; }
    public int TotalWagered { get; private set; }
    public int TotalWon { get; private set; }
    public int BiggestWin { get; private set; }
    public int FreeSpinsWon { get; private set; }

    public int NetProfit => TotalWon - TotalWagered;
    public float HitRate => TotalSpins > 0 ? ((float)TotalWins / TotalSpins) * 100f : 0f;

    public event Action OnStatsUpdated;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Records a spin wager and increments session spin counter.
    /// </summary>
    public void RecordSpin(int betAmount)
    {
        TotalSpins++;
        TotalWagered += betAmount;
        OnStatsUpdated?.Invoke();
    }

    /// <summary>
    /// Records a winning spin outcome and evaluates biggest win.
    /// </summary>
    public void RecordWin(int winAmount, bool isFreeSpinBonus = false)
    {
        if (winAmount > 0)
        {
            TotalWins++;
            TotalWon += winAmount;

            if (winAmount > BiggestWin)
            {
                BiggestWin = winAmount;
            }
        }

        if (isFreeSpinBonus)
        {
            FreeSpinsWon += 5;
        }

        OnStatsUpdated?.Invoke();
    }

    /// <summary>
    /// Resets all session metrics back to zero.
    /// </summary>
    public void ResetStats()
    {
        TotalSpins = 0;
        TotalWins = 0;
        TotalWagered = 0;
        TotalWon = 0;
        BiggestWin = 0;
        FreeSpinsWon = 0;
        OnStatsUpdated?.Invoke();
    }

    /// <summary>
    /// Formats formatted rich text for UI display.
    /// </summary>
    public string GetFormattedSummary()
    {
        string profitColor = NetProfit >= 0 ? "#40FF80" : "#FF5050";
        string profitSign = NetProfit >= 0 ? "+" : "";

        return "<b>SESSION PERFORMANCE DASHBOARD</b>\n\n" +
               $"• Total Spins Played:    <b>{TotalSpins:N0}</b>\n" +
               $"• Winning Spins:         <b>{TotalWins:N0}</b>  <i>({HitRate:F1}% Hit Rate)</i>\n" +
               $"• Total Chips Wagered:   <b>${TotalWagered:N0}</b>\n" +
               $"• Total Chips Won:       <b>${TotalWon:N0}</b>\n" +
               $"• Net Profit / Loss:     <color={profitColor}><b>{profitSign}${NetProfit:N0}</b></color>\n" +
               $"• Biggest Single Win:    <color=#FFD700><b>${BiggestWin:N0}</b></color>\n" +
               $"• Free Spins Awarded:    <b>{FreeSpinsWon:N0} rounds</b>\n\n" +
               "<i>Stats update live in real-time as you spin!</i>";
    }
}
