using UnityEngine;

public static class GameSettings
{
    private const string BgmKey = "ACG.Audio.BGM";
    private const string SfxKey = "ACG.Audio.SFX";

    public static float BgmVolume
    {
        get => PlayerPrefs.GetFloat(BgmKey, 1f);
        set { PlayerPrefs.SetFloat(BgmKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static float SfxVolume
    {
        get => PlayerPrefs.GetFloat(SfxKey, 1f);
        set { PlayerPrefs.SetFloat(SfxKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); }
    }

    public static void ResetAudio()
    {
        PlayerPrefs.DeleteKey(BgmKey);
        PlayerPrefs.DeleteKey(SfxKey);
        PlayerPrefs.Save();
    }
}
