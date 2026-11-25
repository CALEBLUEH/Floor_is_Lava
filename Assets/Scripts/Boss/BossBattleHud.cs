using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BossBattleHud : MonoBehaviour
{
    [SerializeField] private BlueBossController blueBoss;
    [SerializeField] private DarkRedBossController darkRedBoss;
    [SerializeField] private PinkBossController pinkBoss;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private Slider bossHealthSlider;
    [SerializeField] private TMP_Text bossLabel;
    [SerializeField] private TMP_Text playerLabel;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private GameObject bossHealthRoot;
    [SerializeField] private Image bossHealthFill;
    [SerializeField] private Color blueHealthColour = new(.08f, .3f, 1f);
    [SerializeField] private Color darkRedHealthColour = new(.75f, .015f, .025f);
    [SerializeField] private Color pinkHealthColour = new(1f, .08f, .62f);
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverTitle;
    [SerializeField] private TMP_Text gameOverSummary;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button levelSelectButton;
    private bool gameOverShown;
    private float levelStartedAt;

    public bool IsGameOverShown => gameOverShown;

    private void OnEnable()
    {
        levelStartedAt = Time.unscaledTime;
        if (blueBoss != null) blueBoss.HealthChanged += OnBlueHealthChanged;
        if (darkRedBoss != null) darkRedBoss.HealthChanged += OnDarkRedHealthChanged;
        if (pinkBoss != null) pinkBoss.HealthChanged += OnPinkHealthChanged;
        if (bossHealthRoot == null && bossHealthSlider != null) bossHealthRoot = bossHealthSlider.gameObject;
        if (bossHealthFill == null && bossHealthSlider != null && bossHealthSlider.fillRect != null)
            bossHealthFill = bossHealthSlider.fillRect.GetComponent<Image>();
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        BindButtons();
        Refresh();
    }

    private void OnDisable()
    {
        if (blueBoss != null) blueBoss.HealthChanged -= OnBlueHealthChanged;
        if (darkRedBoss != null) darkRedBoss.HealthChanged -= OnDarkRedHealthChanged;
        if (pinkBoss != null) pinkBoss.HealthChanged -= OnPinkHealthChanged;
    }

    private void Update()
    {
        RefreshPlayer();
        RefreshActiveBoss();
        if (!gameOverShown && playerLife != null && playerLife.IsDefeated) ShowGameOver();
    }

    private void OnBlueHealthChanged(int current, int maximum)
    {
        if (darkRedBoss == null || !darkRedBoss.HasEnteredArena) SetBossHealth("BLUE", current, maximum);
    }

    private void OnDarkRedHealthChanged(int current, int maximum)
    {
        if (darkRedBoss != null && darkRedBoss.HasEnteredArena) SetBossHealth("DARK RED", current, maximum);
    }

    private void OnPinkHealthChanged(int current, int maximum)
    {
        if (pinkBoss != null && pinkBoss.HasEnteredArena) SetBossHealth("PINK", current, maximum, pinkHealthColour);
    }

    private void Refresh()
    {
        RefreshActiveBoss();
        RefreshPlayer();
    }

    private void RefreshActiveBoss()
    {
        if (pinkBoss != null && pinkBoss.HasEnteredArena && !pinkBoss.DefeatSequenceComplete)
        {
            if (pinkBoss.IsInDesperation) SetDesperationMeter();
            else SetBossHealth("PINK", pinkBoss.CurrentHealth, pinkBoss.MaxHealth, pinkHealthColour);
        }
        else if (darkRedBoss != null && darkRedBoss.HasEnteredArena && !darkRedBoss.IsDefeated)
            SetBossHealth("DARK RED", darkRedBoss.CurrentHealth, darkRedBoss.MaxHealth, darkRedHealthColour);
        else if (blueBoss != null && blueBoss.HasEnteredArena && !blueBoss.IsDefeated)
            SetBossHealth("BLUE", blueBoss.CurrentHealth, blueBoss.MaxHealth, blueHealthColour);
        else if (bossHealthRoot != null)
            bossHealthRoot.SetActive(false);
    }

    private void SetBossHealth(string bossName, int current, int maximum)
    {
        Color colour = bossName == "BLUE" ? blueHealthColour : darkRedHealthColour;
        SetBossHealth(bossName, current, maximum, colour);
    }

    private void SetBossHealth(string bossName, int current, int maximum, Color colour)
    {
        if (bossHealthRoot != null) bossHealthRoot.SetActive(true);
        if (bossHealthSlider != null)
        {
            bossHealthSlider.minValue = 0f;
            bossHealthSlider.maxValue = maximum;
            bossHealthSlider.value = current;
        }
        if (bossHealthFill != null) bossHealthFill.color = colour;
        if (bossLabel != null) bossLabel.text = $"{bossName}  {current} / {maximum}";
        if (stateLabel != null) stateLabel.text = string.Empty;
    }

    private void SetDesperationMeter()
    {
        if (bossHealthRoot != null) bossHealthRoot.SetActive(true);
        if (bossHealthSlider != null)
        {
            bossHealthSlider.minValue = 0f;
            bossHealthSlider.maxValue = 1f;
            bossHealthSlider.value = pinkBoss.DesperationRemainingNormalized;
        }
        if (bossHealthFill != null) bossHealthFill.color = pinkHealthColour;
        if (bossLabel != null) bossLabel.text = "DESPERATION";
        if (stateLabel != null) stateLabel.text = string.Empty;
    }

    public void ShowVictory()
    {
        if (gameOverShown) return;
        gameOverShown = true;
        bool perfect = playerLife == null || playerLife.CurrentLives == playerLife.MaxLives;
        BestTimeRecord.SaveCompletion(SceneManager.GetActiveScene().name,
            Mathf.Max(.01f, Time.unscaledTime - levelStartedAt), perfect);
        if (gameOverTitle != null) gameOverTitle.text = "VICTORY";
        if (gameOverSummary != null) gameOverSummary.text = "All three bosses have been defeated.";
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            CanvasGroup group = gameOverPanel.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            else Debug.LogError("Boss game-over panel needs an authored CanvasGroup.", gameOverPanel);
        }
        if (playerLife != null) playerLife.GetComponent<TopDownPlayer>()?.SetControlEnabled(false);
        if (bossHealthRoot != null) bossHealthRoot.SetActive(false);
        Time.timeScale = 0f;
    }

    private void RefreshPlayer()
    {
        if (playerLife != null && playerLabel != null)
            playerLabel.text = $"HP  {playerLife.CurrentLives} / {playerLife.MaxLives}";
    }

    private void ShowGameOver()
    {
        gameOverShown = true;
        if (gameOverTitle != null) gameOverTitle.text = "GAME OVER";
        if (gameOverSummary != null) gameOverSummary.text = "You ran out of HP. Try the boss battle again.";
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            CanvasGroup group = gameOverPanel.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.interactable = true;
                group.blocksRaycasts = true;
            }
            else Debug.LogError("Boss game-over panel needs an authored CanvasGroup.", gameOverPanel);
        }
        Time.timeScale = 0f;
    }

    private void BindButtons()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartLevel);
            restartButton.onClick.AddListener(RestartLevel);
        }
        if (levelSelectButton != null)
        {
            levelSelectButton.onClick.RemoveListener(LoadLevelSelection);
            levelSelectButton.onClick.AddListener(LoadLevelSelection);
        }
    }

    private static void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private static void LoadLevelSelection()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Interface_LevelSelection");
    }

    private void OnDestroy()
    {
        if (gameOverShown && Time.timeScale == 0f) Time.timeScale = 1f;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(BlueBossController blue, PlayerLife life, Image legacyBossFill,
        TMP_Text healthLabel, Image legacyPlayerFill, TMP_Text hpLabel, TMP_Text battleState)
    {
        blueBoss = blue;
        playerLife = life;
        bossLabel = healthLabel;
        playerLabel = hpLabel;
        stateLabel = battleState;
    }

    public void ConfigureForEditor(BlueBossController blue, DarkRedBossController darkRed, PlayerLife life,
        Slider healthSlider, TMP_Text healthLabel, TMP_Text hpLabel, TMP_Text battleState,
        GameObject endingPanel, TMP_Text endingTitle, TMP_Text endingSummary, Button retryButton, Button selectButton)
    {
        blueBoss = blue;
        darkRedBoss = darkRed;
        playerLife = life;
        bossHealthSlider = healthSlider;
        bossLabel = healthLabel;
        playerLabel = hpLabel;
        stateLabel = battleState;
        gameOverPanel = endingPanel;
        gameOverTitle = endingTitle;
        gameOverSummary = endingSummary;
        restartButton = retryButton;
        levelSelectButton = selectButton;
    }


    public void ConfigureBossPhasesForEditor(PinkBossController pink, GameObject healthRoot, Image healthFill)
    {
        pinkBoss = pink;
        bossHealthRoot = healthRoot;
        bossHealthFill = healthFill;
    }
#endif
}
