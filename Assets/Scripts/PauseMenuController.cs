using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }
    public static bool IsGamePaused => Instance != null && Instance.paused;
    public bool IsPaused => paused;

    [SerializeField] private GameObject panel;
    [SerializeField] private GameObject mainPage;
    [SerializeField] private GameObject audioPage;
    [SerializeField] private Text feedback;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button audioButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private Button resetDataButton;
    [SerializeField] private Button audioBackButton;
    [SerializeField] private Button nextMinuteButton;
    [SerializeField] private Button survivalSpeedButton;
    [SerializeField] private Text survivalSpeedLabel;
    [SerializeField] private Slider bgmSlider;
    [SerializeField] private Slider sfxSlider;
    private bool paused;
    private bool levelSelection;
    private float previousTimeScale = 1f;
    private CursorLockMode previousLockMode;
    private bool previousCursorVisible;
    private SurvivalFirstMinuteController survivalController;

    // Kept for compatibility with existing level-entry scripts. Pause menus are
    // authored in each supported scene, so this method intentionally creates nothing.
    public static void EnsureInstalled() { }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        levelSelection = SceneManager.GetActiveScene().name == "Interface_LevelSelection";
        survivalController = FindAnyObjectByType<SurvivalFirstMinuteController>();
        BindUi();
        if (panel != null) panel.SetActive(false);
    }

    private void Update() { if (Input.GetKeyDown(KeyCode.Escape)) TogglePause(); }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (paused) RestoreState();
    }

    public void TogglePause() => SetPaused(!paused);

    public void SetResumeTimeScale(float value)
    {
        float safeValue = Mathf.Clamp(value, .1f, 4f);
        if (paused) previousTimeScale = safeValue;
        else Time.timeScale = safeValue;
    }

    public void SetPaused(bool value)
    {
        if (paused == value) return;
        if (value && panel == null) return;
        paused = value;
        if (paused)
        {
            previousTimeScale = Time.timeScale;
            previousLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            ShowMainPage();
        }
        else RestoreState();
        if (panel != null) panel.SetActive(paused);
    }

    private void RestoreState()
    {
        Time.timeScale = previousTimeScale;
        Cursor.lockState = previousLockMode;
        Cursor.visible = previousCursorVisible;
    }

    private void BindUi()
    {
        Bind(continueButton, () => SetPaused(false));
        Bind(audioButton, ShowAudioPage);
        Bind(restartButton, Restart);
        Bind(levelSelectButton, () => LoadScene("Interface_LevelSelection"));
        Bind(resetDataButton, ResetRecords);
        Bind(audioBackButton, ShowMainPage);
        Bind(nextMinuteButton, () => survivalController?.SkipToNextMinute());
        Bind(survivalSpeedButton, ToggleSurvivalSpeed);
        if (bgmSlider != null)
        {
            bgmSlider.SetValueWithoutNotify(GameSettings.BgmVolume);
            bgmSlider.onValueChanged.AddListener(SetBgmVolume);
        }
        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(GameSettings.SfxVolume);
            sfxSlider.onValueChanged.AddListener(SetSfxVolume);
        }
    }

    private static void Bind(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null) return;
        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    private static void SetBgmVolume(float value) => GameSettings.BgmVolume = value;
    private static void SetSfxVolume(float value) => GameSettings.SfxVolume = value;

    private void ShowMainPage()
    {
        if (mainPage != null) mainPage.SetActive(true);
        if (audioPage != null) audioPage.SetActive(false);
    }

    private void ShowAudioPage()
    {
        if (mainPage != null) mainPage.SetActive(false);
        if (audioPage != null) audioPage.SetActive(true);
    }

    private void ResetRecords()
    {
        BestTimeRecord.ResetAll();
        if (feedback != null) feedback.text = "ALL RECORDS RESET  •  ESC TO CLOSE";
    }

    private void ToggleSurvivalSpeed()
    {
        if (survivalController == null) return;
        survivalController.ToggleDoubleSpeed();
        if (survivalSpeedLabel != null)
            survivalSpeedLabel.text = survivalController.IsDoubleSpeed ? "SPEED 2X: ON" : "SPEED 2X: OFF";
    }

    private static void Restart()
    {
        Time.timeScale = 1f; Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex); else SceneManager.LoadScene(scene.name);
    }

    private static void LoadScene(string scene) { Time.timeScale = 1f; SceneManager.LoadScene(scene); }

#if UNITY_EDITOR
    public void ConfigureAuthoredUi(GameObject authoredPanel, GameObject authoredMainPage, GameObject authoredAudioPage,
        Text authoredFeedback, Button authoredContinue, Button authoredAudio, Button authoredRestart,
        Button authoredLevelSelect, Button authoredReset, Button authoredBack, Button authoredNextMinute,
        Button authoredSpeed, Text authoredSpeedLabel, Slider authoredBgm, Slider authoredSfx)
    {
        panel = authoredPanel;
        mainPage = authoredMainPage;
        audioPage = authoredAudioPage;
        feedback = authoredFeedback;
        continueButton = authoredContinue;
        audioButton = authoredAudio;
        restartButton = authoredRestart;
        levelSelectButton = authoredLevelSelect;
        resetDataButton = authoredReset;
        audioBackButton = authoredBack;
        nextMinuteButton = authoredNextMinute;
        survivalSpeedButton = authoredSpeed;
        survivalSpeedLabel = authoredSpeedLabel;
        bgmSlider = authoredBgm;
        sfxSlider = authoredSfx;
    }
#endif
}
