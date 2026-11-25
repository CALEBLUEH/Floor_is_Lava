using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneAudioDirector : MonoBehaviour
{
    private static SceneAudioDirector instance;
    [SerializeField] private AudioSource music;
    [SerializeField] private AudioSource effects;
    [SerializeField] private AudioClip objectiveTrigger;
    [SerializeField] private AudioClip playerShoot;

    public static string CurrentMusicClipName => instance != null && instance.music != null && instance.music.clip != null
        ? instance.music.clip.name
        : null;
    public static string LastEffectClipName { get; private set; }

    private void Awake()
    {
        instance = this;
        if (objectiveTrigger == null) objectiveTrigger = Resources.Load<AudioClip>("SceneAudio/ObjectiveTrigger");
        if (playerShoot == null) playerShoot = Resources.Load<AudioClip>("SceneAudio/PlayerShoot");
        if (effects != null) effects.ignoreListenerPause = true;
        ApplyVolumes();
    }

    private void Start()
    {
        PlayFor(SceneManager.GetActiveScene().name);
    }

    private void Update()
    {
        ApplyVolumes();
    }

    public static void PlayEffect(AudioClip clip)
    {
        if (instance == null || instance.effects == null || clip == null) return;
        LastEffectClipName = clip.name;
        instance.effects.PlayOneShot(clip);
    }

    public static void PlayObjectiveTrigger() => PlayEffect(instance != null ? instance.objectiveTrigger : null);

    public static void PlayPlayerShot() => PlayEffect(instance != null ? instance.playerShoot : null);

    private void ApplyVolumes()
    {
        if (music != null) music.volume = 0.42f * GameSettings.BgmVolume;
        if (effects != null) effects.volume = GameSettings.SfxVolume;
    }

    private void PlayFor(string sceneName)
    {
        string clipName;
        switch (sceneName)
        {
            case "Interface_MainMenu":
            case "Interface_LevelSelection": clipName = "MenuSelection"; break;
            case "Level_Laser": clipName = "Laser"; break;
            case "Level_MemoryGrid": clipName = "Memory"; break;
            case "Level_Pong": clipName = "AirHockey"; break;
            case "Level_Tutorial": clipName = "YourBestFriend"; break;
            case "Level_Survival": clipName = "ForsakenCity"; break;
            case "Level_Boss": clipName = "VirtualStormHardArrange"; break;
            default: clipName = null; break;
        }

        AudioClip next = clipName != null ? Resources.Load<AudioClip>("SceneAudio/" + clipName) : null;
        if (music == null || music.clip == next && music.isPlaying) return;
        music.Stop();
        music.clip = next;
        if (next != null) music.Play();
    }

    private void OnDestroy() { if (instance == this) instance = null; }

#if UNITY_EDITOR
    public void ConfigureAuthoredSources(AudioSource musicSource, AudioSource effectsSource)
    {
        music = musicSource;
        effects = effectsSource;
        objectiveTrigger = Resources.Load<AudioClip>("SceneAudio/ObjectiveTrigger");
        playerShoot = Resources.Load<AudioClip>("SceneAudio/PlayerShoot");
    }
#endif
}
