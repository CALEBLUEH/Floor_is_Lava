using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class HorrorCeilingLight : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float normalIntensity = 2.8f;
    [SerializeField, Min(0f)] private float darkIntensity = .03f;
    [SerializeField] private Vector2 calmDelay = new(1.35f, 3.8f);
    [SerializeField] private Vector2 flickerDuration = new(.045f, .13f);
    [SerializeField, Range(2, 7)] private int maximumFlickers = 5;
    [SerializeField] private Renderer glowRenderer;

    private Light ceilingLight;
    private Coroutine routine;

    private void Awake()
    {
        ceilingLight = GetComponent<Light>();
        normalIntensity = ceilingLight.intensity;
    }

    private void OnEnable()
    {
        ceilingLight ??= GetComponent<Light>();
        routine = StartCoroutine(FlickerLoop());
    }

    private void OnDisable()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        if (ceilingLight != null)
        {
            ceilingLight.enabled = true;
            ceilingLight.intensity = normalIntensity;
        }
    }

    private IEnumerator FlickerLoop()
    {
        while (true)
        {
            ceilingLight.enabled = true;
            ceilingLight.intensity = normalIntensity;
            UpdateGlow(1f);
            yield return new WaitForSeconds(Random.Range(calmDelay.x, calmDelay.y));
            int count = Random.Range(2, maximumFlickers + 1);
            for (int i = 0; i < count; i++)
            {
                ceilingLight.intensity = Random.value < .72f
                    ? darkIntensity
                    : normalIntensity * Random.Range(.12f, .38f);
                UpdateGlow(ceilingLight.intensity / Mathf.Max(.01f, normalIntensity));
                yield return new WaitForSeconds(Random.Range(flickerDuration.x, flickerDuration.y));
                ceilingLight.intensity = normalIntensity * Random.Range(.72f, 1.08f);
                UpdateGlow(ceilingLight.intensity / Mathf.Max(.01f, normalIntensity));
                yield return new WaitForSeconds(Random.Range(.04f, .16f));
            }
        }
    }

    private void UpdateGlow(float amount)
    {
        if (glowRenderer == null) return;
        Color glow = ceilingLight.color * Mathf.Lerp(.02f, 4.5f, Mathf.Clamp01(amount));
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        glowRenderer.GetPropertyBlock(block);
        block.SetColor("_EmissionColor", glow);
        block.SetColor("_Color", Color.Lerp(Color.black, ceilingLight.color, Mathf.Clamp01(amount)));
        glowRenderer.SetPropertyBlock(block);
    }

#if UNITY_EDITOR
    public void ConfigureAuthoredGlow(Renderer authoredGlow) => glowRenderer = authoredGlow;
#endif
}
