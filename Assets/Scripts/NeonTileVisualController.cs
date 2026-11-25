using UnityEngine;

[DisallowMultipleComponent]
public sealed class NeonTileVisualController : MonoBehaviour
{
    [SerializeField] private FloorTile tile;
    [SerializeField] private Renderer shellRenderer;
    [SerializeField] private Renderer coreRenderer;
    [SerializeField] private LineRenderer outline;
    [SerializeField] private Light coreLight;
    [SerializeField] private Material shellMaterial;

    [Header("Pressure animation")]
    [SerializeField, Min(0f)] private float sinkDepth = .18f;
    [SerializeField, Min(.01f)] private float sinkTime = .075f;
    [SerializeField, Min(.01f)] private float releaseTime = .16f;
    [SerializeField, Min(0f)] private float idleFloatAmount = .045f;
    [SerializeField, Min(0f)] private float idleFloatSpeed = 1.35f;

    private MaterialPropertyBlock shellBlock;
    private MaterialPropertyBlock coreBlock;
    private TileType lastType;
    private bool lastCompleted;
    private bool lastObjectiveEnabled;
    private bool initialized;
    private Color displayedColor;
    private float displayedEmission;
    private float displayedLightIntensity;
    private float displayedShellAlpha;
    private float displayedOutlineAlpha;
    private TopDownPlayer player;
    private Transform coreTransform;
    private Transform outlineTransform;
    private Transform lightTransform;
    private Vector3 coreRestPosition;
    private Vector3 outlineRestPosition;
    private Vector3 lightRestPosition;
    private float pressDepth;
    private float pressVelocity;
    private float floatPhase;

    private void Awake()
    {
        shellBlock = new MaterialPropertyBlock();
        coreBlock = new MaterialPropertyBlock();
        CaptureMotionParts();
        Refresh(true);
    }

    private void Start()
    {
        if (player == null) player = FindAnyObjectByType<TopDownPlayer>();
    }

    private void LateUpdate()
    {
        if (shellRenderer != null && shellMaterial != null && shellRenderer.sharedMaterial != shellMaterial)
            shellRenderer.sharedMaterial = shellMaterial;
        // Reassert the property blocks every frame. Material swaps, skybox/GI
        // refreshes and renderer rebinds can clear them without changing TileType.
        Refresh(false);
        AnimateInnerBlock();
    }

    private void CaptureMotionParts()
    {
        coreTransform = coreRenderer != null ? coreRenderer.transform : null;
        outlineTransform = outline != null ? outline.transform : null;
        lightTransform = coreLight != null ? coreLight.transform : null;
        if (coreTransform != null) coreRestPosition = coreTransform.localPosition;
        if (outlineTransform != null) outlineRestPosition = outlineTransform.localPosition;
        if (lightTransform != null) lightRestPosition = lightTransform.localPosition;
        Vector3 position = transform.position;
        floatPhase = position.x * .37f + position.z * .23f;
    }

    private void AnimateInnerBlock()
    {
        if (!Application.isPlaying || coreTransform == null) return;
        bool pressed = IsPlayerStandingOnTile();
        float targetDepth = pressed ? sinkDepth : 0f;
        pressDepth = Mathf.SmoothDamp(pressDepth, targetDepth, ref pressVelocity,
            pressed ? sinkTime : releaseTime, Mathf.Infinity, Time.deltaTime);
        float floating = pressed ? 0f : Mathf.Sin(Time.time * idleFloatSpeed + floatPhase) * idleFloatAmount;
        float offset = floating - pressDepth;
        coreTransform.localPosition = coreRestPosition + Vector3.up * offset;
        if (outlineTransform != null) outlineTransform.localPosition = outlineRestPosition + Vector3.up * offset;
        if (lightTransform != null) lightTransform.localPosition = lightRestPosition + Vector3.up * offset;
    }

    private bool IsPlayerStandingOnTile()
    {
        if (player == null || !player.IsGrounded || shellRenderer == null) return false;
        Bounds bounds = shellRenderer.bounds;
        Vector3 position = player.transform.position;
        return position.x >= bounds.min.x && position.x <= bounds.max.x &&
               position.z >= bounds.min.z && position.z <= bounds.max.z;
    }

    private void Refresh(bool force)
    {
        if (tile == null) return;
        shellBlock ??= new MaterialPropertyBlock();
        coreBlock ??= new MaterialPropertyBlock();
        TileType type = tile.RuntimeType;
        // Future-wave objectives look like normal blue tiles while idle, but must
        // still accept temporary celebration and ground-laser visual overrides.
        if (tile.StartingType == TileType.Objective && !tile.ObjectiveEnabled && type == TileType.Objective)
            type = TileType.Default;
        bool hadVisual = initialized;
        initialized = true;
        lastType = type;
        lastCompleted = tile.IsCompleted;
        lastObjectiveEnabled = tile.ObjectiveEnabled;

        Color color;
        float emission;
        float lightIntensity;
        switch (type)
        {
            case TileType.Objective:
                if (tile.IsCompleted)
                {
                    color = new Color(.08f, .34f, .46f, .32f); emission = .22f; lightIntensity = 0f;
                    break;
                }
                color = new Color(.02f, 1f, .2f, .96f); emission = 5.2f; lightIntensity = 3.2f; break;
            case TileType.DisplayGreen:
                color = new Color(.02f, 1f, .2f, .96f); emission = 5.2f; lightIntensity = 3.2f; break;
            case TileType.DisplayRed:
            case TileType.HazardJumpable:
            case TileType.HazardUnjumpable:
                color = new Color(1f, .025f, .015f, .88f); emission = 5.5f; lightIntensity = 3.5f; break;
            case TileType.WarningJumpable:
            case TileType.WarningUnjumpable:
                color = new Color(1f, .58f, .02f, .84f); emission = 3.5f; lightIntensity = 2.2f; break;
            case TileType.Portal:
            case TileType.DisplayStart:
                color = new Color(.78f, .06f, 1f, .86f); emission = 3.4f; lightIntensity = 2.5f; break;
            case TileType.Wall:
                color = new Color(.08f, .62f, 1f, .7f); emission = 1.8f; lightIntensity = 1.2f; break;
            default:
                color = new Color(.16f, .66f, .95f, .52f); emission = 1.35f; lightIntensity = 0f; break;
        }

        float shellAlpha = type == TileType.Objective && tile.IsCompleted ? .2f : type == TileType.Default ? .3f : .48f;
        float outlineAlpha = type == TileType.Objective && tile.IsCompleted ? .25f : type == TileType.Default ? .32f : 1f;
        if (force || !Application.isPlaying || !hadVisual)
        {
            displayedColor = color;
            displayedEmission = emission;
            displayedLightIntensity = lightIntensity;
            displayedShellAlpha = shellAlpha;
            displayedOutlineAlpha = outlineAlpha;
        }
        else
        {
            float blend = 1f - Mathf.Exp(-10f * Mathf.Max(Time.deltaTime, .0001f));
            displayedColor = Color.Lerp(displayedColor, color, blend);
            displayedEmission = Mathf.Lerp(displayedEmission, emission, blend);
            displayedLightIntensity = Mathf.Lerp(displayedLightIntensity, lightIntensity, blend);
            displayedShellAlpha = Mathf.Lerp(displayedShellAlpha, shellAlpha, blend);
            displayedOutlineAlpha = Mathf.Lerp(displayedOutlineAlpha, outlineAlpha, blend);
        }

        if (shellRenderer != null)
        {
            Color shellColor = new(displayedColor.r, displayedColor.g, displayedColor.b, displayedShellAlpha);
            shellBlock.SetColor("_Color", shellColor);
            shellBlock.SetColor("_EmissionColor", displayedColor * (displayedEmission * .16f));
            shellRenderer.SetPropertyBlock(shellBlock);
        }
        if (coreRenderer != null)
        {
            coreBlock.SetColor("_Color", displayedColor);
            coreBlock.SetColor("_EmissionColor", displayedColor * displayedEmission);
            coreRenderer.SetPropertyBlock(coreBlock);
        }
        if (outline != null)
        {
            Color outlineColor = new(displayedColor.r, displayedColor.g, displayedColor.b, displayedOutlineAlpha);
            outline.startColor = outlineColor;
            outline.endColor = outlineColor;
        }
        if (coreLight != null)
        {
            coreLight.color = displayedColor;
            coreLight.intensity = displayedLightIntensity;
            coreLight.enabled = displayedLightIntensity > .01f;
        }
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(FloorTile targetTile, Renderer shell, Renderer core,
        LineRenderer line, Light glow, Material transparentShell)
    {
        tile = targetTile;
        shellRenderer = shell;
        coreRenderer = core;
        outline = line;
        coreLight = glow;
        shellMaterial = transparentShell;
    }
#endif
}
