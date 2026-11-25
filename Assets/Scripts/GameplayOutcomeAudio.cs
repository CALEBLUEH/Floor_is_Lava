using UnityEngine;

[DisallowMultipleComponent]
public sealed class GameplayOutcomeAudio : MonoBehaviour
{
    private static GameplayOutcomeAudio instance;
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip levelCompleteClip;
    [SerializeField] private AudioClip gameOverClip;

    private void Awake()
    {
        instance = this;
    }

    public static void PlayLevelComplete() => instance?.Play(instance.levelCompleteClip);
    public static void PlayGameOver() => instance?.Play(instance.gameOverClip);

    private void Play(AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.Stop();
        source.PlayOneShot(clip, GameSettings.SfxVolume);
    }

    private void OnDestroy() { if (instance == this) instance = null; }

#if UNITY_EDITOR
    public void ConfigureAuthoredAudio(AudioSource authoredSource, AudioClip complete, AudioClip gameOver)
    {
        source = authoredSource;
        levelCompleteClip = complete;
        gameOverClip = gameOver;
    }
#endif
}
