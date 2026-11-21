using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the UI and interactions in the Start/Menu Scene.
/// Allows players to view and top-up their balance before launching the game.
/// </summary>
public class StartMenuController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI balanceText;
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button add100Button;
    [SerializeField] private Button add500Button;
    [SerializeField] private Button add1000Button;
    [SerializeField] private Button resetButton;

    [Header("Rules Modal")]
    [SerializeField] private GameObject rulesModal;
    [SerializeField] private Button openRulesButton;
    [SerializeField] private Button closeRulesButton;

    [Header("Scene Configuration")]
    [SerializeField] private string gameSceneName = "GameScene";

    private void Start()
    {
        // Ensure a PlayerWallet exists in the scene
        if (PlayerWallet.Instance == null)
        {
            GameObject walletObj = new GameObject("PlayerWallet");
            walletObj.AddComponent<PlayerWallet>();
        }

        // Subscribe to balance change events
        PlayerWallet.Instance.OnBalanceChanged += UpdateBalanceDisplay;

        // Hook up button listeners
        if (startGameButton != null)
            startGameButton.onClick.AddListener(StartGame);

        if (add100Button != null)
            add100Button.onClick.AddListener(() => AddMoney(100));

        if (add500Button != null)
            add500Button.onClick.AddListener(() => AddMoney(500));

        if (add1000Button != null)
            add1000Button.onClick.AddListener(() => AddMoney(1000));

        if (resetButton != null)
            resetButton.onClick.AddListener(ResetBalance);

        if (openRulesButton != null && rulesModal != null)
            openRulesButton.onClick.AddListener(() => rulesModal.SetActive(true));

        if (closeRulesButton != null && rulesModal != null)
            closeRulesButton.onClick.AddListener(() => rulesModal.SetActive(false));

        if (rulesModal != null)
            rulesModal.SetActive(false);

        // Initial UI display
        UpdateBalanceDisplay(PlayerWallet.Instance.CurrentBalance);
    }

    private void OnDestroy()
    {
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.OnBalanceChanged -= UpdateBalanceDisplay;
        }
    }

    private void UpdateBalanceDisplay(int newBalance)
    {
        if (balanceText != null)
        {
            balanceText.text = $"${newBalance:N0}";
        }
    }

    public void AddMoney(int amount)
    {
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.AddMoney(amount);
        }
    }

    public void ResetBalance()
    {
        if (PlayerWallet.Instance != null)
        {
            PlayerWallet.Instance.ResetBalance();
        }
    }

    public void StartGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}
