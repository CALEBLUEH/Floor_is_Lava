using UnityEngine;

[DisallowMultipleComponent]
public sealed class LaserWallLedDecorator : MonoBehaviour
{
    [SerializeField] private Renderer[] segments;
    [SerializeField] private Light[] roamingLights;
    [SerializeField, Min(.1f)] private float animationSpeed = 1.5f;
    [SerializeField, Min(.1f)] private float emissionStrength = 5f;

    private Vector3[] segmentPositions;
    private MaterialPropertyBlock propertyBlock;

    private void Awake()
    {
        segments ??= System.Array.Empty<Renderer>();
        roamingLights ??= System.Array.Empty<Light>();
        segmentPositions = new Vector3[segments.Length];
        for (int i = 0; i < segments.Length; i++)
            if (segments[i] != null) segmentPositions[i] = segments[i].transform.position;
    }

    private void Update()
    {
        if (segments.Length == 0) return;
        propertyBlock ??= new MaterialPropertyBlock();
        float time = Time.time * animationSpeed;
        int pattern = Mathf.FloorToInt(time / 8f) % 3;
        for (int i = 0; i < segments.Length; i++)
        {
            if (segments[i] == null) continue;
            float phase = i / (float)segments.Length;
            float brightness;
            if (pattern == 0)
                brightness = Mathf.Pow(Mathf.Max(0f, Mathf.Cos((phase - time * .08f) * Mathf.PI * 10f)), 5f);
            else if (pattern == 1)
                brightness = .5f + Mathf.Sin(time * 2.1f + i * .72f) * .5f;
            else
            {
                bool darkBeat = Mathf.Repeat(time, 3.5f) > 2.85f;
                brightness = darkBeat ? .025f : (i % 2 == Mathf.FloorToInt(time * 2f) % 2 ? 1f : .14f);
            }

            Color color = Color.Lerp(new Color(.02f, .65f, 1f), new Color(1f, .03f, .58f),
                .5f + Mathf.Sin(time * .55f + phase * Mathf.PI * 2f) * .5f);
            segments[i].GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", color);
            propertyBlock.SetColor("_BaseColor", color);
            propertyBlock.SetColor("_EmissionColor", color * Mathf.Lerp(.18f, emissionStrength, brightness));
            segments[i].SetPropertyBlock(propertyBlock);
        }

        for (int i = 0; i < roamingLights.Length; i++)
        {
            Light lightSource = roamingLights[i];
            if (lightSource == null) continue;
            float phase = Mathf.Repeat(time * .045f + i / (float)roamingLights.Length, 1f);
            int index = Mathf.Min(segments.Length - 1, Mathf.FloorToInt(phase * segments.Length));
            lightSource.transform.position = segmentPositions[index] + Vector3.up * .12f;
            lightSource.color = i % 2 == 0 ? new Color(.05f, .7f, 1f) : new Color(1f, .04f, .55f);
            lightSource.intensity = 2.2f + Mathf.Sin(time * 3f + i) * .8f;
        }
    }

#if UNITY_EDITOR
    public void ConfigureAuthoredContent(Renderer[] authoredSegments, Light[] authoredLights)
    {
        segments = authoredSegments;
        roamingLights = authoredLights;
    }
#endif
}
