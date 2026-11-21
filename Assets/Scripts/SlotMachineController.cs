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

    [Header("Session Statistics Modal")]
    [SerializeField] private GameObject statsModal;
    [SerializeField] private Button statsButton;
    [SerializeField] private Button statsCloseButton;
    [SerializeField] private Button statsResetButton;
    [SerializeField] private TextMeshProUGUI statsContentText;

    [Header("Win & Bonus Popups")]
    [SerializeField] private GameObject winPopupModal;
    [SerializeField] private TextMeshProUGUI winPopupAmountText;
    [SerializeField] private TextMeshProUGUI winPopupTitleText;
    [SerializeField] private Button winPopupCloseButton;

    [Header("Low Funds Dialog")]
    [SerializeField] private GameObject lowFundsModal;
    [SerializeField] private Button addFundsButton;
    [SerializeField] private Button lowFundsCloseButton;

    [Header("Audio Settings")]
    [SerializeField] private Button audioToggleBtn;
    [SerializeField] private TextMeshProUGUI audioToggleText;

    [Header("Developer Test / Cheat Controls")]
    [SerializeField] private GameObject devPanel;
    [SerializeField] private Button devToggleBtn;
    [SerializeField] private Button devCloseBtn;
    [SerializeField] private Button testWin777Btn;
    [SerializeField] private Button testWinBellBtn;
    [SerializeField] private Button testWinCherryBtn;
    [SerializeField, HideInInspector] private Button testWinPlumBtn;
    [SerializeField] private Button testWinBarBtn;
    [SerializeField] private Button testNearMissBtn;
    [SerializeField] private Button devAddFundsBtn;

    [Header("RNG & Mathematics Configuration")]
    [Tooltip("Probability of hitting a 3-of-a-kind win (default: 0.12 = 12% hit rate)")]
    [Range(0.01f, 0.40f)]
    [SerializeField] private float winProbability = 0.12f;

    [Tooltip("Probability of a suspenseful near-miss on losing rolls (Reel 1 & 2 match)")]
    [Range(0f, 0.60f)]
    [SerializeField] private float nearMissProbability = 0.30f;

    private SymbolData[] forcedNextOutcome = null;

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
        // 1. Ensure PlayerWallet, AudioManager, and SessionStatsManager exist
        if (PlayerWallet.Instance == null)
        {
            GameObject walletObj = new GameObject("PlayerWallet");
            walletObj.AddComponent<PlayerWallet>();
        }

        if (AudioManager.Instance == null)
        {
            GameObject audioObj = new GameObject("AudioManager");
            audioObj.AddComponent<AudioManager>();
        }

        if (SessionStatsManager.Instance == null)
        {
            GameObject statsObj = new GameObject("SessionStatsManager");
            statsObj.AddComponent<SessionStatsManager>();
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
        if (betPlusButton != null) betPlusButton.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnBetPlusClicked(); });
        if (betMinusButton != null) betMinusButton.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnBetMinusClicked(); });
        if (maxBetButton != null) maxBetButton.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnMaxBetClicked(); });
        if (backToMenuButton != null) backToMenuButton.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnBackToMenuClicked(); });

        if (winPopupCloseButton != null && winPopupModal != null)
            winPopupCloseButton.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); winPopupModal.SetActive(false); });

        if (rulesButton != null && rulesModal != null)
            rulesButton.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); rulesModal.SetActive(true); });

        if (rulesCloseButton != null && rulesModal != null)
            rulesCloseButton.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); rulesModal.SetActive(false); });

        if (rulesModal != null)
            rulesModal.SetActive(false);

        if (lowFundsCloseButton != null)
            lowFundsCloseButton.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnBackToMenuClicked(); });

        if (addFundsButton != null)
            addFundsButton.onClick.AddListener(() => { AudioManager.Instance?.PlayButtonClick(); OnQuickAddFundsClicked(); });

        if (audioToggleBtn != null)
            audioToggleBtn.onClick.AddListener(OnAudioToggleClicked);

        // 4. Developer Test Controls Listeners
        if (devToggleBtn != null) devToggleBtn.onClick.AddListener(ToggleDevPanel);
        if (devCloseBtn != null) devCloseBtn.onClick.AddListener(() => { if (devPanel != null) devPanel.SetActive(false); });
        if (testWin777Btn != null) testWin777Btn.onClick.AddListener(() => ForceSymbolWin(1));
        if (testWinBellBtn != null) testWinBellBtn.onClick.AddListener(() => ForceSymbolWin(2));
        Button cherryBtn = testWinCherryBtn != null ? testWinCherryBtn : testWinPlumBtn;
        if (cherryBtn != null) cherryBtn.onClick.AddListener(() => ForceSymbolWin(3));
        if (testWinBarBtn != null) testWinBarBtn.onClick.AddListener(() => ForceSymbolWin(4));
        if (testNearMissBtn != null) testNearMissBtn.onClick.AddListener(ForceNearMissOutcome);
        if (devAddFundsBtn != null) devAddFundsBtn.onClick.AddListener(() => PlayerWallet.Instance?.AddMoney(1000));

        if (devPanel != null) devPanel.SetActive(false);

        // 5. Session Statistics UI Setup
        SetupSessionStatsUI();

        // 6. Initial Displays
        UpdateBalanceDisplay(PlayerWallet.Instance.CurrentBalance);
        UpdateBetDisplay();
        UpdateWinDisplay(0);

        if (statusBannerText != null)
            statusBannerText.text = "PULL LEVER TO SPIN";

        if (winPopupModal != null) winPopupModal.SetActive(false);
        if (lowFundsModal != null) lowFundsModal.SetActive(false);
    }

    private void Update()
    {
        // Keyboard Shortcuts (WebGL & Desktop)
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            // Backquote / Tilde (~) or F1 toggles Dev Test Panel
            if (UnityEngine.InputSystem.Keyboard.current.backquoteKey.wasPressedThisFrame ||
                UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
            {
                ToggleDevPanel();
            }

            // Spacebar pulls lever to spin
            if (UnityEngine.InputSystem.Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                if (CurrentState == GameState.Idle)
                {
                    OnLeverClicked();
                }
            }

            // 'M' key toggles audio mute
            if (UnityEngine.InputSystem.Keyboard.current.mKey.wasPressedThisFrame)
            {
                OnAudioToggleClicked();
            }

            // 'S' key toggles Session Statistics Modal
            if (UnityEngine.InputSystem.Keyboard.current.sKey.wasPressedThisFrame)
            {
                ToggleStatsModal();
            }
        }
    }

    private void OnDestroy()
    {
        if (PlayerWallet.Instance != null)
            PlayerWallet.Instance.OnBalanceChanged -= UpdateBalanceDisplay;

        if (SessionStatsManager.Instance != null)
            SessionStatsManager.Instance.OnStatsUpdated -= UpdateStatsDisplay;

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

    public void OnAudioToggleClicked()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ToggleMute();
            UpdateAudioButtonDisplay();
        }
    }

    private void UpdateAudioButtonDisplay()
    {
        if (audioToggleText != null && AudioManager.Instance != null)
        {
            audioToggleText.text = AudioManager.Instance.IsMuted ? "SFX: OFF" : "SFX: ON";
        }
    }

    #endregion

    #region Session Statistics UI

    private void SetupSessionStatsUI()
    {
        // 1. Create Top Bar STATS Button if not assigned
        if (statsButton == null && rulesButton != null)
        {
            GameObject btnObj = Instantiate(rulesButton.gameObject, rulesButton.transform.parent);
            btnObj.name = "Btn_Stats";
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(540, 0);
            rect.sizeDelta = new Vector2(120, 50);

            TextMeshProUGUI tmp = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = "STATS";

            statsButton = btnObj.GetComponent<Button>();
        }

        // 2. Create Stats Modal by duplicating rulesModal if not assigned
        if (statsModal == null && rulesModal != null)
        {
            statsModal = Instantiate(rulesModal, rulesModal.transform.parent);
            statsModal.name = "SessionStatsModal";

            // Find Title and Content TextMeshProUGUI components
            TextMeshProUGUI[] tmps = statsModal.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                if (t.gameObject.name.Contains("Title"))
                {
                    t.text = "SESSION STATISTICS";
                }
                else if (t.gameObject.name.Contains("Content"))
                {
                    statsContentText = t;
                    t.fontSize = 21;
                    t.lineSpacing = 16;
                }
            }

            // Find Close button and create Reset Stats button alongside it
            Button[] btns = statsModal.GetComponentsInChildren<Button>(true);
            foreach (var b in btns)
            {
                if (b.gameObject.name.Contains("Close"))
                {
                    statsCloseButton = b;
                    RectTransform closeRect = b.GetComponent<RectTransform>();
                    closeRect.anchoredPosition = new Vector2(130, -220);
                    closeRect.sizeDelta = new Vector2(200, 55);

                    GameObject resetObj = Instantiate(b.gameObject, b.transform.parent);
                    resetObj.name = "Btn_StatsReset";
                    RectTransform resetRect = resetObj.GetComponent<RectTransform>();
                    resetRect.anchoredPosition = new Vector2(-130, -220);
                    resetRect.sizeDelta = new Vector2(200, 55);

                    Image resetImg = resetObj.GetComponent<Image>();
                    if (resetImg != null) resetImg.color = new Color(0.75f, 0.25f, 0.25f, 1f);

                    TextMeshProUGUI resetTmp = resetObj.GetComponentInChildren<TextMeshProUGUI>();
                    if (resetTmp != null) resetTmp.text = "RESET STATS";

                    statsResetButton = resetObj.GetComponent<Button>();
                    break;
                }
            }
        }

        if (statsButton != null)
            statsButton.onClick.AddListener(OpenStatsModal);

        if (statsCloseButton != null)
            statsCloseButton.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlayButtonClick();
                if (statsModal != null) statsModal.SetActive(false);
            });

        if (statsResetButton != null)
            statsResetButton.onClick.AddListener(OnResetStatsClicked);

        if (SessionStatsManager.Instance != null)
            SessionStatsManager.Instance.OnStatsUpdated += UpdateStatsDisplay;

        if (statsModal != null)
            statsModal.SetActive(false);
    }

    public void OpenStatsModal()
    {
        AudioManager.Instance?.PlayButtonClick();
        UpdateStatsDisplay();
        if (statsModal != null) statsModal.SetActive(true);
    }

    public void ToggleStatsModal()
    {
        if (statsModal != null)
        {
            if (statsModal.activeSelf)
            {
                statsModal.SetActive(false);
            }
            else
            {
                OpenStatsModal();
            }
        }
    }

    public void UpdateStatsDisplay()
    {
        if (statsContentText != null && SessionStatsManager.Instance != null)
        {
            statsContentText.text = SessionStatsManager.Instance.GetFormattedSummary();
        }
    }

    public void OnResetStatsClicked()
    {
        AudioManager.Instance?.PlayButtonClick();
        SessionStatsManager.Instance?.ResetStats();
        UpdateStatsDisplay();
    }

    #endregion

    #region Spin & RNG Logic

    private void OnLeverClicked()
    {
        if (CurrentState != GameState.Idle) return;
        AudioManager.Instance?.PlayLeverPull();
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
        AudioManager.Instance?.PlayLeverPull();
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

        SessionStatsManager.Instance?.RecordSpin(isFreeSpin ? 0 : currentBetAmount);

        StartCoroutine(ExecuteSpinRoutine(isFreeSpin));
    }

    private IEnumerator ExecuteSpinRoutine(bool isFreeSpin)
    {
        CurrentState = GameState.Spinning;
        reelsStoppedCount = 0;
        SetControlsInteractable(false);
        UpdateWinDisplay(0);

        AudioManager.Instance?.StartReelSpin();

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
    /// Simulates authentic slot mathematics with fair probability weighting and near-miss suspense.
    /// </summary>
    private SymbolData[] DetermineRNGOutcome()
    {
        // 1. Consume developer cheat/test forced outcome if armed
        if (forcedNextOutcome != null)
        {
            SymbolData[] forced = forcedNextOutcome;
            forcedNextOutcome = null; // consume once
            return forced;
        }

        SymbolData[] targets = new SymbolData[3];

        // 2. Controlled fair casino hit frequency (~12% chance of 3-of-a-kind)
        bool isWin = UnityEngine.Random.value < winProbability;

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
            // Losing roll: 30% chance of a dramatic near-miss (Reel 1 & 2 match, Reel 3 differs)
            bool isNearMiss = UnityEngine.Random.value < nearMissProbability;
            if (isNearMiss)
            {
                SymbolData highSym = PickWeightedRandomSymbol();
                targets[0] = highSym;
                targets[1] = highSym;
                targets[2] = GetDifferentSymbol(highSym);
            }
            else
            {
                // Independent roll ensuring not all 3 match
                targets[0] = PickWeightedRandomSymbol();
                targets[1] = PickWeightedRandomSymbol();
                targets[2] = PickWeightedRandomSymbol();

                if (targets[0].id == targets[1].id && targets[1].id == targets[2].id)
                {
                    targets[2] = GetDifferentSymbol(targets[0]);
                }
            }
        }

        return targets;
    }

    private SymbolData GetDifferentSymbol(SymbolData exclude)
    {
        if (symbols == null || symbols.Length <= 1) return exclude;
        for (int i = 0; i < symbols.Length; i++)
        {
            if (symbols[i] != null && symbols[i].id != exclude.id)
                return symbols[i];
        }
        return symbols[0];
    }

    #region Developer Test & Cheat Controls

    public void ForceSymbolWin(int symbolId)
    {
        if (CurrentState != GameState.Idle) return;
        SymbolData target = null;
        if (symbols != null)
        {
            for (int i = 0; i < symbols.Length; i++)
            {
                if (symbols[i] != null && symbols[i].id == symbolId)
                {
                    target = symbols[i];
                    break;
                }
            }
        }

        if (target != null)
        {
            forcedNextOutcome = new SymbolData[] { target, target, target };
            StartCoroutine(PullLeverAndSpinRoutine());
        }
    }

    public void ForceNearMissOutcome()
    {
        if (CurrentState != GameState.Idle) return;
        if (symbols != null && symbols.Length >= 2)
        {
            SymbolData highSym = symbols[0]; // Lucky Seven
            SymbolData diffSym = symbols.Length >= 4 ? symbols[3] : (symbols.Length > 2 ? symbols[2] : symbols[1]); // Classic Bar
            forcedNextOutcome = new SymbolData[] { highSym, highSym, diffSym };
            StartCoroutine(PullLeverAndSpinRoutine());
        }
    }

    public void ToggleDevPanel()
    {
        if (devPanel != null)
        {
            devPanel.SetActive(!devPanel.activeSelf);
        }
    }

    #endregion

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
        AudioManager.Instance?.PlayReelStop(reelsStoppedCount - 1);
        if (reelsStoppedCount >= reels.Length)
        {
            AudioManager.Instance?.StopReelSpin();
        }
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
            SessionStatsManager.Instance?.RecordWin(totalWin, match.isBonus);

            // Play celebratory audio fanfare
            if (match.isBonus)
            {
                AudioManager.Instance?.PlayFreeSpins();
            }
            else if (multiplier >= 100)
            {
                AudioManager.Instance?.PlayJackpot();
            }
            else
            {
                AudioManager.Instance?.PlayWin();
            }

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
