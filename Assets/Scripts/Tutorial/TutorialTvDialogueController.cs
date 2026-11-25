using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class TutorialTvDialogueController : MonoBehaviour
{
    public enum TutorialPattern
    {
        RedCross,
        YellowExclamation,
        RedExclamation
    }

    [Header("Scene references")]
    [SerializeField] private Camera interactionCamera;
    [SerializeField] private Collider promptTvCollider;
    [SerializeField] private TutorialTvScreenPresenter promptScreen;
    [SerializeField] private TutorialTvScreenPresenter leftEmojiScreen;
    [SerializeField] private TutorialTvScreenPresenter rightEmojiScreen;
    [SerializeField] private TutorialTvScreenPresenter[] sideTvScreens;
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private TopDownPlayer player;
    [SerializeField] private TutorialGridGhost ghostPrefab;
    [SerializeField] private TutorialHealthUIController completionUi;

    [Header("TV timing")]
    [SerializeField, Min(0f)] private float noSignalDuration = 0.28f;
    [SerializeField, Min(0.1f)] private float dialogueDuration = 3.1f;
    [SerializeField, Min(0.1f)] private float emojiInterval = 1.25f;
    [SerializeField, Min(0.1f)] private float symbolDisplayDuration = 2.2f;
    [SerializeField, Min(0f)] private float completionPanelDelay = 2.2f;

    [Header("Pac-Man tutorial")]
    [SerializeField, Min(0.1f)] private float ghostMoveSpeed = 3.5f;
    [SerializeField, Min(0f)] private float trailWarningDuration = 0.8f;
    [SerializeField, Min(0f)] private float trailHazardDuration = 1.6f;
    [SerializeField, Min(0f)] private float stagePause = 1f;

    [Header("Dialogue")]
    [SerializeField, TextArea] private string interactionPrompt = "Use mouse to interact...";
    [SerializeField, TextArea] private string[] introductionLines =
    {
        "(O v O)",
        "Welcome to Floor is Lava, I'm your guide!",
        "This is a very simple game! We'll first start with basic movement!",
        "Use WASD to move, Space to jump, and hold Shift to move faster!"
    };
    [SerializeField, TextArea] private string objectiveInstruction = "Try to step on this green tile!";
    [SerializeField, TextArea] private string[] colourIntroductionLines =
    {
        "Good job!",
        "Everything is determined by colour, even when its appearance changes.",
        "Green is your objective. Touch or step on every green tile!"
    };
    [SerializeField, TextArea] private string redInstruction =
        "Red means danger. Touching it reduces your health, so avoid anything red!";
    [SerializeField, TextArea] private string yellowInstruction =
        "Yellow is a warning before danger appears. Move away before it turns red!";
    [SerializeField, TextArea] private string challengeInstruction =
        "Collect every green tile. Red ghosts move one grid step at a time, and your trail becomes dangerous!";
    [SerializeField, TextArea] private string[] completionLines =
    {
        "Good job! You've learned the basics of the game!",
        "You can interact with me, so the mouse may also be used during the game!",
        "Interactions can vary, so I'll let you discover the rest yourself!"
    };

    [Header("ASCII emoji cycle")]
    [SerializeField] private string[] emojiFaces =
    {
        "(O v O)",
        "(^ o ^)",
        "(> w <)",
        "(T _ T)",
        "(> _ <)",
        "(- . -)",
        "(O _ O)",
        "(x _ x)"
    };

    private readonly List<TutorialObjectivePickup> activeObjectives = new List<TutorialObjectivePickup>();
    private readonly List<TutorialGridGhost> activeGhosts = new List<TutorialGridGhost>();
    private readonly HashSet<FloorTile> activeTrailTiles = new HashSet<FloorTile>();
    private Coroutine tutorialRoutine;
    private Coroutine emojiRoutine;
    private Coroutine completionRoutine;
    private bool challengeActive;
    private bool tutorialFinished;
    private bool bonusInteractionComplete;
#if UNITY_EDITOR
    private bool suppressRecordSavingForTest;
#endif
    private int challengeGeneration;
    private Vector2Int lastTrailCoordinate = new Vector2Int(int.MinValue, int.MinValue);

    public bool HasStarted => tutorialRoutine != null;
    public int ActiveObjectiveCount => activeObjectives.Count;
    public int ActiveGhostCount => activeGhosts.Count;
    public bool TutorialFinished => tutorialFinished;
    public bool BonusInteractionComplete => bonusInteractionComplete;

    private void Awake()
    {
        promptScreen?.SetTextImmediate(interactionPrompt);
        leftEmojiScreen?.Clear();
        rightEmojiScreen?.Clear();
        SetSideTvText(string.Empty);
        if (completionUi == null) completionUi = FindAnyObjectByType<TutorialHealthUIController>();
    }

    private void Update()
    {
        if (WasInteractionPressed() && IsPromptTvUnderPointer())
        {
            if (!tutorialFinished && tutorialRoutine == null) StartTutorial();
            else if (tutorialFinished && !bonusInteractionComplete && completionRoutine == null)
                completionRoutine = StartCoroutine(RunBonusInteraction());
        }

        if (challengeActive)
        {
            UpdateDangerTrail();
        }
    }

    public void StartTutorial()
    {
        if (tutorialRoutine != null || promptScreen == null || tileGrid == null || player == null)
        {
            return;
        }

        emojiRoutine = StartCoroutine(CycleEmoji());
        tutorialRoutine = StartCoroutine(RunTutorial());
    }

    public void NotifyObjectiveCollected(TutorialObjectivePickup objective)
    {
        if (objective == null)
        {
            return;
        }

        activeObjectives.Remove(objective);
        Destroy(objective);
    }

    private IEnumerator RunTutorial()
    {
        foreach (string line in introductionLines)
        {
            yield return ShowDialogue(line);
        }

        yield return ShowDialogue(objectiveInstruction, false);
        SpawnObjectives(new[] { new Vector2Int(tileGrid.Width / 2, tileGrid.Height / 2) });
        yield return WaitForObjectives();

        foreach (string line in colourIntroductionLines)
        {
            yield return ShowDialogue(line);
        }

        yield return ShowDialogue(redInstruction, false);
        ApplyPattern(TutorialPattern.RedCross);
        yield return WaitRealtime(symbolDisplayDuration);
        ClearPattern();

        yield return ShowDialogue(yellowInstruction, false);
        ApplyPattern(TutorialPattern.YellowExclamation);
        yield return WaitRealtime(symbolDisplayDuration);
        ApplyPattern(TutorialPattern.RedExclamation);
        yield return WaitRealtime(symbolDisplayDuration);
        ClearPattern();

        yield return ShowDialogue(challengeInstruction);
        for (int stage = 1; stage <= 3; stage++)
        {
            yield return ShowDialogue($"Stage {stage}: collect every green tile!", false);
            BeginChallengeStage(stage);
            yield return WaitForObjectives();
            EndChallengeStage();
            yield return ShowDialogue($"Stage {stage} complete!", false);
            yield return WaitRealtime(stagePause);
        }

        foreach (string line in completionLines)
        {
            yield return ShowDialogue(line);
        }

        promptScreen.SetTextImmediate("Tutorial complete!");
        tutorialFinished = true;
        tutorialRoutine = null;
        BestTimeRecord.SaveCompletionStatus(SceneManager.GetActiveScene().name, false);
    }

    private IEnumerator RunBonusInteraction()
    {
        bonusInteractionComplete = true;
        yield return ShowDialogue("There is nothing more for now.", false);
        yield return WaitRealtime(completionPanelDelay);
        bool saveRecord = true;
#if UNITY_EDITOR
        saveRecord = !suppressRecordSavingForTest;
#endif
        if (saveRecord) BestTimeRecord.SaveCompletionStatus(SceneManager.GetActiveScene().name, true);
        if (completionUi == null) completionUi = FindAnyObjectByType<TutorialHealthUIController>();
        completionUi?.ShowTutorialComplete();
        completionRoutine = null;
    }

    private IEnumerator ShowDialogue(string text, bool waitAfter = true)
    {
        yield return promptScreen.TransitionTo(text, noSignalDuration);
        if (waitAfter)
        {
            yield return WaitRealtime(dialogueDuration);
        }
    }

    private IEnumerator CycleEmoji()
    {
        leftEmojiScreen?.Clear();
        rightEmojiScreen?.Clear();
        yield return WaitRealtime(noSignalDuration);

        int index = 0;
        while (true)
        {
            if (emojiFaces == null || emojiFaces.Length == 0)
            {
                yield break;
            }

            leftEmojiScreen?.SetTextImmediate(emojiFaces[index % emojiFaces.Length]);
            SetSideTvText(emojiFaces[index % emojiFaces.Length]);
            int mirroredIndex = (emojiFaces.Length - 1 - index) % emojiFaces.Length;
            rightEmojiScreen?.SetTextImmediate(emojiFaces[mirroredIndex]);
            index = (index + 1) % emojiFaces.Length;
            yield return WaitRealtime(emojiInterval);
        }
    }

    private void SpawnObjectives(IReadOnlyList<Vector2Int> coordinates)
    {
        ClearObjectives();
        foreach (Vector2Int coordinate in coordinates)
        {
            FloorTile tile = tileGrid.GetTile(coordinate.x, coordinate.y);
            if (tile == null)
            {
                continue;
            }

            TutorialObjectivePickup pickup = tile.gameObject.AddComponent<TutorialObjectivePickup>();
            pickup.Activate(tile, this);
            activeObjectives.Add(pickup);
        }
    }

    private IEnumerator WaitForObjectives()
    {
        while (activeObjectives.Count > 0)
        {
            yield return null;
        }
    }

    private void BeginChallengeStage(int stage)
    {
        EndChallengeStage();
        challengeGeneration++;
        challengeActive = true;
        lastTrailCoordinate = new Vector2Int(int.MinValue, int.MinValue);

        Vector2Int[][] objectiveSets =
        {
            new[] { new Vector2Int(1, 1) },
            new[] { new Vector2Int(tileGrid.Width - 2, 1), new Vector2Int(1, tileGrid.Height - 2) },
            new[]
            {
                new Vector2Int(tileGrid.Width - 2, tileGrid.Height - 2),
                new Vector2Int(tileGrid.Width / 2, 1),
                new Vector2Int(1, tileGrid.Height / 2)
            }
        };
        Vector2Int[][] ghostSets =
        {
            new[] { new Vector2Int(tileGrid.Width - 2, tileGrid.Height - 2) },
            new[] { new Vector2Int(1, tileGrid.Height - 2), new Vector2Int(tileGrid.Width - 2, 1) },
            new[]
            {
                new Vector2Int(1, 1),
                new Vector2Int(tileGrid.Width - 2, tileGrid.Height - 2),
                new Vector2Int(tileGrid.Width / 2, tileGrid.Height - 2)
            }
        };

        int index = Mathf.Clamp(stage - 1, 0, objectiveSets.Length - 1);
        SpawnObjectives(objectiveSets[index]);
        SpawnGhosts(ghostSets[index]);
    }

    private void EndChallengeStage()
    {
        challengeActive = false;
        challengeGeneration++;
        ClearTrail();
        foreach (TutorialGridGhost ghost in activeGhosts)
        {
            if (ghost != null)
            {
                Destroy(ghost.gameObject);
            }
        }
        activeGhosts.Clear();
    }

    private void SpawnGhosts(IReadOnlyList<Vector2Int> coordinates)
    {
        if (ghostPrefab == null)
        {
            return;
        }

        foreach (Vector2Int coordinate in coordinates)
        {
            TutorialGridGhost ghost = Instantiate(ghostPrefab, transform);
            ghost.name = $"Tutorial Ghost {activeGhosts.Count + 1}";
            ghost.Initialize(tileGrid, player, coordinate, ghostMoveSpeed);
            activeGhosts.Add(ghost);
        }
    }

    private void UpdateDangerTrail()
    {
        Vector3 local = tileGrid.transform.InverseTransformPoint(player.transform.position);
        Vector2Int coordinate = new Vector2Int(
            Mathf.RoundToInt(local.x / tileGrid.CellSize),
            Mathf.RoundToInt(local.z / tileGrid.CellSize));
        if (coordinate == lastTrailCoordinate || coordinate.x < 0 || coordinate.x >= tileGrid.Width ||
            coordinate.y < 0 || coordinate.y >= tileGrid.Height)
        {
            return;
        }

        lastTrailCoordinate = coordinate;
        FloorTile tile = tileGrid.GetTile(coordinate.x, coordinate.y);
        if (tile != null && !activeTrailTiles.Contains(tile) &&
            (tile.GetComponent<TutorialObjectivePickup>() == null ||
             !tile.GetComponent<TutorialObjectivePickup>().IsActiveObjective))
        {
            activeTrailTiles.Add(tile);
            StartCoroutine(RunTrailTile(tile, challengeGeneration));
        }
    }

    private IEnumerator RunTrailTile(FloorTile tile, int generation)
    {
        tile.ApplyTileOverride(this, TileType.WarningUnjumpable, 300);
        yield return WaitRealtime(trailWarningDuration);
        if (!challengeActive || generation != challengeGeneration)
        {
            tile.RemoveTileOverride(this);
            activeTrailTiles.Remove(tile);
            yield break;
        }

        tile.ApplyTileOverride(this, TileType.HazardUnjumpable, 300);
        yield return WaitRealtime(trailHazardDuration);
        tile.RemoveTileOverride(this);
        activeTrailTiles.Remove(tile);
    }

    public void ApplyPattern(TutorialPattern pattern)
    {
        ClearPattern();
        int startColumn = Mathf.Max(0, (tileGrid.Width - 10) / 2);
        int startRow = Mathf.Max(0, (tileGrid.Height - 10) / 2);
        int patternWidth = Mathf.Min(10, tileGrid.Width);
        int patternHeight = Mathf.Min(10, tileGrid.Height);
        TileType displayType = pattern == TutorialPattern.YellowExclamation
            ? TileType.WarningUnjumpable
            : TileType.HazardUnjumpable;

        for (int y = 0; y < patternHeight; y++)
        {
            for (int x = 0; x < patternWidth; x++)
            {
                bool included = pattern == TutorialPattern.RedCross
                    ? x == y || x + y == patternWidth - 1
                    : IsExclamationCell(x, y, patternWidth, patternHeight);
                if (!included)
                {
                    continue;
                }

                tileGrid.GetTile(startColumn + x, startRow + y)
                    ?.ApplyTileOverride(promptScreen, displayType, 700);
            }
        }
    }

    private static bool IsExclamationCell(int x, int y, int width, int height)
    {
        int leftCentre = (width - 1) / 2;
        int rightCentre = width / 2;
        bool stem = (x == leftCentre || x == rightCentre) && y >= 3 && y < height;
        bool dot = (x == leftCentre || x == rightCentre) && y <= 1;
        return stem || dot;
    }

    public void ClearPattern()
    {
        if (tileGrid == null || promptScreen == null)
        {
            return;
        }

        foreach (FloorTile tile in tileGrid.GetComponentsInChildren<FloorTile>(true))
        {
            tile.RemoveTileOverride(promptScreen);
        }
    }

    private void ClearTrail()
    {
        if (tileGrid != null)
        {
            foreach (FloorTile tile in tileGrid.GetComponentsInChildren<FloorTile>(true))
            {
                tile.RemoveTileOverride(this);
            }
        }
        activeTrailTiles.Clear();
    }

    private void ClearObjectives()
    {
        foreach (TutorialObjectivePickup objective in activeObjectives)
        {
            if (objective != null)
            {
                objective.Deactivate();
                Destroy(objective);
            }
        }
        activeObjectives.Clear();
    }

    private bool WasInteractionPressed()
    {
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        for (int index = 0; index < Input.touchCount; index++)
        {
            if (Input.GetTouch(index).phase == TouchPhase.Began)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsPromptTvUnderPointer()
    {
        if (interactionCamera == null || promptTvCollider == null)
        {
            return false;
        }

        Vector3 pointerPosition = Input.mousePosition;
        if (Input.touchCount > 0)
        {
            pointerPosition = Input.GetTouch(0).position;
        }

        Ray ray = interactionCamera.ScreenPointToRay(pointerPosition);
        return Physics.Raycast(ray, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore) &&
               (hit.collider == promptTvCollider ||
                hit.transform.IsChildOf(promptTvCollider.transform) ||
                promptTvCollider.transform.IsChildOf(hit.transform));
    }

    private static IEnumerator WaitRealtime(float duration)
    {
        float remaining = Mathf.Max(0f, duration);
        while (remaining > 0f)
        {
            if (!PauseMenuController.IsGamePaused && Time.timeScale > 0f)
                remaining -= Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void SetSideTvText(string value)
    {
        if (sideTvScreens == null) return;
        foreach (TutorialTvScreenPresenter screen in sideTvScreens)
            if (screen != null) screen.SetTextImmediate(value);
    }

    private void OnDestroy()
    {
        if (emojiRoutine != null)
        {
            StopCoroutine(emojiRoutine);
        }
        ClearPattern();
        EndChallengeStage();
        ClearObjectives();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        Camera inputCamera,
        Collider middleTvCollider,
        TutorialTvScreenPresenter middleScreen,
        TutorialTvScreenPresenter leftScreen,
        TutorialTvScreenPresenter rightScreen,
        TileGrid grid,
        TopDownPlayer controlledPlayer,
        TutorialGridGhost ghost,
        TutorialTvScreenPresenter[] additionalSideScreens = null)
    {
        interactionCamera = inputCamera;
        promptTvCollider = middleTvCollider;
        promptScreen = middleScreen;
        leftEmojiScreen = leftScreen;
        rightEmojiScreen = rightScreen;
        tileGrid = grid;
        player = controlledPlayer;
        ghostPrefab = ghost;
        sideTvScreens = additionalSideScreens;
    }

    public void ConfigureCompletionUiForEditor(TutorialHealthUIController ui) => completionUi = ui;

    public void ConfigureFastTestTimings()
    {
        noSignalDuration = 0.04f;
        dialogueDuration = 0.06f;
        emojiInterval = 0.08f;
        symbolDisplayDuration = 0.08f;
        trailWarningDuration = 0.05f;
        trailHazardDuration = 0.1f;
        stagePause = 0.04f;
        completionPanelDelay = .25f;
    }

    public void StartChallengeStageForTest(int stage)
    {
        BeginChallengeStage(stage);
    }

    public void StopTutorialForTest()
    {
        if (tutorialRoutine != null)
        {
            StopCoroutine(tutorialRoutine);
            tutorialRoutine = null;
        }
        if (emojiRoutine != null)
        {
            StopCoroutine(emojiRoutine);
            emojiRoutine = null;
        }
        ClearPattern();
        EndChallengeStage();
        ClearObjectives();
    }

    public void CompleteDialogueForTest()
    {
        suppressRecordSavingForTest = true;
        tutorialFinished = true;
        tutorialRoutine = null;
        promptScreen?.SetTextImmediate("Tutorial complete!");
    }

    public void TriggerBonusInteractionForTest()
    {
        if (tutorialFinished && !bonusInteractionComplete && completionRoutine == null)
            completionRoutine = StartCoroutine(RunBonusInteraction());
    }
#endif
}
