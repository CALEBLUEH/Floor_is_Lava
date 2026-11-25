using UnityEngine;

public static class BestTimeRecord
{
    public const float SurvivalClearSeconds = 5f * 60f;
    public const float SurvivalPerfectSeconds = 10f * 60f;

    private const string KeyPrefix = "ACG.BestTime.";
    private const string PerfectKeyPrefix = "ACG.BestTime.Perfect.";
    private const string CompletedKeyPrefix = "ACG.Stage.Completed.";

    public static void SaveCompletion(string sceneName, float seconds)
    {
        SaveCompletion(sceneName, seconds, false);
    }

    public static void SaveCompletion(string sceneName, float seconds, bool perfect)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        if (sceneName == "Level_Survival")
        {
            SaveSurvivalResult(seconds);
            return;
        }
        SaveCompletionStatus(sceneName, perfect);
        if (sceneName == "Level_Tutorial" || seconds <= 0f) return;
        string key = KeyPrefix + sceneName;
        if (!PlayerPrefs.HasKey(key) || seconds < PlayerPrefs.GetFloat(key))
        {
            PlayerPrefs.SetFloat(key, seconds);
            PlayerPrefs.Save();
        }
    }

    public static void SaveSurvivalResult(float seconds)
    {
        if (seconds <= 0f) return;
        const string sceneName = "Level_Survival";
        string key = KeyPrefix + sceneName;
        if (!PlayerPrefs.HasKey(key) || seconds > PlayerPrefs.GetFloat(key))
            PlayerPrefs.SetFloat(key, seconds);
        if (seconds >= SurvivalClearSeconds)
            PlayerPrefs.SetInt(CompletedKeyPrefix + sceneName, 1);
        if (seconds >= SurvivalPerfectSeconds)
            PlayerPrefs.SetInt(PerfectKeyPrefix + sceneName, 1);
        else if (!PlayerPrefs.HasKey(PerfectKeyPrefix + sceneName))
            PlayerPrefs.SetInt(PerfectKeyPrefix + sceneName, 0);
        PlayerPrefs.Save();
    }

    public static void SaveCompletionStatus(string sceneName, bool perfect)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        PlayerPrefs.SetInt(CompletedKeyPrefix + sceneName, 1);
        if (perfect) PlayerPrefs.SetInt(PerfectKeyPrefix + sceneName, 1);
        else if (!PlayerPrefs.HasKey(PerfectKeyPrefix + sceneName))
            PlayerPrefs.SetInt(PerfectKeyPrefix + sceneName, 0);
        PlayerPrefs.Save();
    }

    public static bool HasCompleted(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return false;
        if (sceneName == "Level_Survival")
            return PlayerPrefs.GetInt(CompletedKeyPrefix + sceneName, 0) == 1 ||
                   TryGet(sceneName, out float survivalSeconds) && survivalSeconds >= SurvivalClearSeconds;
        return PlayerPrefs.GetInt(CompletedKeyPrefix + sceneName, 0) == 1 ||
            PlayerPrefs.HasKey(KeyPrefix + sceneName);
    }

    public static bool IsPerfect(string sceneName)
    {
        if (sceneName == "Level_Survival")
            return HasCompleted(sceneName) &&
                   (PlayerPrefs.GetInt(PerfectKeyPrefix + sceneName, 0) == 1 ||
                    TryGet(sceneName, out float survivalSeconds) && survivalSeconds >= SurvivalPerfectSeconds);
        return HasCompleted(sceneName) && PlayerPrefs.GetInt(PerfectKeyPrefix + sceneName, 0) == 1;
    }

    public static bool TryGet(string sceneName, out float seconds, out bool perfect)
    {
        bool found = TryGet(sceneName, out seconds);
        perfect = found && IsPerfect(sceneName);
        return found;
    }

    public static bool TryGet(string sceneName, out float seconds)
    {
        string key = KeyPrefix + sceneName;
        if (PlayerPrefs.HasKey(key))
        {
            seconds = PlayerPrefs.GetFloat(key);
            return true;
        }
        seconds = 0f;
        return false;
    }

    public static string Format(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        float remaining = seconds - minutes * 60f;
        return $"{minutes:00}:{remaining:00.00}";
    }

    public static void ResetAll()
    {
        string[] scenes =
        {
            "Level_Tutorial", "Level_Survival", "Level_Boss", "Level_Laser", "Level_MemoryGrid",
            "Level_Pong", "Level_Maze"
        };
        foreach (string scene in scenes)
        {
            PlayerPrefs.DeleteKey(KeyPrefix + scene);
            PlayerPrefs.DeleteKey(PerfectKeyPrefix + scene);
            PlayerPrefs.DeleteKey(CompletedKeyPrefix + scene);
        }
        PlayerPrefs.Save();
    }
}
