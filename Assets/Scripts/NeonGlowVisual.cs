using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class NeonGlowVisual : MonoBehaviour
{
    [SerializeField] private Renderer sourceRenderer;
    [SerializeField] private Renderer glowShellRenderer;
    [SerializeField] private Material nonGlowingMaterial;
    [SerializeField, Range(0.1f, 3f)] private float shellBrightness = 0.9f;

    private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
    private MaterialPropertyBlock propertyBlock;
    private Material lastSourceMaterial;
    private Renderer[] gameplayRenderers;

    private void OnEnable()
    {
        ApplyVisual(true);
    }

    private void LateUpdate()
    {
        ApplyVisual(false);
    }

    private void OnValidate()
    {
        ApplyVisual(true);
    }

    private void ApplyVisual(bool force)
    {
        string sceneName = gameObject.scene.name;
        if (sceneName == "Level_MemoryGrid" ||
            sceneName == "Level_Pong" ||
            sceneName == "Level_Laser")
        {
            SuppressGameplayGlowShells();
            if (sceneName == "Level_Pong") SuppressPongGoalRowVisual();
            return;
        }

        if (sourceRenderer == null || glowShellRenderer == null)
        {
            return;
        }

        Material sourceMaterial = sourceRenderer.sharedMaterial;
        if (!force && sourceMaterial == lastSourceMaterial)
        {
            return;
        }
        lastSourceMaterial = sourceMaterial;

        bool shouldGlow = sourceMaterial != null && sourceMaterial != nonGlowingMaterial &&
                          sourceMaterial.IsKeywordEnabled("_EMISSION") &&
                          sourceMaterial.HasProperty("_EmissionColor");
        Color emission = shouldGlow ? sourceMaterial.GetColor("_EmissionColor") : Color.black;
        shouldGlow &= Mathf.Max(emission.r, emission.g, emission.b) > 0.02f;
        glowShellRenderer.enabled = shouldGlow;
        if (!shouldGlow)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        glowShellRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(GlowColorId, emission * shellBrightness);
        glowShellRenderer.SetPropertyBlock(propertyBlock);
    }

    private void SuppressGameplayGlowShells()
    {
        gameplayRenderers ??= GetComponentsInChildren<Renderer>(true);
        foreach (Renderer candidate in gameplayRenderers)
        {
            if (candidate != null && candidate.gameObject.name == "Neon Glow Shell")
                candidate.enabled = false;
        }
    }

    private void SuppressPongGoalRowVisual()
    {
        string[] parts = gameObject.name.Split('_');
        if (parts.Length < 3 || !int.TryParse(parts[parts.Length - 1], out int row)) return;
        if (row >= 2 && row <= 9) return;
        if (sourceRenderer != null) sourceRenderer.enabled = false;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(Renderer source, Renderer shell, Material defaultMaterial)
    {
        sourceRenderer = source;
        glowShellRenderer = shell;
        nonGlowingMaterial = defaultMaterial;
        ApplyVisual(true);
    }
#endif
}
