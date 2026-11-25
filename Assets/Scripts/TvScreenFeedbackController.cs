using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TvScreenFeedbackController : MonoBehaviour
{
    [SerializeField] private RawImage staticImage;
    [SerializeField] private CanvasGroup staticGroup;
    [SerializeField] private CanvasGroup titleGroup;
    [SerializeField] private TMP_Text titleText;
    private Texture2D noise;
    private Sequence sequence;
    private float nextNoiseUpdate;

    private void Start()
    {
        noise = new Texture2D(96, 54, TextureFormat.RGB24, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        staticImage.texture = noise;
        staticImage.color = Color.white;
        titleText.text = "FLOOR IS LAVA";
        titleGroup.alpha = 0;
        sequence = DOTween.Sequence().SetLoops(-1).SetUpdate(true)
            .Append(Fade(staticGroup, 1f, .08f)).AppendInterval(1.2f)
            .Append(Fade(staticGroup, 0f, .22f))
            .Join(Fade(titleGroup, 1f, .3f)).AppendInterval(2.6f)
            .Append(Fade(titleGroup, 0f, .25f)).Join(Fade(staticGroup, 1f, .08f));
        RectTransform titleRect = titleText.rectTransform;
        DOTween.To(() => titleRect.anchoredPosition.y, y => titleRect.anchoredPosition = new Vector2(titleRect.anchoredPosition.x, y), titleRect.anchoredPosition.y + 8f, 1f).SetLink(gameObject, LinkBehaviour.KillOnDestroy).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo).SetUpdate(true);
    }

    private void Update()
    {
        if (noise == null || Time.unscaledTime < nextNoiseUpdate) return;
        nextNoiseUpdate = Time.unscaledTime + .06f;
        Color32[] pixels = new Color32[noise.width * noise.height];
        for (int y = 0; y < noise.height; y++)
        {
            byte line = (byte)Random.Range(18, 205);
            for (int x = 0; x < noise.width; x++)
            {
                byte jitter = (byte)Mathf.Clamp(line + Random.Range(-28, 29), 0, 255);
                pixels[y * noise.width + x] = new Color32(jitter, jitter, jitter, 255);
            }
        }
        noise.SetPixels32(pixels); noise.Apply(false);
    }

    private void OnDestroy()
    {
        sequence?.Kill();
        if (noise != null) Destroy(noise);
    }

    private static Tweener Fade(CanvasGroup group, float target, float duration)
    {
        return DOTween.To(() => group.alpha, value => group.alpha = value, target, duration).SetUpdate(true);
    }

#if UNITY_EDITOR
    public void Configure(RawImage image, CanvasGroup noiseGroup, CanvasGroup gameTitleGroup, TMP_Text gameTitle)
    { staticImage = image; staticGroup = noiseGroup; titleGroup = gameTitleGroup; titleText = gameTitle; }
#endif
}
