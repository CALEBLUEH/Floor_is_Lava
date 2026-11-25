using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Cinemachine;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossArenaExpansionController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private DarkRedBossController darkRedBoss;
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private BossArenaBoundary arenaBoundary;
    [SerializeField] private CinemachineBasicMultiChannelPerlin cameraNoise;
    [SerializeField] private PinkBossController pinkBoss;
    [SerializeField] private FloorTile[] expansionTiles;

    [Header("20 x 20 transition")]
    [SerializeField, Min(0f)] private float delayBeforeShake = 1.25f;
    [SerializeField, Min(.05f)] private float shakeDuration = .8f;
    [SerializeField, Min(0f)] private float shakeAmplitude = 2.2f;
    [SerializeField, Min(.1f)] private float expansionDuration = 5f;
    [SerializeField, Min(.05f)] private float tileRiseDuration = 1.15f;
    [SerializeField, Min(1f)] private float tileStartDepth = 14f;
    [SerializeField] private AnimationCurve riseCurve = new(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(.72f, 1.08f),
        new Keyframe(1f, 1f));

    public bool ExpansionComplete { get; private set; }

    private void Awake()
    {
        PrepareExpansionTiles();
        if (cameraNoise != null) cameraNoise.AmplitudeGain = 0f;
    }

    private IEnumerator Start()
    {
        // All tile visual controllers have completed Awake before this point, so a
        // second preparation pass also hides any renderers they created at runtime.
        PrepareExpansionTiles();
        while (darkRedBoss != null && !darkRedBoss.IsDefeated) yield return null;
        if (delayBeforeShake > 0f) yield return new WaitForSeconds(delayBeforeShake);
        yield return ShakeCamera();
        yield return RaiseExpansionTiles();

        foreach (FloorTile tile in expansionTiles)
        {
            if (tile == null) continue;
            tile.transform.SetParent(tileGrid.transform, true);
            foreach (Collider collider in tile.GetComponentsInChildren<Collider>(true)) collider.enabled = true;
        }
        tileGrid.ConfigureRuntimeDimensions(20, 20);
        arenaBoundary?.RefreshBoundary();
        ExpansionComplete = true;
        pinkBoss?.BeginEntrance();
    }

    private void PrepareExpansionTiles()
    {
        if (tileGrid == null || expansionTiles == null) return;
        for (int index = 0; index < expansionTiles.Length; index++)
        {
            FloorTile tile = expansionTiles[index];
            if (tile == null) continue;
            Vector3 target = GetTargetWorld(index);
            tile.transform.position = target - Vector3.up * tileStartDepth;
            foreach (Collider collider in tile.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (Renderer renderer in tile.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (Light light in tile.GetComponentsInChildren<Light>(true)) light.enabled = false;
            foreach (ParticleSystem particles in tile.GetComponentsInChildren<ParticleSystem>(true))
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private IEnumerator ShakeCamera()
    {
        if (cameraNoise == null || shakeDuration <= 0f) yield break;
        cameraNoise.AmplitudeGain = shakeAmplitude;
        cameraNoise.FrequencyGain = 1.5f;
        yield return new WaitForSeconds(shakeDuration);
        cameraNoise.AmplitudeGain = 0f;
    }

    private IEnumerator RaiseExpansionTiles()
    {
        if (expansionTiles == null || expansionTiles.Length == 0) yield break;
        List<int> order = new(expansionTiles.Length);
        for (int index = 0; index < expansionTiles.Length; index++) order.Add(index);
        for (int index = order.Count - 1; index > 0; index--)
        {
            int swap = Random.Range(0, index + 1);
            (order[index], order[swap]) = (order[swap], order[index]);
        }

        float availableStagger = Mathf.Max(0f, expansionDuration - tileRiseDuration);
        for (int orderIndex = 0; orderIndex < order.Count; orderIndex++)
        {
            float delay = order.Count > 1 ? availableStagger * orderIndex / (order.Count - 1f) : 0f;
            StartCoroutine(RaiseTile(order[orderIndex], delay));
        }
        yield return new WaitForSeconds(expansionDuration);
    }

    private IEnumerator RaiseTile(int index, float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        FloorTile tile = expansionTiles[index];
        if (tile == null) yield break;
        Renderer[] renderers = tile.GetComponentsInChildren<Renderer>(true);
        Renderer primaryRenderer = tile.GetComponent<Renderer>() ?? renderers.FirstOrDefault();
        MaterialPropertyBlock fadeProperties = null;
        Color originalColour = Color.white;
        if (primaryRenderer != null)
        {
            primaryRenderer.enabled = true;
            fadeProperties = new MaterialPropertyBlock();
            Material shared = primaryRenderer.sharedMaterial;
            if (shared != null)
            {
                if (shared.HasProperty("_BaseColor")) originalColour = shared.GetColor("_BaseColor");
                else if (shared.HasProperty("_Color")) originalColour = shared.GetColor("_Color");
            }
        }
        Vector3 target = GetTargetWorld(index);
        Vector3 start = target - Vector3.up * tileStartDepth;
        Vector3 targetScale = tile.transform.localScale;
        tile.transform.localScale = targetScale * .02f;
        float elapsed = 0f;
        float duration = Mathf.Max(.05f, tileRiseDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = riseCurve.Evaluate(t);
            tile.transform.position = Vector3.LerpUnclamped(start, target, eased);
            float alpha = Mathf.SmoothStep(0f, 1f, t);
            tile.transform.localScale = Vector3.Lerp(targetScale * .02f, targetScale, alpha);
            if (primaryRenderer != null)
            {
                Color faded = originalColour * alpha;
                faded.a = alpha;
                fadeProperties.SetColor("_Color", faded);
                fadeProperties.SetColor("_BaseColor", faded);
                primaryRenderer.SetPropertyBlock(fadeProperties);
            }
            yield return null;
        }
        tile.transform.position = target;
        tile.transform.localScale = targetScale;
        if (primaryRenderer != null) primaryRenderer.SetPropertyBlock(null);
        foreach (Renderer renderer in renderers) renderer.enabled = true;
        foreach (Light light in tile.GetComponentsInChildren<Light>(true)) light.enabled = true;
        foreach (ParticleSystem particles in tile.GetComponentsInChildren<ParticleSystem>(true)) particles.Play(true);
    }

    private Vector3 GetTargetWorld(int index)
    {
        int x = index % 20;
        int y = 10 + index / 20;
        return tileGrid.transform.TransformPoint(new Vector3(x * tileGrid.CellSize, 0f, y * tileGrid.CellSize));
    }

    private void OnDisable()
    {
        if (cameraNoise != null) cameraNoise.AmplitudeGain = 0f;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(DarkRedBossController darkRed, TileGrid grid, BossArenaBoundary boundary,
        CinemachineBasicMultiChannelPerlin noise, PinkBossController pink, FloorTile[] tiles)
    {
        darkRedBoss = darkRed;
        tileGrid = grid;
        arenaBoundary = boundary;
        cameraNoise = noise;
        pinkBoss = pink;
        expansionTiles = tiles;
    }

    public void ConfigureFastTestTimings()
    {
        delayBeforeShake = .05f;
        shakeDuration = .05f;
        expansionDuration = .6f;
        tileRiseDuration = .2f;
    }
#endif
}
