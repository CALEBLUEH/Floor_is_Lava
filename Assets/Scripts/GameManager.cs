using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField, FormerlySerializedAs("greenTilesRemaining")]
    private int objectiveTilesRemaining;

    private readonly HashSet<FloorTile> incompleteObjectives = new HashSet<FloorTile>();
    private GameplayUIController gameplayUI;
    private float levelStartedAt;

    public int ObjectiveTilesRemaining => objectiveTilesRemaining;
    public bool IsLevelComplete { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("More than one GameManager exists. Using the first instance.", this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        levelStartedAt = Time.time;
        RecountObjectives();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RegisterObjectiveTile(FloorTile tile)
    {
        if (tile != null && tile.IsObjective && !tile.IsCompleted && incompleteObjectives.Add(tile))
        {
            objectiveTilesRemaining = incompleteObjectives.Count;
        }
    }

    public void ObjectiveTileCompleted(FloorTile tile)
    {
        if (tile == null || !incompleteObjectives.Remove(tile))
        {
            return;
        }

        objectiveTilesRemaining = incompleteObjectives.Count;
        Debug.Log($"Objective tiles remaining: {objectiveTilesRemaining}", this);

        if (objectiveTilesRemaining == 0)
        {
            WaveComplete();
        }
    }

    // Kept so existing scene events do not break while old prototype scenes are migrated.
    public void GreenTileCompleted()
    {
        objectiveTilesRemaining = Mathf.Max(0, objectiveTilesRemaining - 1);
        Debug.Log($"Objective tiles remaining: {objectiveTilesRemaining}", this);

        if (objectiveTilesRemaining == 0)
        {
            WaveComplete();
        }
    }

    public void RecountObjectives()
    {
        IsLevelComplete = false;
        incompleteObjectives.Clear();

        FloorTile[] tiles = FindObjectsByType<FloorTile>(FindObjectsInactive.Exclude);
        foreach (FloorTile tile in tiles)
        {
            RegisterObjectiveTile(tile);
        }

        objectiveTilesRemaining = incompleteObjectives.Count;
    }

    private void WaveComplete()
    {
        if (IsLevelComplete)
        {
            return;
        }

        ThreeWaveController waveController = FindAnyObjectByType<ThreeWaveController>();
        if (waveController != null && waveController.TryAdvanceWave())
        {
            return;
        }

        IsLevelComplete = true;
        PlayerLife life = FindAnyObjectByType<PlayerLife>();
        bool perfect = life == null || life.CurrentLives == life.MaxLives;
        BestTimeRecord.SaveCompletion(SceneManager.GetActiveScene().name, Time.time - levelStartedAt, perfect);
        Debug.Log("All objective tiles completed. Level complete!", this);
        gameplayUI?.ShowLevelComplete();
    }

    public void SetWave(int wave, int totalWaves)
    {
        gameplayUI?.SetWave(wave, totalWaves);
    }

    public void RegisterUI(GameplayUIController uiController)
    {
        gameplayUI = uiController;
    }

    public void PlayerDefeated()
    {
        if (!IsLevelComplete)
        {
            gameplayUI?.ShowGameOver();
        }
    }
}
