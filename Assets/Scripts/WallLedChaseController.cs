using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class WallLedChaseController : MonoBehaviour
{
    [SerializeField] private Renderer[] ledSegments;
    [SerializeField] private Color baseColor = new(.02f, .22f, .34f, 1f);
    [SerializeField] private Color chaseColor = new(.04f, .9f, 1f, 1f);
    [SerializeField] private Color horizontalBaseColor = new(.32f, .015f, .18f, 1f);
    [SerializeField] private Color horizontalChaseColor = new(1f, .06f, .62f, 1f);
    [SerializeField, Min(.05f)] private float segmentDuration = .18f;
    [SerializeField, Min(0f)] private float segmentDelay = .035f;
    [SerializeField, Min(0f)] private float emissionStrength = 4f;

    private Material[] materials;
    private Color[] segmentBaseColors;
    private Color[] segmentChaseColors;
    private Sequence chaseSequence;
    private bool memoryGridStyle;

    private void Start()
    {
        if (ledSegments == null || ledSegments.Length == 0) return;
        memoryGridStyle = SceneManager.GetActiveScene().name == "Level_MemoryGrid";
        if (memoryGridStyle)
        {
            emissionStrength = Mathf.Max(emissionStrength, 7f);
        }

        materials = new Material[ledSegments.Length];
        segmentBaseColors = new Color[ledSegments.Length];
        segmentChaseColors = new Color[ledSegments.Length];
        for (int i = 0; i < ledSegments.Length; i++)
        {
            if (ledSegments[i] == null || ledSegments[i].sharedMaterial == null) continue;
            bool horizontal = memoryGridStyle &&
                (ledSegments[i].name.StartsWith("North LED") || ledSegments[i].name.StartsWith("South LED"));
            segmentBaseColors[i] = horizontal ? horizontalBaseColor : baseColor;
            segmentChaseColors[i] = horizontal ? horizontalChaseColor : chaseColor;
            materials[i] = new Material(ledSegments[i].sharedMaterial);
            materials[i].name = "Animated Wall LED";
            materials[i].EnableKeyword("_EMISSION");
            ledSegments[i].sharedMaterial = materials[i];
            SetMaterialColor(materials[i], segmentBaseColors[i], memoryGridStyle ? 1.2f : .45f);
        }
        BuildSequence();
    }

    private void BuildSequence()
    {
        chaseSequence?.Kill();
        chaseSequence = DOTween.Sequence().SetLoops(-1, LoopType.Restart).SetUpdate(true);
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null) continue;
            Color fromColor = segmentBaseColors[i];
            Color toColor = segmentChaseColors[i];
            float idleEmission = memoryGridStyle ? 1.2f : .45f;
            chaseSequence.Insert(i * segmentDelay,
                DOVirtual.Float(0f, 1f, segmentDuration, value =>
                    SetMaterialColor(material, Color.Lerp(fromColor, toColor, value), Mathf.Lerp(idleEmission, emissionStrength, value)))
                    .SetEase(Ease.OutQuad));
            chaseSequence.Insert(i * segmentDelay + segmentDuration,
                DOVirtual.Float(0f, 1f, segmentDuration, value =>
                    SetMaterialColor(material, Color.Lerp(toColor, fromColor, value), Mathf.Lerp(emissionStrength, idleEmission, value)))
                    .SetEase(Ease.InQuad));
        }
        float cycle = materials.Length * segmentDelay + segmentDuration * 2f;
        chaseSequence.AppendInterval(Mathf.Max(.05f, cycle - chaseSequence.Duration()));
    }

    private static void SetMaterialColor(Material material, Color color, float emission)
    {
        if (material.HasProperty("_Color")) material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * emission);
    }

    private void OnDestroy()
    {
        chaseSequence?.Kill();
        if (materials == null) return;
        foreach (Material material in materials) if (material != null) Destroy(material);
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(Renderer[] segments) => ledSegments = segments;
#endif
}
