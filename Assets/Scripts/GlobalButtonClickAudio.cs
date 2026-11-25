using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GlobalButtonClickAudio : MonoBehaviour
{
    [SerializeField] private AudioSource source;
    [SerializeField] private AudioClip clickClip;

    private void Awake()
    {
        if (source != null) source.ignoreListenerPause = true;
    }

    public void PlayClick()
    {
        if (source == null || clickClip == null) return;
        source.PlayOneShot(clickClip, GameSettings.SfxVolume);
    }

#if UNITY_EDITOR
    public void ConfigureAuthoredAudio(AudioSource authoredSource, AudioClip authoredClip)
    {
        source = authoredSource;
        clickClip = authoredClip;
    }
#endif
}
