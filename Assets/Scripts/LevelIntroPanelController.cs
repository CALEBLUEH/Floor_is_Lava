using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelIntroPanelController : MonoBehaviour
{
    [SerializeField] private TopDownPlayer player;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Button enterButton;
    [SerializeField] private string levelTitle = "LEVEL";
    [SerializeField, TextArea] private string levelDescription;

    private bool entered;

    private void Awake()
    {
        PauseMenuController.EnsureInstalled();
        if (enterButton != null) enterButton.onClick.AddListener(EnterLevel);
    }

    private void Start()
    {
        if (titleText != null) titleText.text = levelTitle;
        if (descriptionText != null) descriptionText.text = levelDescription;
        if (panel != null) panel.SetActive(true);
        player?.SetControlEnabled(false);
        Time.timeScale = 0f;
    }

    private void Update()
    {
        if (!entered && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))) EnterLevel();
    }

    public void EnterLevel()
    {
        if (entered) return;
        entered = true;
        if (panel != null) panel.SetActive(false);
        Time.timeScale = 1f;
        player?.SetControlEnabled(true);
        playerLife?.GrantInvincibility(2f);
    }

    private void OnDestroy()
    {
        if (enterButton != null) enterButton.onClick.RemoveListener(EnterLevel);
        if (!entered && Time.timeScale == 0f) Time.timeScale = 1f;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(TopDownPlayer targetPlayer, PlayerLife life, GameObject introPanel,
        TMP_Text title, TMP_Text description, Button button, string heading, string body)
    {
        player = targetPlayer;
        playerLife = life;
        panel = introPanel;
        titleText = title;
        descriptionText = description;
        enterButton = button;
        levelTitle = heading;
        levelDescription = body;
    }
#endif
}
