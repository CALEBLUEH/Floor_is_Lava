using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TutorialTvScreenPresenter : MonoBehaviour
{
    [SerializeField] private RawImage staticImage;
    [SerializeField] private CanvasGroup staticGroup;
    [SerializeField] private CanvasGroup textGroup;
    [SerializeField] private TMP_Text screenText;
    [SerializeField, Min(0.02f)] private float noiseRefreshInterval = 0.06f;

    private Texture2D noiseTexture;
    private Color32[] noisePixels;
    private float nextNoiseRefresh;

    public string CurrentText => screenText != null ? screenText.text : string.Empty;

    private void Awake()
    {
        CreateNoiseTexture();
        SetStaticVisible(false);
    }

    private void Update()
    {
        if (staticGroup == null || staticGroup.alpha <= 0.01f || Time.unscaledTime < nextNoiseRefresh)
        {
            return;
        }

        nextNoiseRefresh = Time.unscaledTime + noiseRefreshInterval;
        RefreshNoise();
    }

    public void Clear()
    {
        SetTextImmediate(string.Empty);
    }

    public void SetTextImmediate(string value)
    {
        if (screenText != null)
        {
            screenText.text = value ?? string.Empty;
        }

        SetStaticVisible(false);
    }

    public IEnumerator TransitionTo(string value, float noSignalDuration)
    {
        SetStaticVisible(true);
        float remaining = Mathf.Max(0f, noSignalDuration);
        while (remaining > 0f)
        {
            if (!PauseMenuController.IsGamePaused && Time.timeScale > 0f)
                remaining -= Time.unscaledDeltaTime;
            yield return null;
        }

        SetTextImmediate(value);
    }

    private void SetStaticVisible(bool visible)
    {
        if (staticGroup != null)
        {
            staticGroup.alpha = visible ? 1f : 0f;
        }

        if (textGroup != null)
        {
            textGroup.alpha = visible ? 0f : 1f;
        }
    }

    private void CreateNoiseTexture()
    {
        if (staticImage == null || noiseTexture != null)
        {
            return;
        }

        noiseTexture = new Texture2D(96, 54, TextureFormat.RGB24, false)
        {
            name = "Tutorial TV Static",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        noisePixels = new Color32[noiseTexture.width * noiseTexture.height];
        staticImage.texture = noiseTexture;
        staticImage.color = Color.white;
        RefreshNoise();
    }

    private void RefreshNoise()
    {
        if (noiseTexture == null || noisePixels == null)
        {
            return;
        }

        for (int y = 0; y < noiseTexture.height; y++)
        {
            byte line = (byte)Random.Range(22, 210);
            for (int x = 0; x < noiseTexture.width; x++)
            {
                byte value = (byte)Mathf.Clamp(line + Random.Range(-32, 33), 0, 255);
                noisePixels[y * noiseTexture.width + x] = new Color32(value, value, value, 255);
            }
        }

        noiseTexture.SetPixels32(noisePixels);
        noiseTexture.Apply(false);
    }

    private void OnDestroy()
    {
        if (noiseTexture != null)
        {
            Destroy(noiseTexture);
        }
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(
        RawImage noiseImage,
        CanvasGroup noiseGroup,
        CanvasGroup titleGroup,
        TMP_Text titleText)
    {
        staticImage = noiseImage;
        staticGroup = noiseGroup;
        textGroup = titleGroup;
        screenText = titleText;
    }
#endif
}
