using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class FloorTile : MonoBehaviour
{
    [Header("Tile identity")]
    [SerializeField, FormerlySerializedAs("currentState")]
    private TileType startingType = TileType.Default;

    [SerializeField] private Renderer visualRenderer;
    [SerializeField] private Collider solidCollider;
    [SerializeField] private bool hideVisualWhenObjectiveInactive;

    [Header("Shared visual materials")]
    [SerializeField, FormerlySerializedAs("completedMaterial")]
    private Material defaultMaterial;
    [SerializeField, FormerlySerializedAs("greenMaterial")]
    private Material objectiveMaterial;
    [SerializeField] private Material objectiveCompletedMaterial;
    [SerializeField, FormerlySerializedAs("redMaterial")]
    private Material unjumpableHazardMaterial;
    [SerializeField] private Material jumpableHazardMaterial;
    [SerializeField, FormerlySerializedAs("warningMaterial")]
    private Material jumpableWarningMaterial;
    [SerializeField] private Material unjumpableWarningMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material portalMaterial;
    [SerializeField] private Material movingIndicatorMaterial;

    [Header("Warning cycle")]
    [SerializeField, Min(0f)] private float initialWarningDelay;
    [SerializeField, Min(0.05f)] private float warningDuration = 2f;
    [SerializeField, Min(0.05f)] private float hazardDuration = 2.5f;
    [SerializeField] private bool loopWarningCycle = true;

    [Header("Hazard detection")]
    [SerializeField, Min(0f)] private float jumpableClearance = 0.15f;

    [Header("Portal")]
    [SerializeField] private FloorTile portalPartner;
    [SerializeField, Min(0f)] private float portalExitDistance = 1.25f;
    [SerializeField, Min(0f)] private float portalCooldown = 0.5f;

    private TileType runtimeType;
    private bool objectiveCompleted;
    private bool objectiveEnabled = true;
    private bool movementIndicatorActive;
    private float portalBlockedUntil;
    private Coroutine warningRoutine;
    private long overrideSequence;
    private bool celebrating;
    private bool rendererEnabledBeforeCelebration;

    private readonly Dictionary<Object, TileOverride> tileOverrides = new Dictionary<Object, TileOverride>();

    private struct TileOverride
    {
        public TileType Type;
        public int Priority;
        public long Sequence;
    }

    public TileType StartingType => startingType;
    public TileType RuntimeType => GetEffectiveType();
    public bool IsObjective => startingType == TileType.Objective && objectiveEnabled;
    public bool IsCompleted => objectiveCompleted;
    public bool ObjectiveEnabled => objectiveEnabled;
    public FloorTile PortalPartner => portalPartner;

    private void Awake()
    {
        ResolveReferences();
        runtimeType = startingType;
        ApplyVisual();
    }

    private void Start()
    {
        if (IsObjective)
        {
            GameManager.Instance?.RegisterObjectiveTile(this);
        }

        StartWarningCycleIfNeeded();
    }

    private void OnDisable()
    {
        if (warningRoutine != null)
        {
            StopCoroutine(warningRoutine);
            warningRoutine = null;
        }
    }

    private void OnValidate()
    {
        ResolveReferences();
        runtimeType = startingType;
        ApplyVisual();
    }

    private void ResolveReferences()
    {
        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<Renderer>();
        }

        if (solidCollider == null)
        {
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider candidate in colliders)
            {
                if (!candidate.isTrigger)
                {
                    solidCollider = candidate;
                    break;
                }
            }
        }
    }

    public void TryInteract(Collider playerCollider, bool enteredThisFrame)
    {
        if (playerCollider == null)
        {
            return;
        }

        TopDownPlayer player = playerCollider.attachedRigidbody != null
            ? playerCollider.attachedRigidbody.GetComponent<TopDownPlayer>()
            : playerCollider.GetComponentInParent<TopDownPlayer>();

        if (player == null)
        {
            return;
        }

        switch (GetEffectiveType())
        {
            case TileType.Objective:
                if (objectiveEnabled && IsPlayerLowEnoughForTileSurface(playerCollider))
                {
                    CompleteObjective();
                }
                break;

            case TileType.HazardUnjumpable:
                player.ReportHazardHit(this, false);
                break;

            case TileType.HazardJumpable:
                if (IsPlayerLowEnoughForTileSurface(playerCollider))
                {
                    player.ReportHazardHit(this, true);
                }
                break;

            case TileType.Portal:
                if (IsPlayerLowEnoughForTileSurface(playerCollider))
                {
                    TryTeleport(player, playerCollider);
                }
                break;
        }
    }

    private bool IsPlayerLowEnoughForTileSurface(Collider playerCollider)
    {
        float tileSurface = solidCollider != null
            ? solidCollider.bounds.max.y
            : transform.position.y;

        return playerCollider.bounds.min.y <= tileSurface + jumpableClearance;
    }

    private void CompleteObjective()
    {
        if (!objectiveEnabled || objectiveCompleted)
        {
            return;
        }

        objectiveCompleted = true;
        Debug.Log($"Objective tile completed: {name}", this);
        ApplyVisual();
        SceneAudioDirector.PlayObjectiveTrigger();
        GameManager.Instance?.ObjectiveTileCompleted(this);
    }

    private void TryTeleport(TopDownPlayer player, Collider playerCollider)
    {
        if (Time.time < portalBlockedUntil)
        {
            return;
        }

        if (portalPartner == null)
        {
            Debug.LogWarning($"Portal '{name}' has no partner. Assign one directly or use a PortalPair prefab.", this);
            return;
        }

        float playerBottomOffset = player.transform.position.y - playerCollider.bounds.min.y;
        float partnerSurface = portalPartner.solidCollider != null
            ? portalPartner.solidCollider.bounds.max.y
            : portalPartner.transform.position.y;

        Vector3 exitDirection = portalPartner.transform.forward;
        Vector3 destination = portalPartner.transform.position + exitDirection * portalExitDistance;
        destination.y = partnerSurface + playerBottomOffset + 0.05f;

        float blockedUntil = Time.time + portalCooldown;
        portalBlockedUntil = blockedUntil;
        portalPartner.portalBlockedUntil = blockedUntil;

        player.TeleportTo(destination);
        Debug.Log($"Teleported from {name} to {portalPartner.name}.", this);
    }

    public void SetPortalPartner(FloorTile partner)
    {
        portalPartner = partner;
    }

    public void SetMovementIndicator(bool active)
    {
        movementIndicatorActive = active;
        ApplyVisual();
    }

    public bool ApplyTileOverride(Object source, TileType overrideType, int priority)
    {
        if (source == null || startingType == TileType.Wall)
        {
            return false;
        }

        tileOverrides[source] = new TileOverride
        {
            Type = overrideType,
            Priority = priority,
            Sequence = ++overrideSequence
        };
        ApplyVisual();
        return true;
    }

    public void RemoveTileOverride(Object source)
    {
        if (source != null && tileOverrides.Remove(source))
        {
            ApplyVisual();
        }
    }

    public void ClearTileOverrides()
    {
        if (tileOverrides.Count == 0)
        {
            return;
        }

        tileOverrides.Clear();
        ApplyVisual();
    }

    public void SetCelebrationGreen(bool active, Object source)
    {
        if (visualRenderer == null) ResolveReferences();
        if (active)
        {
            if (!celebrating && visualRenderer != null)
                rendererEnabledBeforeCelebration = visualRenderer.enabled;
            celebrating = true;
            ApplyTileOverride(source, TileType.DisplayGreen, 1000);
            if (visualRenderer != null) visualRenderer.enabled = true;
        }
        else
        {
            celebrating = false;
            RemoveTileOverride(source);
            if (visualRenderer != null) visualRenderer.enabled = rendererEnabledBeforeCelebration;
        }
    }

    public void SetObjectiveEnabled(bool enabledState)
    {
        if (startingType != TileType.Objective) return;
        objectiveEnabled = enabledState;
        ApplyVisual();
    }

    public void SetHideVisualWhenObjectiveInactive(bool hide)
    {
        hideVisualWhenObjectiveInactive = hide;
        ApplyVisual();
    }

    private TileType GetEffectiveType()
    {
        if (startingType == TileType.Wall)
        {
            return TileType.Wall;
        }

        bool foundOverride = false;
        TileOverride strongest = default;
        foreach (TileOverride candidate in tileOverrides.Values)
        {
            if (!foundOverride ||
                candidate.Priority > strongest.Priority ||
                candidate.Priority == strongest.Priority && candidate.Sequence > strongest.Sequence)
            {
                strongest = candidate;
                foundOverride = true;
            }
        }

        return foundOverride ? strongest.Type : runtimeType;
    }

    private void StartWarningCycleIfNeeded()
    {
        if (startingType != TileType.WarningJumpable &&
            startingType != TileType.WarningUnjumpable)
        {
            return;
        }

        warningRoutine = StartCoroutine(WarningCycle());
    }

    private IEnumerator WarningCycle()
    {
        if (initialWarningDelay > 0f)
        {
            yield return new WaitForSeconds(initialWarningDelay);
        }

        do
        {
            SetRuntimeType(startingType);
            yield return new WaitForSeconds(warningDuration);

            TileType hazardType = startingType == TileType.WarningJumpable
                ? TileType.HazardJumpable
                : TileType.HazardUnjumpable;

            SetRuntimeType(hazardType);
            yield return new WaitForSeconds(hazardDuration);
            SetRuntimeType(startingType);
        }
        while (loopWarningCycle);

        warningRoutine = null;
    }

    private void SetRuntimeType(TileType newType)
    {
        runtimeType = newType;
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (visualRenderer == null)
        {
            return;
        }

        if (startingType == TileType.Objective && hideVisualWhenObjectiveInactive)
        {
            bool objectiveVisualActive = objectiveEnabled &&
                (!objectiveCompleted || GetEffectiveType() != TileType.Objective);
            visualRenderer.enabled = objectiveVisualActive;
            if (!objectiveVisualActive)
            {
                return;
            }
        }

        Material material = GetActiveMaterial();
        if (material != null)
        {
            visualRenderer.sharedMaterial = material;
        }
    }

    private Material GetActiveMaterial()
    {
        if (movementIndicatorActive && movingIndicatorMaterial != null)
        {
            return movingIndicatorMaterial;
        }

        TileType effectiveType = GetEffectiveType();

        if (objectiveCompleted && effectiveType == TileType.Objective && objectiveCompletedMaterial != null)
        {
            return objectiveCompletedMaterial;
        }

        switch (effectiveType)
        {
            case TileType.DisplayGreen:
                return objectiveMaterial;
            case TileType.DisplayRed:
                return jumpableHazardMaterial;
            case TileType.DisplayStart:
                return portalMaterial;
            case TileType.Objective:
                return objectiveEnabled ? objectiveMaterial : defaultMaterial;
            case TileType.HazardUnjumpable:
                return unjumpableHazardMaterial;
            case TileType.HazardJumpable:
                return jumpableHazardMaterial;
            case TileType.WarningUnjumpable:
                return unjumpableWarningMaterial;
            case TileType.WarningJumpable:
                return jumpableWarningMaterial;
            case TileType.Wall:
                return wallMaterial;
            case TileType.Portal:
                return portalMaterial;
            default:
                return defaultMaterial;
        }
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(
        TileType type,
        Renderer rendererReference,
        Collider solidColliderReference,
        Material defaultMat,
        Material objectiveMat,
        Material completedMat,
        Material unjumpableMat,
        Material jumpableMat,
        Material jumpableWarningMat,
        Material unjumpableWarningMat,
        Material wallMat,
        Material portalMat,
        Material movingMat)
    {
        startingType = type;
        runtimeType = type;
        objectiveCompleted = false;
        objectiveEnabled = true;
        visualRenderer = rendererReference;
        solidCollider = solidColliderReference;
        defaultMaterial = defaultMat;
        objectiveMaterial = objectiveMat;
        objectiveCompletedMaterial = completedMat;
        unjumpableHazardMaterial = unjumpableMat;
        jumpableHazardMaterial = jumpableMat;
        jumpableWarningMaterial = jumpableWarningMat;
        unjumpableWarningMaterial = unjumpableWarningMat;
        wallMaterial = wallMat;
        portalMaterial = portalMat;
        movingIndicatorMaterial = movingMat;
        if (portalExitDistance <= 0f)
        {
            portalExitDistance = 1.25f;
        }
        ApplyVisual();
    }
#endif
}
