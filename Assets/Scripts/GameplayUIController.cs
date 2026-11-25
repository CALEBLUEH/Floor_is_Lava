using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class GameplayUIController : MonoBehaviour
{
    [SerializeField] private TopDownPlayer player;
    [SerializeField] private PlayerLife playerLife;
    [Header("Start panel")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private TMP_Text startTitleText;
    [SerializeField] private TMP_Text startInstructionsText;
    [SerializeField] private Button startButton;
    [SerializeField] private string levelTitle = "LEVEL";
    [SerializeField, TextArea] private string levelInstructions;
    [Header("HUD")]
    [SerializeField] private TMP_Text livesText;
    [SerializeField] private TMP_Text objectivesText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField, Range(0, 2)] private int timerDecimalPlaces;
    [SerializeField] private string playerHealthLabel = "LIVES";
    [Header("Ending panel")]
    [SerializeField] private GameObject endingPanel;
    [SerializeField] private TMP_Text endingTitleText;
    [SerializeField] private TMP_Text endingSummaryText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button levelSelectButton;

    private bool endingShown;
    private bool gameStarted;
    private int currentWave = 1;
    private int totalWaves = 1;
    private string objectiveOverride;
    private float elapsedTime;
    private float timerOverride = -1f;
    private string timerOverrideLabel = "TIME";

    public bool IsGameStarted => gameStarted;
    public float ElapsedTime => elapsedTime;

    private void Awake()
    {
        PauseMenuController.EnsureInstalled();
        ResolveReferences();
        EnsureReadableFonts();
        BindButtons();
    }

    private void Start()
    {
        ResolveReferences(); EnsureReadableFonts(); BindButtons();
        GameManager.Instance?.RegisterUI(this);
        if (startTitleText != null) startTitleText.text = levelTitle;
        if (startInstructionsText != null) startInstructionsText.text = levelInstructions;
        SetStartButtonLabel();
        if (startPanel != null)
        {
            startPanel.SetActive(true);
            CanvasGroup group = startPanel.GetComponent<CanvasGroup>();
            if (group == null) Debug.LogError("Start Panel requires an authored CanvasGroup.", startPanel);
            if (group == null) return;
            group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true;
        }
        if (endingPanel != null) endingPanel.SetActive(false);
        player?.SetControlEnabled(false);
        Time.timeScale = 0f;
        RefreshHud();
    }

    private void Update()
    {
        if (!gameStarted && !endingShown && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))) StartGame();
        if (gameStarted && !endingShown && Time.timeScale > 0f) elapsedTime += Time.deltaTime;
        RefreshHud();
    }

    private void OnDestroy() { if (Time.timeScale == 0f) Time.timeScale = 1f; }

    public void StartGame()
    {
        if (endingShown || gameStarted) return;
        gameStarted = true;
        if (startPanel != null) startPanel.SetActive(false);
        player?.SetControlEnabled(FindAnyObjectByType<MemoryGridController>() == null);
        Time.timeScale = 1f;
        playerLife?.GrantInvincibility(2f);
    }

    public void ShowLevelComplete()
    {
        bool perfect = playerLife != null && playerLife.CurrentLives == playerLife.MaxLives;
        BestTimeRecord.SaveCompletion(SceneManager.GetActiveScene().name, elapsedTime, perfect);
        ShowEnding("LEVEL COMPLETE", "All waves have been completed.");
    }
    public void ShowGameOver() => ShowGameOver(true);

    private void ShowGameOver(bool saveSurvivalRecord)
    {
        if (SceneManager.GetActiveScene().name == "Level_Survival")
        {
            if (saveSurvivalRecord) BestTimeRecord.SaveSurvivalResult(elapsedTime);
            string tier = elapsedTime >= BestTimeRecord.SurvivalPerfectSeconds
                ? "GOLD RECORD"
                : elapsedTime >= BestTimeRecord.SurvivalClearSeconds ? "LEVEL CLEARED" : "KEEP GOING";
            ShowEnding("GAME OVER", $"SURVIVAL TIME\n{BestTimeRecord.Format(elapsedTime)}\n{tier}");
            return;
        }
        ShowEnding("GAME OVER", "You ran out of lives. Try the level again.");
    }

    public void SetWave(int wave, int waveCount)
    {
        currentWave = Mathf.Max(1, wave); totalWaves = Mathf.Max(currentWave, waveCount); RefreshHud();
    }

    public void SetObjectiveStatus(string status) { objectiveOverride = status; RefreshHud(); }
    public void ClearObjectiveStatus() { objectiveOverride = null; RefreshHud(); }
    public void SetLevelInstructions(string instructions)
    {
        levelInstructions = instructions;
        if (startInstructionsText != null) startInstructionsText.text = levelInstructions;
    }

    public void SetTimerCountdown(float seconds, string label = "MEMORIZE")
    {
        timerOverride = Mathf.Max(0f, seconds);
        timerOverrideLabel = string.IsNullOrWhiteSpace(label) ? "TIME" : label.ToUpperInvariant();
        RefreshHud();
    }
    public void ClearTimerCountdown() { timerOverride = -1f; RefreshHud(); }

    public void SetElapsedTime(float seconds)
    {
        elapsedTime = Mathf.Max(0f, seconds);
        RefreshHud();
    }

    private void ShowEnding(string title, string summary)
    {
        if (endingShown) return;
        endingShown = true;
        if (title == "LEVEL COMPLETE") GameplayOutcomeAudio.PlayLevelComplete();
        else if (title == "GAME OVER") GameplayOutcomeAudio.PlayGameOver();
        if (endingTitleText != null)
        {
            endingTitleText.text = title;
            if (SceneManager.GetActiveScene().name == "Level_Pong")
                endingTitleText.color = title == "GAME OVER"
                    ? new Color(1f, .42f, .32f, 1f)
                    : new Color(.2f, 1f, .72f, 1f);
        }
        if (endingSummaryText != null)
        {
            endingSummaryText.text = summary;
            if (SceneManager.GetActiveScene().name == "Level_Pong")
                endingSummaryText.color = new Color(.94f, .97f, 1f, 1f);
        }
        if (endingPanel != null)
        {
            endingPanel.SetActive(true);
            CanvasGroup group = endingPanel.GetComponent<CanvasGroup>();
            if (group == null) Debug.LogError("Ending Panel requires an authored CanvasGroup.", endingPanel);
            if (group == null) return;
            group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true;
        }
        player?.SetControlEnabled(false); Time.timeScale = 0f;
    }

    private void RestartLevel()
    {
        Time.timeScale = 1f; Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex); else SceneManager.LoadScene(scene.name);
    }

    private void LoadLevelSelection()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Interface_LevelSelection");
    }

    private void RefreshHud()
    {
        if (livesText != null && playerLife != null)
            livesText.text = $"{playerHealthLabel}  {playerLife.CurrentLives} / {playerLife.MaxLives}";
        if (objectivesText != null) objectivesText.text = !string.IsNullOrEmpty(objectiveOverride)
            ? objectiveOverride : $"GREEN TILES: {(GameManager.Instance != null ? GameManager.Instance.ObjectiveTilesRemaining : 0)}";
        if (waveText != null) waveText.text = totalWaves > currentWave
            ? $"WAVE {currentWave}/{totalWaves}"
            : $"WAVE {currentWave}";
        if (timerText != null)
        {
            if (timerOverride >= 0f) timerText.text = $"{timerOverrideLabel}  {Mathf.CeilToInt(timerOverride):00}";
            else if (timerDecimalPlaces >= 2)
            {
                int minutes = Mathf.FloorToInt(elapsedTime / 60f);
                float seconds = elapsedTime - minutes * 60f;
                timerText.text = $"TIME  {minutes:00}:{seconds:00.00}";
            }
            else
            {
                timerText.text = $"TIME  {Mathf.FloorToInt(elapsedTime / 60f):00}:{Mathf.FloorToInt(elapsedTime % 60f):00}";
            }
        }
    }

    private void ResolveReferences()
    {
        if (player == null) player = FindAnyObjectByType<TopDownPlayer>();
        PlayerLife currentLife = player != null ? player.GetComponent<PlayerLife>() : FindAnyObjectByType<PlayerLife>();
        if (currentLife != null) playerLife = currentLife;
        if (startPanel == null) startPanel = FindChild("Start Panel")?.gameObject;
        if (endingPanel == null) endingPanel = FindChild("Ending Panel")?.gameObject;
        if (livesText == null) livesText = FindChild("Lives")?.GetComponent<TMP_Text>();
        if (waveText == null) waveText = FindChild("Wave Text")?.GetComponent<TMP_Text>();
        if (timerText == null) timerText = FindChild("Timer")?.GetComponent<TMP_Text>();
        if (startButton == null && startPanel != null) startButton = startPanel.GetComponentInChildren<Button>(true);
        if (restartButton == null && endingPanel != null) restartButton = endingPanel.GetComponentInChildren<Button>(true);
        if (levelSelectButton == null && endingPanel != null)
            levelSelectButton = endingPanel.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(button => button.name == "Level Select Button");
    }

    private void EnsureReadableFonts()
    {
        TMP_FontAsset readableFont = TMP_Settings.defaultFontAsset;
        if (readableFont == null) return;
        foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font == null || text.font.characterTable == null || text.font.characterTable.Count == 0)
                text.font = readableFont;
            text.enabled = true;
        }
    }

    private void BindButtons()
    {
        if (startButton != null) { startButton.onClick.RemoveListener(StartGame); startButton.onClick.AddListener(StartGame); startButton.interactable = true; }
        if (restartButton != null) { restartButton.onClick.RemoveListener(RestartLevel); restartButton.onClick.AddListener(RestartLevel); restartButton.interactable = true; }
        if (levelSelectButton != null) { levelSelectButton.onClick.RemoveListener(LoadLevelSelection); levelSelectButton.onClick.AddListener(LoadLevelSelection); levelSelectButton.interactable = true; }
    }

    private void SetStartButtonLabel()
    {
        if (startButton == null) return;
        TMP_Text tmpLabel = startButton.GetComponentInChildren<TMP_Text>(true);
        if (tmpLabel != null) { tmpLabel.text = "ENTER"; return; }
        Text legacyLabel = startButton.GetComponentInChildren<Text>(true);
        if (legacyLabel != null) legacyLabel.text = "ENTER";
    }

    private Transform FindChild(string objectName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true)) if (child.name == objectName) return child;
        return null;
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(TopDownPlayer targetPlayer, PlayerLife life, GameObject start, TMP_Text startTitle,
        TMP_Text instructions, Button beginButton, TMP_Text lifeLabel, TMP_Text objectiveLabel, GameObject ending,
        TMP_Text endingTitle, TMP_Text endingSummary, Button retryButton, string title, string instructionText)
    {
        player = targetPlayer; playerLife = life; startPanel = start; startTitleText = startTitle;
        startInstructionsText = instructions; startButton = beginButton; livesText = lifeLabel;
        objectivesText = objectiveLabel; endingPanel = ending; endingTitleText = endingTitle;
        endingSummaryText = endingSummary; restartButton = retryButton; levelTitle = title; levelInstructions = instructionText;
    }
    public void ConfigureWaveText(TMP_Text label) => waveText = label;
    public void ConfigureTimerText(TMP_Text label) => timerText = label;
    public void ConfigureSurvivalHud(TMP_Text timerLabel, TMP_Text survivalWaveLabel = null,
        TMP_Text healthLabel = null)
    {
        objectivesText = null;
        waveText = survivalWaveLabel;
        timerText = timerLabel;
        if (healthLabel != null) livesText = healthLabel;
        timerDecimalPlaces = 2;
        playerHealthLabel = "HP";
    }

    public void ConfigureEndingPanelForEditor(GameObject panel, TMP_Text title, TMP_Text summary,
        Button retry, Button select)
    {
        endingPanel = panel;
        endingTitleText = title;
        endingSummaryText = summary;
        restartButton = retry;
        levelSelectButton = select;
    }

    public void ShowGameOverWithoutSavingForTest() => ShowGameOver(false);
#endif
}
