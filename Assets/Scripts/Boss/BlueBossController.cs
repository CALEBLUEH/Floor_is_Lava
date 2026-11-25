using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BlueBossController : MonoBehaviour, IBossDamageTarget
{
    [Header("Scene references")]
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private TopDownPlayer player;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private BossAttackProjectile playerAttackPrefab;
    [SerializeField] private Transform projectileRoot;

    [Header("Entrance")]
    [SerializeField, Min(0f)] private float spawnDelay = 1f;
    [SerializeField, Min(1f)] private float fallHeight = 16f;
    [SerializeField, Min(.05f)] private float fallDuration = .8f;
    [SerializeField, Min(0f)] private float landingWarningDuration = 1f;
    [SerializeField, Range(1, 11)] private int landingFootprint = 5;
    [SerializeField, Min(.05f)] private float landingHazardDuration = .55f;

    [Header("Boss health")]
    [SerializeField, Min(1)] private int maxHealth = 5;
    [SerializeField, Min(1)] private int damagePerGreenTile = 1;
    [SerializeField, Min(.1f)] private float playerAttackSpeed = 16f;

    [Header("Battle pacing")]
    [SerializeField, Min(0f)] private float openingDelay = 1f;
    [SerializeField, Min(0f)] private float sequenceCooldown = 1.25f;
    [SerializeField, Min(0f)] private float objectiveRespawnDelay = .65f;

    [Header("Movement")]
    [SerializeField, Min(1)] private int moveTilesTowardPlayer = 3;
    [SerializeField, Min(.05f)] private float moveDuration = .55f;
    [SerializeField, Min(0f)] private float hoverHeight = 1.8f;
    [SerializeField, Min(.01f)] private float turnDuration = .2f;
    [SerializeField, Min(0f)] private float movementHopHeight = .65f;

    [Header("Defeat presentation")]
    [SerializeField, Min(.05f)] private float defeatFadeDuration = 1.8f;
    [SerializeField, Min(0f)] private float defeatRiseDistance = 5f;

    [Header("Aimed volley")]
    [SerializeField, Min(.05f)] private float aimedShotInterval = .55f;
    [SerializeField, Min(.03f)] private float aimedProjectileSecondsPerCell = .14f;
    [SerializeField, Min(.02f)] private float projectileTileActiveTime = .11f;
    [SerializeField, Min(1)] private int maximumProjectileSteps = 48;

    [Header("Random ricochet volley")]
    [SerializeField, Min(1)] private int ricochetShotCount = 3;
    [SerializeField, Min(0)] private int ricochetBounces = 4;
    [SerializeField, Min(.05f)] private float ricochetShotInterval = .65f;
    [SerializeField, Min(.03f)] private float ricochetSecondsPerCell = .16f;

    [Header("Locked dash impact")]
    [SerializeField, Min(0f)] private float lockTelegraphDuration = 2f;
    [SerializeField, Min(0f)] private float lockedChargeDelay = 3f;
    [SerializeField, Min(.05f)] private float dashDuration = .3f;
    [SerializeField, Range(1, 11)] private int impactFootprint = 5;
    [SerializeField, Min(.05f)] private float impactHazardDuration = .75f;

    [Header("Tile overlay priorities")]
    [SerializeField] private int projectilePriority = 500;
    [SerializeField] private int impactPriority = 550;

    private readonly List<FloorTile> impactTiles = new();
    private Coroutine battleRoutine;
    private Coroutine objectiveRoutine;
    private BossDamageObjective activeObjective;
    private Coroutine defeatRoutine;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDefeated => CurrentHealth <= 0;
    public bool HasEnteredArena { get; private set; }
    public Transform TargetTransform => transform;
    public BossDamageObjective ActiveObjective => activeObjective;
    public event Action<int, int> HealthChanged;
    public event Action BlueDefeated;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        if (projectileRoot == null) projectileRoot = transform.parent != null ? transform.parent : transform;
    }

    private void Start()
    {
        tileGrid?.RebuildLookup();
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        battleRoutine = StartCoroutine(EntranceAndBattle());
    }

    private IEnumerator EntranceAndBattle()
    {
        SetPresentationVisible(false);
        if (spawnDelay > 0f) yield return new WaitForSeconds(spawnDelay);
        Vector2Int centre = new(tileGrid.Width / 2, tileGrid.Height / 2);
        Vector3 landingPosition = CellWorld(centre);
        transform.position = landingPosition + Vector3.up * fallHeight;
        SetPresentationVisible(true);
        ApplyArea(centre, landingFootprint, TileType.WarningUnjumpable, impactPriority);
        float elapsed = 0f;
        float duration = Mathf.Max(.01f, fallDuration);
        Vector3 start = transform.position;
        while (elapsed < duration && !IsDefeated)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(start, landingPosition, t);
            transform.Rotate(Vector3.up, 540f * Time.deltaTime / duration, Space.World);
            yield return null;
        }
        transform.position = landingPosition;
        if (landingWarningDuration > 0f) yield return new WaitForSeconds(landingWarningDuration);
        ApplyArea(centre, landingFootprint, TileType.HazardUnjumpable, impactPriority);
        if (landingHazardDuration > 0f) yield return new WaitForSeconds(landingHazardDuration);
        ClearImpactArea();
        HasEnteredArena = true;
        objectiveRoutine = StartCoroutine(SpawnObjectiveAfter(objectiveRespawnDelay));
        yield return BattleLoop();
    }

    private IEnumerator BattleLoop()
    {
        if (openingDelay > 0f) yield return new WaitForSeconds(openingDelay);
        while (!IsDefeated && (playerLife == null || !playerLife.IsDefeated))
        {
            yield return MoveTowardPlayer();
            yield return AimedVolley();
            yield return Cooldown();

            yield return MoveTowardPlayer();
            yield return RicochetVolley();
            yield return Cooldown();

            yield return LockedDashImpact();
            yield return Cooldown();
        }
        battleRoutine = null;
    }

    private IEnumerator Cooldown()
    {
        if (sequenceCooldown > 0f) yield return new WaitForSeconds(sequenceCooldown);
    }

    private IEnumerator MoveTowardPlayer()
    {
        if (tileGrid == null || player == null) yield break;
        Vector2Int current = WorldToCell(transform.position);
        Vector2Int target = WorldToCell(player.transform.position);
        Vector2Int direction = new(Math.Sign(target.x - current.x), Math.Sign(target.y - current.y));
        Vector2Int destination = new(
            Mathf.Clamp(current.x + direction.x * moveTilesTowardPlayer, 0, tileGrid.Width - 1),
            Mathf.Clamp(current.y + direction.y * moveTilesTowardPlayer, 0, tileGrid.Height - 1));
        yield return MoveToCell(destination, moveDuration);
    }

    private IEnumerator AimedVolley()
    {
        for (int shot = 0; shot < 3 && !IsDefeated; shot++)
        {
            Vector2Int origin = WorldToCell(transform.position);
            Vector2Int direction = DirectionToward(origin, WorldToCell(player.transform.position));
            yield return RotateToward(direction, turnDuration);
            if (shot < 2)
            {
                LaunchTileProjectile(origin, direction, 0, aimedProjectileSecondsPerCell);
            }
            else
            {
                LaunchTileProjectile(origin, RotateDirection(direction, -1), 0, aimedProjectileSecondsPerCell);
                LaunchTileProjectile(origin, direction, 0, aimedProjectileSecondsPerCell);
                LaunchTileProjectile(origin, RotateDirection(direction, 1), 0, aimedProjectileSecondsPerCell);
            }
            yield return new WaitForSeconds(aimedShotInterval);
        }
    }

    private IEnumerator RicochetVolley()
    {
        for (int shot = 0; shot < ricochetShotCount && !IsDefeated; shot++)
        {
            Vector2Int origin = WorldToCell(transform.position);
            Vector2Int randomDirection = Directions[UnityEngine.Random.Range(0, Directions.Length)];
            yield return RotateToward(randomDirection, turnDuration);
            LaunchTileProjectile(origin, randomDirection, ricochetBounces, ricochetSecondsPerCell);
            yield return new WaitForSeconds(ricochetShotInterval);
        }
    }

    private IEnumerator LockedDashImpact()
    {
        if (tileGrid == null || player == null) yield break;
        Vector2Int lockedCell = WorldToCell(player.transform.position);
        ApplyArea(lockedCell, impactFootprint, TileType.WarningUnjumpable, impactPriority);
        if (lockTelegraphDuration > 0f) yield return new WaitForSeconds(lockTelegraphDuration);
        if (lockedChargeDelay > 0f) yield return new WaitForSeconds(lockedChargeDelay);
        yield return MoveToCell(lockedCell, dashDuration);
        ApplyArea(lockedCell, impactFootprint, TileType.HazardUnjumpable, impactPriority);
        yield return new WaitForSeconds(impactHazardDuration);
        ClearImpactArea();
    }

    private IEnumerator MoveToCell(Vector2Int destination, float duration)
    {
        Vector2Int current = WorldToCell(transform.position);
        yield return RotateToward(destination - current, turnDuration);
        Vector3 start = transform.position;
        Vector3 end = CellWorld(destination);
        float elapsed = 0f;
        duration = Mathf.Max(.01f, duration);
        while (elapsed < duration && !IsDefeated)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += Mathf.Sin(t * Mathf.PI) * movementHopHeight;
            transform.position = position;
            yield return null;
        }
        if (!IsDefeated) transform.position = end;
    }

    private IEnumerator RotateToward(Vector2Int gridDirection, float duration)
    {
        if (gridDirection == Vector2Int.zero) yield break;
        Vector3 worldDirection = tileGrid.transform.TransformDirection(new Vector3(gridDirection.x, 0f, gridDirection.y)).normalized;
        Quaternion start = transform.rotation;
        Quaternion end = Quaternion.LookRotation(worldDirection, Vector3.up);
        float elapsed = 0f;
        duration = Mathf.Max(.01f, duration);
        while (elapsed < duration && !IsDefeated)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }
        if (!IsDefeated) transform.rotation = end;
    }

    private void SetPresentationVisible(bool visible)
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
        foreach (Light light in GetComponentsInChildren<Light>(true)) light.enabled = visible;
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = visible;
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
        {
            if (visible) particles.Play(true);
            else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void LaunchTileProjectile(Vector2Int origin, Vector2Int direction, int bounces, float secondsPerCell)
    {
        if (direction == Vector2Int.zero || tileGrid == null) return;
        GameObject source = new("Blue Boss Tile Projectile", typeof(BossTileProjectile));
        source.transform.SetParent(projectileRoot, false);
        source.GetComponent<BossTileProjectile>().Initialize(tileGrid, origin, direction, bounces,
            maximumProjectileSteps, secondsPerCell, projectileTileActiveTime, projectilePriority);
    }

    private void ApplyArea(Vector2Int centre, int footprint, TileType type, int priority)
    {
        ClearImpactArea();
        int size = Mathf.Max(1, footprint);
        int minOffset = -(size / 2);
        int maxOffset = minOffset + size - 1;
        for (int y = minOffset; y <= maxOffset; y++)
        for (int x = minOffset; x <= maxOffset; x++)
        {
            FloorTile tile = tileGrid.GetTile(centre.x + x, centre.y + y);
            if (tile != null && tile.ApplyTileOverride(this, type, priority)) impactTiles.Add(tile);
        }
    }

    private void ClearImpactArea()
    {
        foreach (FloorTile tile in impactTiles) if (tile != null) tile.RemoveTileOverride(this);
        impactTiles.Clear();
    }

    private IEnumerator SpawnObjectiveAfter(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        objectiveRoutine = null;
        SpawnDamageObjective();
    }

    private void SpawnDamageObjective()
    {
        if (IsDefeated || tileGrid == null || activeObjective != null) return;
        List<FloorTile> candidates = tileGrid.GetComponentsInChildren<FloorTile>(true)
            .Where(tile => tile.StartingType != TileType.Wall &&
                           Vector3.Distance(tile.transform.position, transform.position) >= tileGrid.CellSize * 2f &&
                           (player == null || Vector3.Distance(tile.transform.position, player.transform.position) >= tileGrid.CellSize))
            .ToList();
        if (candidates.Count == 0) return;
        FloorTile selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        activeObjective = selected.GetComponent<BossDamageObjective>();
        if (activeObjective == null) activeObjective = selected.gameObject.AddComponent<BossDamageObjective>();
        activeObjective.Activate(selected, this, player);
    }

    public void LaunchPlayerAttack(Vector3 launchPosition)
    {
        activeObjective = null;
        if (IsDefeated) return;
        if (playerAttackPrefab != null)
        {
            BossAttackProjectile projectile = Instantiate(playerAttackPrefab, launchPosition, Quaternion.identity, projectileRoot);
            projectile.Initialize(this, damagePerGreenTile, playerAttackSpeed);
        }
        else
        {
            TakeDamage(damagePerGreenTile);
        }
    }

    public void TakeDamage(int amount)
    {
        if (IsDefeated || amount <= 0) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (IsDefeated)
        {
            StopBattle();
            BlueDefeated?.Invoke();
            defeatRoutine = StartCoroutine(RiseAndFade());
            return;
        }
        if (objectiveRoutine != null) StopCoroutine(objectiveRoutine);
        objectiveRoutine = StartCoroutine(SpawnObjectiveAfter(objectiveRespawnDelay));
    }

    private IEnumerator RiseAndFade()
    {
        Collider[] colliders = GetComponentsInChildren<Collider>(true);
        foreach (Collider collider in colliders) collider.enabled = false;
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        Material[][] materials = new Material[renderers.Length][];
        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            materials[rendererIndex] = renderers[rendererIndex].materials;
            foreach (Material material in materials[rendererIndex]) ConfigureFadeMaterial(material);
        }
        Vector3 start = transform.position;
        float elapsed = 0f;
        float duration = Mathf.Max(.05f, defeatFadeDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = start + Vector3.up * defeatRiseDistance * t;
            foreach (Material[] rendererMaterials in materials)
            foreach (Material material in rendererMaterials)
            {
                if (material == null) continue;
                Color colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
                colour.a = 1f - t;
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
                if (material.HasProperty("_Color")) material.color = colour;
            }
            yield return null;
        }
        foreach (Renderer renderer in renderers) renderer.enabled = false;
        foreach (Light light in GetComponentsInChildren<Light>(true)) light.enabled = false;
        defeatRoutine = null;
    }

    private static void ConfigureFadeMaterial(Material material)
    {
        if (material == null) return;
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = 3000;
    }

    private void StopBattle()
    {
        if (battleRoutine != null) StopCoroutine(battleRoutine);
        battleRoutine = null;
        if (objectiveRoutine != null) StopCoroutine(objectiveRoutine);
        objectiveRoutine = null;
        if (activeObjective != null) activeObjective.Deactivate();
        activeObjective = null;
        ClearImpactArea();
        ParticleSystem particles = GetComponentInChildren<ParticleSystem>();
        if (particles != null) particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    private void SnapToNearestCell()
    {
        if (tileGrid != null) transform.position = CellWorld(WorldToCell(transform.position));
    }

    private Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = tileGrid.transform.InverseTransformPoint(world);
        return new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(local.x / tileGrid.CellSize), 0, tileGrid.Width - 1),
            Mathf.Clamp(Mathf.RoundToInt(local.z / tileGrid.CellSize), 0, tileGrid.Height - 1));
    }

    private Vector3 CellWorld(Vector2Int coordinate)
    {
        FloorTile tile = tileGrid.GetTile(coordinate.x, coordinate.y);
        Vector3 world = tile != null
            ? tile.transform.position
            : tileGrid.transform.TransformPoint(new Vector3(coordinate.x * tileGrid.CellSize, 0f, coordinate.y * tileGrid.CellSize));
        world.y += hoverHeight;
        return world;
    }

    private static Vector2Int DirectionToward(Vector2Int from, Vector2Int to)
    {
        return new Vector2Int(Math.Sign(to.x - from.x), Math.Sign(to.y - from.y));
    }

    private static Vector2Int RotateDirection(Vector2Int direction, int steps)
    {
        int index = Array.IndexOf(Directions, direction);
        if (index < 0) return direction;
        index = (index + steps + Directions.Length) % Directions.Length;
        return Directions[index];
    }

    private static readonly Vector2Int[] Directions =
    {
        Vector2Int.up,
        new(1, 1),
        Vector2Int.right,
        new(1, -1),
        Vector2Int.down,
        new(-1, -1),
        Vector2Int.left,
        new(-1, 1)
    };

    private void OnDestroy()
    {
        ClearImpactArea();
        if (activeObjective != null) activeObjective.Deactivate();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(TileGrid grid, TopDownPlayer targetPlayer, PlayerLife life,
        BossAttackProjectile attackPrefab, Transform spawnedProjectileRoot)
    {
        tileGrid = grid;
        player = targetPlayer;
        playerLife = life;
        playerAttackPrefab = attackPrefab;
        projectileRoot = spawnedProjectileRoot;
    }

    public void ConfigureFastTestTimings()
    {
        openingDelay = .02f;
        spawnDelay = .02f;
        fallDuration = .04f;
        landingWarningDuration = .04f;
        landingHazardDuration = .04f;
        sequenceCooldown = .04f;
        objectiveRespawnDelay = .02f;
        playerAttackSpeed = 1000f;
        moveDuration = .06f;
        aimedShotInterval = .07f;
        aimedProjectileSecondsPerCell = .06f;
        projectileTileActiveTime = .045f;
        ricochetShotInterval = .07f;
        ricochetSecondsPerCell = .06f;
        lockTelegraphDuration = .08f;
        lockedChargeDelay = .08f;
        dashDuration = .06f;
        impactHazardDuration = .12f;
    }

    public void StopBattleForTest()
    {
        if (battleRoutine != null) StopCoroutine(battleRoutine);
        battleRoutine = null;
        ClearImpactArea();
    }

    public void SpawnObjectiveForTest()
    {
        if (activeObjective != null) activeObjective.Deactivate();
        activeObjective = null;
        SpawnDamageObjective();
    }

    public void StartAimedVolleyForTest() => StartCoroutine(AimedVolley());
    public void StartRicochetVolleyForTest() => StartCoroutine(RicochetVolley());
    public void StartLockedDashForTest() => StartCoroutine(LockedDashImpact());
#endif
}
