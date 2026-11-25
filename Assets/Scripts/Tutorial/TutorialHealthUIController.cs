using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialHealthUIController : MonoBehaviour
{
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text gameOverTitle;
    [SerializeField] private TMP_Text gameOverSummary;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button levelSelectButton;

    private bool gameOverShown;

    public bool IsGameOverShown => gameOverShown;

    private void Awake()
    {
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
        }
        BindButtons();
        RefreshHealth();
    }

    private void Update()
    {
        RefreshHealth();
        if (!gameOverShown && playerLife != null && playerLife.IsDefeated)
        {
            ShowGameOver();
        }
    }

    private void RefreshHealth()
    {
        if (healthText != null && playerLife != null)
        {
            healthText.text = $"HP  {playerLife.CurrentLives} / {playerLife.MaxLives}";
        }
    }

    private void ShowGameOver()
    {
        gameOverShown = true;
        if (gameOverTitle != null)
        {
            gameOverTitle.text = "GAME OVER";
        }
        if (gameOverSummary != null)
        {
            gameOverSummary.text = "You ran out of HP. Try the tutorial again.";
        }
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
            else Debug.LogError("Tutorial result panel needs an authored CanvasGroup.", gameOverPanel);
        }
        Time.timeScale = 0f;
    }

    public void ShowTutorialComplete()
    {
        if (gameOverShown) return;
        gameOverShown = true;
        GameplayOutcomeAudio.PlayLevelComplete();
        if (gameOverTitle != null) gameOverTitle.text = "TUTORIAL COMPLETE";
        if (gameOverSummary != null)
            gameOverSummary.text = "You found the final TV message.\nThe Tutorial floor is now gold.";
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
            else Debug.LogError("Tutorial result panel needs an authored CanvasGroup.", gameOverPanel);
        }
        playerLife?.GetComponent<TopDownPlayer>()?.SetControlEnabled(false);
        Time.timeScale = 0f;
    }

    private void BindButtons()
    {
        if (restartButton != null)
        {
            restartButton.onClick.RemoveListener(RestartTutorial);
            restartButton.onClick.AddListener(RestartTutorial);
        }
        if (levelSelectButton != null)
        {
            levelSelectButton.onClick.RemoveListener(LoadLevelSelection);
            levelSelectButton.onClick.AddListener(LoadLevelSelection);
        }
    }

    private static void RestartTutorial()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        SceneManager.LoadScene(scene.name);
    }

    private static void LoadLevelSelection()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Interface_LevelSelection");
    }

    private void OnDestroy()
    {
        if (Time.timeScale == 0f)
        {
            Time.timeScale = 1f;
        }
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        PlayerLife life,
        TMP_Text hpLabel,
        GameObject endingPanel,
        TMP_Text endingTitle,
        TMP_Text endingSummary,
        Button retryButton,
        Button selectButton)
    {
        playerLife = life;
        healthText = hpLabel;
        gameOverPanel = endingPanel;
        gameOverTitle = endingTitle;
        gameOverSummary = endingSummary;
        restartButton = retryButton;
        levelSelectButton = selectButton;
    }
#endif
}
