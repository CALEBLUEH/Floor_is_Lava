using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(-800)]
public sealed class MemoryGridSpaceEnvironment : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private MemoryGridCameraMotion cameraMotion;
    [SerializeField] private MemoryGridAsteroidDirector asteroidDirector;
    private AmbientMode previousAmbientMode;
    private Color previousSkyColor;
    private Color previousEquatorColor;
    private Color previousGroundColor;
    private float previousAmbientIntensity;
    private float previousReflectionIntensity;
    private Coroutine gridShake;
    private Vector3 gridRestPosition;

    private void Start()
    {
        previousAmbientMode = RenderSettings.ambientMode;
        previousSkyColor = RenderSettings.ambientSkyColor;
        previousEquatorColor = RenderSettings.ambientEquatorColor;
        previousGroundColor = RenderSettings.ambientGroundColor;
        previousAmbientIntensity = RenderSettings.ambientIntensity;
        previousReflectionIntensity = RenderSettings.reflectionIntensity;

        // SceneSkybox runs first. Finish lighting and bloom before gameplay Start
        // begins its first preview, avoiding a one-frame startup flash.
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.018f, .045f, .075f);
        RenderSettings.ambientEquatorColor = new Color(.008f, .022f, .04f);
        RenderSettings.ambientGroundColor = new Color(.003f, .008f, .016f);
        RenderSettings.ambientIntensity = .48f;
        RenderSettings.reflectionIntensity = .18f;
        DynamicGI.UpdateEnvironment();

        if (grid != null) gridRestPosition = grid.transform.localPosition;
        asteroidDirector?.Initialize(this, grid);
    }

    public void PlayImpact(Vector3 worldPoint)
    {
        cameraMotion?.AddImpact(.8f);
        if (grid != null)
        {
            if (gridShake != null) StopCoroutine(gridShake);
            grid.transform.localPosition = gridRestPosition;
            gridShake = StartCoroutine(ShakeGrid());
        }
    }

    private IEnumerator ShakeGrid()
    {
        const float duration = .42f;
        float elapsed = 0f;
        while (elapsed < duration && grid != null)
        {
            elapsed += Time.deltaTime;
            float strength = (1f - elapsed / duration) * .055f;
            grid.transform.localPosition = gridRestPosition + new Vector3(
                Random.Range(-strength, strength),
                Random.Range(-strength * .45f, strength * .45f),
                Random.Range(-strength, strength));
            yield return null;
        }
        if (grid != null) grid.transform.localPosition = gridRestPosition;
        gridShake = null;
    }

    private void OnDestroy()
    {
        if (grid != null) grid.transform.localPosition = gridRestPosition;
        RenderSettings.ambientMode = previousAmbientMode;
        RenderSettings.ambientSkyColor = previousSkyColor;
        RenderSettings.ambientEquatorColor = previousEquatorColor;
        RenderSettings.ambientGroundColor = previousGroundColor;
        RenderSettings.ambientIntensity = previousAmbientIntensity;
        RenderSettings.reflectionIntensity = previousReflectionIntensity;
    }

#if UNITY_EDITOR
    public void ConfigureAuthoredContent(TileGrid authoredGrid, MemoryGridCameraMotion authoredMotion,
        MemoryGridAsteroidDirector authoredDirector)
    {
        grid = authoredGrid;
        cameraMotion = authoredMotion;
        asteroidDirector = authoredDirector;
    }
#endif
}

[DisallowMultipleComponent]
public sealed class MemoryGridCameraMotion : MonoBehaviour
{
    [SerializeField] private float positionAmount = .12f;
    [SerializeField] private float rotationAmount = .2f;
    [SerializeField] private float driftSpeed = .11f;

    private Vector3 basePosition;
    private Quaternion baseRotation;
    private float impact;
    private float seed;

    private void Awake()
    {
        basePosition = transform.localPosition;
        baseRotation = transform.localRotation;
        seed = Random.Range(10f, 900f);
    }

    public void AddImpact(float amount)
    {
        impact = Mathf.Max(impact, amount);
    }

    private void LateUpdate()
    {
        float time = Time.unscaledTime * driftSpeed;
        Vector3 drift = new Vector3(
            Mathf.PerlinNoise(seed, time) - .5f,
            Mathf.PerlinNoise(seed + 19f, time) - .5f,
            Mathf.PerlinNoise(seed + 43f, time) - .5f) * (positionAmount * 2f);
        Vector3 rotation = new Vector3(
            Mathf.PerlinNoise(seed + 71f, time) - .5f,
            Mathf.PerlinNoise(seed + 97f, time) - .5f,
            0f) * (rotationAmount * 2f);

        if (impact > .001f)
        {
            drift += Random.insideUnitSphere * (impact * .16f);
            rotation += (Vector3)Random.insideUnitCircle * (impact * .7f);
            impact = Mathf.MoveTowards(impact, 0f, Time.unscaledDeltaTime * 2.4f);
        }

        transform.localPosition = basePosition + drift;
        transform.localRotation = baseRotation * Quaternion.Euler(rotation);
    }
}

[DisallowMultipleComponent]
public sealed class MemoryGridAsteroidDirector : MonoBehaviour
{
    private sealed class AsteroidMesh
    {
        public Mesh mesh;
        public Material[] materials;
    }

    private readonly List<AsteroidMesh> meshes = new List<AsteroidMesh>();
    private MemoryGridSpaceEnvironment environment;
    private Bounds arena;
    private Material asteroidMaterial;
    private bool initialized;

    public void Initialize(MemoryGridSpaceEnvironment owner, TileGrid tileGrid)
    {
        if (initialized) return;
        initialized = true;
        environment = owner;
        arena = CalculateArena(tileGrid);
        LoadAsteroidMeshes();
        StartCoroutine(SpawnLoop());
    }

    private void LoadAsteroidMeshes()
    {
        GameObject source = Resources.Load<GameObject>("MemoryGridAsteroids/AsteroidsPackSF");
        if (source == null)
        {
            Debug.LogWarning("Memory Grid asteroid model could not be loaded.", this);
            return;
        }

        asteroidMaterial = BuildMaterial();
        foreach (MeshFilter filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            Renderer sourceRenderer = filter.GetComponent<Renderer>();
            Material[] materials = new Material[Mathf.Max(1, filter.sharedMesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++) materials[i] = asteroidMaterial;
            if (sourceRenderer != null && sourceRenderer.sharedMaterials.Length > 0 && asteroidMaterial == null)
                materials = sourceRenderer.sharedMaterials;
            meshes.Add(new AsteroidMesh { mesh = filter.sharedMesh, materials = materials });
        }
    }

    private Material BuildMaterial()
    {
        Shader shader = Shader.Find("Standard");
        if (shader == null) return null;
        Material material = new Material(shader) { name = "Asteroid Metallic" };
        Texture2D albedo = Resources.Load<Texture2D>("MemoryGridAsteroids/Mat1_Base_Color");
        Texture2D metallic = Resources.Load<Texture2D>("MemoryGridAsteroids/Mat1_Metallic");
        Texture2D occlusion = Resources.Load<Texture2D>("MemoryGridAsteroids/Mat1_Mixed_AO");
        Texture2D normal = Resources.Load<Texture2D>("MemoryGridAsteroids/Mat1_Normal_DirectX");
        if (albedo != null) material.SetTexture("_MainTex", albedo);
        if (metallic != null)
        {
            material.SetTexture("_MetallicGlossMap", metallic);
            material.EnableKeyword("_METALLICGLOSSMAP");
        }
        if (occlusion != null) material.SetTexture("_OcclusionMap", occlusion);
        if (normal != null)
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        material.SetFloat("_Metallic", .82f);
        material.SetFloat("_GlossMapScale", .32f);
        material.SetFloat("_Glossiness", .32f);
        return material;
    }

    private IEnumerator SpawnLoop()
    {
        yield return new WaitForSeconds(1.2f);
        while (true)
        {
            int groupSize = Random.Range(2, 5);
            for (int i = 0; i < groupSize; i++)
            {
                if (meshes.Count > 0) StartCoroutine(FlyAsteroid());
                yield return new WaitForSeconds(Random.Range(.18f, .55f));
            }
            yield return new WaitForSeconds(Random.Range(2.6f, 4.4f));
        }
    }

    private IEnumerator FlyAsteroid()
    {
        AsteroidMesh template = meshes[Random.Range(0, meshes.Count)];
        GameObject asteroid = new GameObject("Flying Asteroid", typeof(MeshFilter), typeof(MeshRenderer));
        asteroid.GetComponent<MeshFilter>().sharedMesh = template.mesh;
        asteroid.GetComponent<MeshRenderer>().sharedMaterials = template.materials;
        asteroid.layer = 0;

        float meshDiameter = Mathf.Max(.01f, template.mesh.bounds.extents.magnitude * 2f);
        float desiredDiameter = Random.Range(1.2f, 3.5f);
        asteroid.transform.localScale = Vector3.one * (desiredDiameter / meshDiameter);
        asteroid.transform.rotation = Random.rotation;

        bool impactRun = Random.value < .24f;
        Vector3 horizontal = Random.insideUnitCircle.normalized;
        Vector3 horizontal3 = new Vector3(horizontal.x, 0f, horizontal.y);
        float radius = Mathf.Max(arena.extents.x, arena.extents.z);
        Vector3 start;
        Vector3 end;
        if (impactRun)
        {
            start = arena.center + horizontal3 * radius * 1.65f + Vector3.up * Random.Range(12f, 19f);
            end = new Vector3(
                Random.Range(arena.min.x + arena.size.x * .12f, arena.max.x - arena.size.x * .12f),
                arena.max.y + .25f,
                Random.Range(arena.min.z + arena.size.z * .12f, arena.max.z - arena.size.z * .12f));
        }
        else
        {
            start = arena.center + horizontal3 * radius * 1.8f + Vector3.up * Random.Range(7f, 14f);
            end = arena.center - horizontal3 * radius * 1.8f + Vector3.up * Random.Range(8f, 16f);
        }

        asteroid.transform.position = start;
        Vector3 spinAxis = Random.onUnitSphere;
        float spinSpeed = Random.Range(45f, 125f);
        float duration = Random.Range(5.5f, 9f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            asteroid.transform.position = Vector3.Lerp(start, end, t);
            asteroid.transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);
            yield return null;
        }

        if (impactRun) environment?.PlayImpact(end);
        Destroy(asteroid);
    }

    private static Bounds CalculateArena(TileGrid grid)
    {
        if (grid == null) return new Bounds(Vector3.zero, new Vector3(24f, 2f, 24f));
        Renderer[] renderers = grid.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return new Bounds(grid.transform.position, new Vector3(24f, 2f, 24f));
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private void OnDestroy()
    {
        if (asteroidMaterial != null) Destroy(asteroidMaterial);
    }
}

[DisallowMultipleComponent]
public sealed class MemoryGridBloom : MonoBehaviour
{
    [SerializeField, Range(.5f, 3f)] private float threshold = 1.08f;
    [SerializeField, Range(0f, 2f)] private float intensity = .82f;
    [SerializeField, Range(1, 4)] private int blurIterations = 2;
    private Material material;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (material == null)
        {
            Shader shader = Shader.Find("Hidden/ACG/MemoryGridBloom");
            if (shader != null) material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        if (material == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        int width = Mathf.Max(1, source.width / 2);
        int height = Mathf.Max(1, source.height / 2);
        RenderTextureFormat format = source.format;
        RenderTexture first = RenderTexture.GetTemporary(width, height, 0, format);
        RenderTexture second = RenderTexture.GetTemporary(width, height, 0, format);
        material.SetFloat("_Threshold", threshold);
        Graphics.Blit(source, first, material, 0);
        for (int i = 0; i < blurIterations; i++)
        {
            material.SetVector("_BlurDirection", new Vector2(1.5f + i, 0f));
            Graphics.Blit(first, second, material, 1);
            material.SetVector("_BlurDirection", new Vector2(0f, 1.5f + i));
            Graphics.Blit(second, first, material, 1);
        }
        material.SetTexture("_BloomTex", first);
        material.SetFloat("_Intensity", intensity);
        Graphics.Blit(source, destination, material, 2);
        RenderTexture.ReleaseTemporary(first);
        RenderTexture.ReleaseTemporary(second);
    }

    private void OnDestroy()
    {
        if (material != null) DestroyImmediate(material);
    }
}
