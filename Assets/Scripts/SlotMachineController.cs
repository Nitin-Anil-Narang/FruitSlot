using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Main game coordinator: manages reels, player betting, RNG outcome generation,
/// win evaluation, payout simulation, and free spins bonus mode.
/// </summary>
public class SlotMachineController : MonoBehaviour
{
    public enum GameState { Idle, Spinning, Evaluating, Payout }

    [Header("Reels Setup")]
    [SerializeField] private Reel[] reels = new Reel[3];
    [SerializeField] private SymbolData[] symbols;

    [Header("Betting Configuration")]
    [SerializeField] private int[] betTiers = new int[] { 10, 25, 50, 100, 250, 500 };
    private int currentBetIndex = 0;

    [Header("UI Displays")]
    [SerializeField] private TextMeshProUGUI balanceText;
    [SerializeField] private TextMeshProUGUI betText;
    [SerializeField] private TextMeshProUGUI winText;
    [SerializeField] private TextMeshProUGUI statusBannerText;

    [Header("Control Buttons")]
    [SerializeField] private Button spinButton;
    [SerializeField] private Button betPlusButton;
    [SerializeField] private Button betMinusButton;
    [SerializeField] private Button maxBetButton;
    [SerializeField] private Button backToMenuButton;

    [Header("Physical Machine Lever")]
    [SerializeField] private Button leverButton;
    [SerializeField] private Image leverImage;
    [SerializeField] private Sprite leverUpSprite;
    [SerializeField] private Sprite leverDownSprite;
    [SerializeField] private Vector2 leverUpPosition = new Vector2(285, -55);
    [SerializeField] private Vector2 leverDownPosition = new Vector2(285, -160);

    [Header("Rules Modal")]
    [SerializeField] private GameObject rulesModal;
    [SerializeField] private Button rulesButton;
    [SerializeField] private Button rulesCloseButton;

    [Header("Win & Bonus Popups")]
    [SerializeField] private GameObject winPopupModal;
    [SerializeField] private TextMeshProUGUI winPopupAmountText;
    [SerializeField] private TextMeshProUGUI winPopupTitleText;
    [SerializeField] private Button winPopupCloseButton;

    [Header("Low Funds Dialog")]
    [SerializeField] private GameObject lowFundsModal;
    [SerializeField] private Button addFundsButton;
    [SerializeField] private Button lowFundsCloseButton;

    // Game Runtime State
    public GameState CurrentState { get; private set; } = GameState.Idle;
    private int reelsStoppedCount = 0;
    private int currentBetAmount => betTiers[currentBetIndex];

    // Bonus State
    private int freeSpinsRemaining = 0;
    private const int FreeSpinsCount = 5;
    private const int FreeSpinsMultiplier = 2;

    private void Start()
    {
        // 1. Ensure PlayerWallet exists
        if (PlayerWallet.Instance == null)
        {
            GameObject walletObj = new GameObject("PlayerWallet");
            walletObj.AddComponent<PlayerWallet>();
        }

        PlayerWallet.Instance.OnBalanceChanged += UpdateBalanceDisplay;

        // 2. Initialize Reels with symbol catalog
        for (int i = 0; i < reels.Length; i++)
        {
            if (reels[i] != null)
            {
                reels[i].Initialize(symbols, symbols.Length > 0 ? symbols[0].sprite : null);
                reels[i].OnReelStopped += HandleReelStopped;
            }
        }

        // 3. Setup Button Listeners
        if (spinButton != null) spinButton.onClick.AddListener(OnSpinClicked);
        if (leverButton != null) leverButton.onClick.AddListener(OnLeverClicked);
        if (betPlusButton != null) betPlusButton.onClick.AddListener(OnBetPlusClicked);
        if (betMinusButton != null) betMinusButton.onClick.AddListener(OnBetMinusClicked);
        if (maxBetButton != null) maxBetButton.onClick.AddListener(OnMaxBetClicked);
        if (backToMenuButton != null) backToMenuButton.onClick.AddListener(OnBackToMenuClicked);

        if (winPopupCloseButton != null && winPopupModal != null)
            winPopupCloseButton.onClick.AddListener(() => winPopupModal.SetActive(false));

        if (rulesButton != null && rulesModal != null)
            rulesButton.onClick.AddListener(() => rulesModal.SetActive(true));

        if (rulesCloseButton != null && rulesModal != null)
            rulesCloseButton.onClick.AddListener(() => rulesModal.SetActive(false));

        if (rulesModal != null)
            rulesModal.SetActive(false);

        if (lowFundsCloseButton != null && lowFundsModal != null)
            lowFundsCloseButton.onClick.AddListener(() => lowFundsModal.SetActive(false));

        if (addFundsButton != null)
            addFundsButton.onClick.AddListener(OnQuickAddFundsClicked);

        // 4. Initial Displays
        UpdateBalanceDisplay(PlayerWallet.Instance.CurrentBalance);
        UpdateBetDisplay();
        UpdateWinDisplay(0);

        if (statusBannerText != null)
            statusBannerText.text = "PULL LEVER TO SPIN";

        if (winPopupModal != null) winPopupModal.SetActive(false);
        if (lowFundsModal != null) lowFundsModal.SetActive(false);
    }

    private void OnDestroy()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnBalanceChanged -= UpdateBalanceDisplay;

        for (int i = 0; i < reels.Length; i++)
        {
            if (reels[i] != null)
                reels[i].OnReelStopped -= HandleReelStopped;
        }
    }

    #region Betting Controls

    private void OnBetPlusClicked()
    {
        if (CurrentState != GameState.Idle) return;
        if (currentBetIndex < betTiers.Length - 1)
        {
            currentBetIndex++;
            UpdateBetDisplay();
        }
    }

    private void OnBetMinusClicked()
    {
        if (CurrentState != GameState.Idle) return;
        if (currentBetIndex > 0)
        {
            currentBetIndex--;
            UpdateBetDisplay();
        }
    }

    private void OnMaxBetClicked()
    {
        if (CurrentState != GameState.Idle) return;
        currentBetIndex = betTiers.Length - 1;
        UpdateBetDisplay();
    }

    private void UpdateBetDisplay()
    {
        if (betText != null)
            betText.text = $"${currentBetAmount}";
    }

    private void UpdateBalanceDisplay(int newBalance)
    {
        if (balanceText != null)
            balanceText.text = $"${newBalance:N0}";
    }

    private void UpdateWinDisplay(int amount)
    {
        if (winText != null)
            winText.text = amount > 0 ? $"${amount:N0}" : "$0";
    }

    #endregion

    #region Spin & RNG Logic

    private void OnLeverClicked()
    {
        if (CurrentState != GameState.Idle) return;
        StartCoroutine(PullLeverAndSpinRoutine());
    }

    private IEnumerator PullLeverAndSpinRoutine()
    {
        yield return StartCoroutine(AnimateLeverPullRoutine());
        TriggerSpinAction();
    }

    private IEnumerator AnimateLeverPullRoutine()
    {
        if (leverImage != null && leverDownSprite != null)
        {
            leverImage.sprite = leverDownSprite;
            leverImage.rectTransform.sizeDelta = new Vector2(94, 61);
            leverImage.rectTransform.anchoredPosition = leverDownPosition;
        }

        yield return new WaitForSeconds(0.2f);

        if (leverImage != null && leverUpSprite != null)
        {
            leverImage.sprite = leverUpSprite;
            leverImage.rectTransform.sizeDelta = new Vector2(94, 272);
            leverImage.rectTransform.anchoredPosition = leverUpPosition;
        }
    }

    public void OnSpinClicked()
    {
        if (CurrentState != GameState.Idle) return;
        StartCoroutine(AnimateLeverPullRoutine());
        TriggerSpinAction();
    }

    private void TriggerSpinAction()
    {
        if (CurrentState != GameState.Idle) return;

        bool isFreeSpin = freeSpinsRemaining > 0;

        // Check wallet funds if it's a paid spin
        if (!isFreeSpin)
        {
            if (!PlayerWallet.Instance.CanAfford(currentBetAmount))
            {
                if (lowFundsModal != null)
                {
                    lowFundsModal.SetActive(true);
                }
                else
                {
                    PlayerWallet.Instance.AddMoney(500); // Friendly fallback
                }
                return;
            }

            PlayerWallet.Instance.DeductBet(currentBetAmount);
        }
        else
        {
            freeSpinsRemaining--;
        }

        StartCoroutine(ExecuteSpinRoutine(isFreeSpin));
    }

    private IEnumerator ExecuteSpinRoutine(bool isFreeSpin)
    {
        CurrentState = GameState.Spinning;
        reelsStoppedCount = 0;
        SetControlsInteractable(false);
        UpdateWinDisplay(0);

        if (statusBannerText != null)
        {
            statusBannerText.text = isFreeSpin
                ? $"FREE SPIN ({freeSpinsRemaining} LEFT) - 2X PAYOUT!"
                : "GOOD LUCK!";
        }

        // --- RNG OUTCOME CALCULATION (Pre-determined upfront) ---
        SymbolData[] targetSymbols = DetermineRNGOutcome();

        // Start staggered reel animations (suspense timing)
        float baseDuration = 1.2f;
        float staggerDelay = 0.35f;

        for (int i = 0; i < reels.Length; i++)
        {
            float reelDuration = baseDuration + (i * staggerDelay);
            reels[i].Spin(targetSymbols[i], reelDuration);
        }

        // Wait until all reels stop
        while (reelsStoppedCount < reels.Length)
        {
            yield return null;
        }

        // Evaluate win condition
        yield return StartCoroutine(EvaluateOutcomeRoutine(targetSymbols, isFreeSpin));
    }

    /// <summary>
    /// Weighted RNG selection: determines outcome upfront before reels visually stop.
    /// Simulates authentic slot mathematics with fair probability weighting.
    /// </summary>
    private SymbolData[] DetermineRNGOutcome()
    {
        SymbolData[] targets = new SymbolData[3];

        // Overall win chance (~30% hit frequency for engaging game feel)
        float winChance = 0.32f;
        bool isWin = UnityEngine.Random.value < winChance;

        if (isWin)
        {
            // Pick 1 symbol using weights, and set all 3 reels to match!
            SymbolData winSymbol = PickWeightedRandomSymbol();
            targets[0] = winSymbol;
            targets[1] = winSymbol;
            targets[2] = winSymbol;
        }
        else
        {
            // Pick independent random symbols, ensuring at least one differs
            targets[0] = PickWeightedRandomSymbol();
            targets[1] = PickWeightedRandomSymbol();
            targets[2] = PickWeightedRandomSymbol();

            // Guard against accidental 3-of-a-kind on a losing roll
            if (targets[0].id == targets[1].id && targets[1].id == targets[2].id)
            {
                // Force last reel to a different symbol
                foreach (var sym in symbols)
                {
                    if (sym.id != targets[0].id)
                    {
                        targets[2] = sym;
                        break;
                    }
                }
            }
        }

        return targets;
    }

    private SymbolData PickWeightedRandomSymbol()
    {
        if (symbols == null || symbols.Length == 0) return null;

        int totalWeight = 0;
        for (int i = 0; i < symbols.Length; i++)
            totalWeight += symbols[i].weight;

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int runningSum = 0;

        for (int i = 0; i < symbols.Length; i++)
        {
            runningSum += symbols[i].weight;
            if (roll < runningSum)
                return symbols[i];
        }

        return symbols[0];
    }

    private void HandleReelStopped(Reel reel)
    {
        reelsStoppedCount++;
    }

    #endregion

    #region Win Evaluation & Payout

    private IEnumerator EvaluateOutcomeRoutine(SymbolData[] finalSymbols, bool isFreeSpin)
    {
        CurrentState = GameState.Evaluating;

        // Check if all 3 symbols match
        bool isMatch = (finalSymbols[0].id == finalSymbols[1].id && finalSymbols[1].id == finalSymbols[2].id);

        if (isMatch)
        {
            CurrentState = GameState.Payout;
            SymbolData match = finalSymbols[0];

            int multiplier = match.payoutMultiplier;
            if (isFreeSpin) multiplier *= FreeSpinsMultiplier;

            int totalWin = currentBetAmount * multiplier;
            PlayerWallet.Instance.AddWin(totalWin);

            // Animate rolling win count
            yield return StartCoroutine(CountUpWinRoutine(totalWin));

            // Check for Bonus Feature trigger
            if (match.isBonus)
            {
                freeSpinsRemaining += FreeSpinsCount;
                if (statusBannerText != null)
                    statusBannerText.text = $"BONUS! {FreeSpinsCount} FREE SPINS AWARDED!";
            }
            else
            {
                if (statusBannerText != null)
                    statusBannerText.text = multiplier >= 50 ? "JACKPOT WINNER!" : "BIG WIN!";
            }

            // Show celebratory popup for big wins
            if (multiplier >= 25 && winPopupModal != null)
            {
                if (winPopupTitleText != null)
                    winPopupTitleText.text = multiplier >= 50 ? "JACKPOT WIN!" : "BIG WIN!";
                if (winPopupAmountText != null)
                    winPopupAmountText.text = $"+${totalWin:N0}";

                winPopupModal.SetActive(true);
            }
        }
        else
        {
            if (statusBannerText != null)
            {
                statusBannerText.text = freeSpinsRemaining > 0
                    ? $"FREE SPINS ACTIVE ({freeSpinsRemaining} LEFT)"
                    : "TRY AGAIN!";
            }
        }

        yield return new WaitForSeconds(0.4f);

        CurrentState = GameState.Idle;
        SetControlsInteractable(true);

        // Auto-spin next free spin if bonus active
        if (freeSpinsRemaining > 0)
        {
            yield return new WaitForSeconds(0.8f);
            OnSpinClicked();
        }
    }

    private IEnumerator CountUpWinRoutine(int targetAmount)
    {
        float duration = 0.8f;
        float elapsed = 0f;
        int current = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            current = (int)Mathf.Lerp(0, targetAmount, elapsed / duration);
            UpdateWinDisplay(current);
            yield return null;
        }

        UpdateWinDisplay(targetAmount);
    }

    private void SetControlsInteractable(bool interactable)
    {
        if (spinButton != null) spinButton.interactable = interactable;
        if (leverButton != null) leverButton.interactable = interactable;
        if (betPlusButton != null) betPlusButton.interactable = interactable;
        if (betMinusButton != null) betMinusButton.interactable = interactable;
        if (maxBetButton != null) maxBetButton.interactable = interactable;
    }

    #endregion

    #region Navigation & Fallbacks

    private void OnBackToMenuClicked()
    {
        SceneManager.LoadScene("StartScene");
    }

    private void OnQuickAddFundsClicked()
    {
        PlayerWallet.Instance.AddMoney(500);
        if (lowFundsModal != null)
            lowFundsModal.SetActive(false);
    }

    #endregion
}
