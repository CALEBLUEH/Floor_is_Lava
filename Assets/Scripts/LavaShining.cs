using UnityEngine;

public class LavaShining : MonoBehaviour
{
    public Renderer lavaRenderer;

    public float minGlow = 1f;
    public float maxGlow = 5f;
    public float speed = 2f;

    private Material lavaMaterial;

    private void Awake()
    {
        if (lavaRenderer == null) lavaRenderer = GetComponent<Renderer>();
        if (lavaRenderer == null) return;
        lavaMaterial = lavaRenderer.material;
        lavaMaterial.EnableKeyword("_EMISSION");
        lavaMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    void Update()
    {
        if (lavaMaterial == null) return;
        float glow = Mathf.Lerp(
            minGlow,
            maxGlow,
            (Mathf.Sin(Time.time * speed) + 1f) / 2f
        );

        Color lavaColor = new Color(1f, 0.25f, 0.02f);
        Color emission = lavaColor * (glow * 0.35f);
        lavaMaterial.SetColor("_EmissionColor", emission);
        DynamicGI.SetEmissive(lavaRenderer, emission);
    }
}
