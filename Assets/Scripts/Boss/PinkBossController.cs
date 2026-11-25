using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PinkBossController : MonoBehaviour, IBossDamageTarget
{
    [Header("Scene references")]
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private TopDownPlayer player;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private BossAttackProjectile playerAttackPrefab;
    [SerializeField] private Transform projectileRoot;
    [SerializeField] private GameObject beamRoot;
    [SerializeField] private BossBattleHud battleHud;

    [Header("Health and objectives")]
    [SerializeField, Min(1)] private int maxHealth = 7;
    [SerializeField, Min(1)] private int damagePerGreenTile = 1;
    [SerializeField, Min(.1f)] private float playerAttackSpeed = 16f;
    [SerializeField, Min(0f)] private float objectiveRespawnDelay = .65f;

    [Header("Beam entrance and defeat")]
    [SerializeField, Min(.05f)] private float entranceDuration = 2.2f;
    [SerializeField, Min(.05f)] private float bossFadeDuration = 1.25f;
    [SerializeField, Min(.05f)] private float defeatBeamDuration = 2f;
    [SerializeField, Range(1, 7)] private int beamDamageFootprint = 3;

    [Header("Figure-eight movement")]
    [SerializeField, Min(.1f)] private float figureEightPeriod = 8f;
    [SerializeField, Min(.1f)] private float figureEightWidthTiles = 5f;
    [SerializeField, Min(.1f)] private float figureEightHeightTiles = 3f;
    [SerializeField, Min(0f)] private float hoverHeight = 1.8f;

    [Header("Five stacked player fields")]
    [SerializeField, Min(1)] private int stackedFieldCount = 5;
    [SerializeField, Min(0f)] private float stackedPlacementInterval = .16f;
    [SerializeField, Min(0f)] private float stackedWarningDuration = 1f;
    [SerializeField, Min(.05f)] private float stackedHazardDuration = .7f;

    [Header("Four long strikes")]
    [SerializeField, Min(1)] private int longStrikeCount = 4;
    [SerializeField, Min(0f)] private float longStrikeWarningDuration = 1f;
    [SerializeField, Min(.05f)] private float longStrikeHazardDuration = 5f;

    [Header("Arbitrary-angle lines")]
    [SerializeField, Min(1)] private int lineAttackCount = 5;
    [SerializeField, Min(0f)] private float lineWarningDuration = 1f;
    [SerializeField, Min(.05f)] private float lineHazardDuration = .65f;

    [Header("Rotating cross laser")]
    [SerializeField, Min(8)] private int laserStepsPerLoop = 32;
    [SerializeField, Min(0f)] private float crossLaserWarningDuration = 1f;
    [SerializeField, Min(.1f)] private float regularLaserLoopDuration = 10f;
    [SerializeField] private float[] desperationLoopDurations = { 10f, 9f, 8f, 6f };
    [SerializeField, Min(.05f)] private float desperationReturnDuration = 1.25f;
    [SerializeField, Min(0f)] private float desperationAttackDelay = 1f;

    [Header("Battle pacing")]
    [SerializeField, Min(0f)] private float sequenceCooldown = 1.1f;
    [SerializeField] private int attackPriority = 600;

    private readonly List<FloorTile> beamTiles = new();
    private Coroutine battleRoutine;
    private Coroutine movementRoutine;
    private Coroutine objectiveRoutine;
    private BossDamageObjective activeObjective;
    private Renderer[] bossRenderers;
    private Material[][] fadeMaterials;
    private Vector3 arenaCentre;
    private bool crossLaserActive;
    private bool desperationStarted;
    private BossAttackCoroutineHost attackHost;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDefeated => CurrentHealth <= 0;
    public bool HasEnteredArena { get; private set; }
    public bool DefeatSequenceComplete { get; private set; }
    public bool IsInDesperation => desperationStarted && !DefeatSequenceComplete;
    public float DesperationRemainingNormalized { get; private set; }
    public Transform TargetTransform => transform;
    public event Action<int, int> HealthChanged;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        if (projectileRoot == null) projectileRoot = transform.parent != null ? transform.parent : transform;
        attackHost = BossAttackCoroutineHost.GetOrAdd(projectileRoot);
        bossRenderers = GetComponentsInChildren<Renderer>(true);
        if (beamRoot != null) beamRoot.SetActive(false);
        SetBossPresentation(false);
    }

    private void Start() => HealthChanged?.Invoke(CurrentHealth, maxHealth);

    public void BeginEntrance()
    {
        if (HasEnteredArena || battleRoutine != null) return;
        StartCoroutine(EntranceSequence());
    }

    private IEnumerator EntranceSequence()
    {
        tileGrid.RebuildLookup();
        Vector2Int centreCell = new(tileGrid.Width / 2, tileGrid.Height / 2);
        arenaCentre = CellWorld(centreCell);
        transform.position = arenaCentre;
        PositionBeam(arenaCentre);
        ApplyArea(this, beamTiles, centreCell, beamDamageFootprint, TileType.HazardUnjumpable);
        if (beamRoot != null) beamRoot.SetActive(true);
        yield return new WaitForSeconds(Mathf.Max(.05f, entranceDuration - bossFadeDuration));
        SetBossPresentation(true);
        yield return FadeBoss(0f, 1f, bossFadeDuration, Vector3.zero);
        ClearArea(this, beamTiles);
        if (beamRoot != null) beamRoot.SetActive(false);
        HasEnteredArena = true;
        movementRoutine = StartCoroutine(FigureEightMovement());
        objectiveRoutine = StartCoroutine(SpawnObjectiveAfter(objectiveRespawnDelay));
        battleRoutine = StartCoroutine(BattleLoop());
    }

    private IEnumerator BattleLoop()
    {
        while (!IsDefeated && (playerLife == null || !playerLife.IsDefeated))
        {
            yield return RunPersistentAttack(StackedPlayerFields());
            yield return Cooldown();
            yield return RunPersistentAttack(LongLivedStrikes());
            yield return Cooldown();
            yield return RunPersistentAttack(AngledLineSequence());
            yield return Cooldown();
            yield return RunPersistentAttack(RotateCrossLaser(regularLaserLoopDuration, true, true, false));
            yield return Cooldown();
        }
        battleRoutine = null;
    }

    private IEnumerator FigureEightMovement()
    {
        float elapsed = 0f;
        while (!DefeatSequenceComplete)
        {
            if (HasEnteredArena && !crossLaserActive && !desperationStarted)
            {
                elapsed += Time.deltaTime;
                float angle = elapsed / Mathf.Max(.1f, figureEightPeriod) * Mathf.PI * 2f;
                float x = Mathf.Sin(angle) * figureEightWidthTiles * tileGrid.CellSize;
                float z = Mathf.Sin(angle * 2f) * .5f * figureEightHeightTiles * tileGrid.CellSize;
                transform.position = arenaCentre + new Vector3(x, 0f, z);
                transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(angle), 0f, Mathf.Cos(angle * 2f)), Vector3.up);
                if (WorldToCell(player.transform.position) == WorldToCell(transform.position))
                    player.ReportHazardHit(this, false);
            }
            yield return null;
        }
        movementRoutine = null;
    }

    private IEnumerator StackedPlayerFields()
    {
        List<GameObject> sources = new();
        List<List<FloorTile>> areas = new();
        for (int index = 0; index < stackedFieldCount && !IsDefeated; index++)
        {
            Vector2Int playerCell = WorldToCell(player.transform.position);
            playerCell += new Vector2Int(UnityEngine.Random.Range(-1, 2), UnityEngine.Random.Range(-1, 2));
            playerCell.x = Mathf.Clamp(playerCell.x, 0, tileGrid.Width - 1);
            playerCell.y = Mathf.Clamp(playerCell.y, 0, tileGrid.Height - 1);
            GameObject source = CreateOverlaySource("Pink Stacked Field");
            List<FloorTile> area = BuildSquarePlusCross(playerCell);
            ApplyTiles(source, area, TileType.WarningJumpable);
            sources.Add(source);
            areas.Add(area);
            if (stackedPlacementInterval > 0f) yield return new WaitForSeconds(stackedPlacementInterval);
        }
        if (stackedWarningDuration > 0f) yield return new WaitForSeconds(stackedWarningDuration);
        for (int index = 0; index < sources.Count; index++) ReplaceTiles(sources[index], areas[index], TileType.HazardJumpable);
        if (stackedHazardDuration > 0f) yield return new WaitForSeconds(stackedHazardDuration);
        CleanupAreas(sources, areas);
    }

    private IEnumerator LongLivedStrikes()
    {
        List<GameObject> sources = new();
        List<List<FloorTile>> areas = new();
        for (int index = 0; index < longStrikeCount; index++)
        {
            Vector2Int centre = index == 0 ? WorldToCell(player.transform.position) :
                new Vector2Int(UnityEngine.Random.Range(0, tileGrid.Width), UnityEngine.Random.Range(0, tileGrid.Height));
            GameObject source = CreateOverlaySource("Pink Long Strike");
            List<FloorTile> area = BuildSquarePlusCross(centre);
            ApplyTiles(source, area, TileType.WarningJumpable);
            sources.Add(source);
            areas.Add(area);
        }
        if (longStrikeWarningDuration > 0f) yield return new WaitForSeconds(longStrikeWarningDuration);
        for (int index = 0; index < sources.Count; index++) ReplaceTiles(sources[index], areas[index], TileType.HazardJumpable);
        if (longStrikeHazardDuration > 0f) yield return new WaitForSeconds(longStrikeHazardDuration);
        CleanupAreas(sources, areas);
    }

    private IEnumerator AngledLineSequence()
    {
        for (int index = 0; index < lineAttackCount && !IsDefeated; index++)
        {
            GameObject source = CreateOverlaySource("Pink Angled Line");
            List<FloorTile> line = BuildLineThroughPlayer();
            ApplyTiles(source, line, TileType.WarningJumpable);
            if (lineWarningDuration > 0f) yield return new WaitForSeconds(lineWarningDuration);
            ReplaceTiles(source, line, TileType.HazardJumpable);
            if (lineHazardDuration > 0f) yield return new WaitForSeconds(lineHazardDuration);
            ClearTiles(source, line);
            Destroy(source);
        }
    }

    private IEnumerator RotateCrossLaser(float loopDuration, bool clockwise, bool showWarning, bool continueAfterDefeat)
    {
        crossLaserActive = true;
        arenaCentre = CellWorld(new Vector2Int(tileGrid.Width / 2, tileGrid.Height / 2));
        transform.position = arenaCentre;
        int steps = Mathf.Max(8, laserStepsPerLoop);
        float stepDuration = Mathf.Max(.02f, loopDuration / steps);
        GameObject source = CreateOverlaySource("Pink Rotating Cross Laser");
        List<FloorTile> active = new();
        if (showWarning)
        {
            active = BuildRotatedCross(0f);
            ApplyTiles(source, active, TileType.WarningJumpable);
            if (crossLaserWarningDuration > 0f) yield return new WaitForSeconds(crossLaserWarningDuration);
        }
        for (int step = 0; step < steps && (continueAfterDefeat || !IsDefeated); step++)
        {
            ClearTiles(source, active);
            float direction = clockwise ? 1f : -1f;
            float angle = direction * step * 360f / steps;
            active = BuildRotatedCross(angle);
            ApplyTiles(source, active, TileType.HazardJumpable);
            yield return new WaitForSeconds(stepDuration);
        }
        ClearTiles(source, active);
        Destroy(source);
        crossLaserActive = false;
    }

    private IEnumerator DesperationAndDefeat()
    {
        desperationStarted = true;
        crossLaserActive = true;
        if (battleRoutine != null) StopCoroutine(battleRoutine);
        battleRoutine = null;
        if (objectiveRoutine != null) StopCoroutine(objectiveRoutine);
        objectiveRoutine = null;
        activeObjective?.Deactivate();
        activeObjective = null;
        arenaCentre = CellWorld(new Vector2Int(tileGrid.Width / 2, tileGrid.Height / 2));
        DesperationRemainingNormalized = 1f;
        yield return MoveToArenaCentre(desperationReturnDuration);
        if (desperationAttackDelay > 0f) yield return new WaitForSeconds(desperationAttackDelay);

        float totalDesperationDuration = crossLaserWarningDuration +
                                         desperationLoopDurations.Sum(duration => Mathf.Max(.1f, duration));
        Coroutine meterRoutine = StartCoroutine(UpdateDesperationMeter(totalDesperationDuration));
        for (int loop = 0; loop < desperationLoopDurations.Length; loop++)
        {
            bool clockwise = loop % 2 == 0;
            yield return RotateCrossLaser(Mathf.Max(.1f, desperationLoopDurations[loop]), clockwise, loop == 0, true);
        }
        if (meterRoutine != null) StopCoroutine(meterRoutine);
        DesperationRemainingNormalized = 0f;

        Vector2Int deathCell = WorldToCell(transform.position);
        PositionBeam(transform.position);
        ApplyArea(this, beamTiles, deathCell, beamDamageFootprint, TileType.HazardUnjumpable);
        if (beamRoot != null) beamRoot.SetActive(true);
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        yield return FadeBoss(1f, 0f, defeatBeamDuration, Vector3.up * 4f);
        ClearArea(this, beamTiles);
        if (beamRoot != null) beamRoot.SetActive(false);
        SetBossPresentation(false);
        DefeatSequenceComplete = true;
        battleHud?.ShowVictory();
    }

    private IEnumerator MoveToArenaCentre(float duration)
    {
        Vector3 start = transform.position;
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = Quaternion.identity;
        float elapsed = 0f;
        duration = Mathf.Max(.05f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(start, arenaCentre, t);
            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }
        transform.SetPositionAndRotation(arenaCentre, endRotation);
    }

    private Coroutine RunPersistentAttack(IEnumerator routine)
    {
        if (attackHost == null) attackHost = BossAttackCoroutineHost.GetOrAdd(projectileRoot);
        return attackHost != null ? attackHost.Run(routine) : StartCoroutine(routine);
    }

    private IEnumerator UpdateDesperationMeter(float totalDuration)
    {
        float elapsed = 0f;
        totalDuration = Mathf.Max(.1f, totalDuration);
        while (elapsed < totalDuration && !DefeatSequenceComplete)
        {
            elapsed += Time.deltaTime;
            DesperationRemainingNormalized = 1f - Mathf.Clamp01(elapsed / totalDuration);
            yield return null;
        }
        DesperationRemainingNormalized = 0f;
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
        else TakeDamage(damagePerGreenTile);
    }

    public void TakeDamage(int amount)
    {
        if (IsDefeated || amount <= 0) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (CurrentHealth <= 0)
        {
            StartCoroutine(DesperationAndDefeat());
            return;
        }
        if (objectiveRoutine != null) StopCoroutine(objectiveRoutine);
        objectiveRoutine = StartCoroutine(SpawnObjectiveAfter(objectiveRespawnDelay));
    }

    private IEnumerator SpawnObjectiveAfter(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        objectiveRoutine = null;
        if (!HasEnteredArena || IsDefeated || activeObjective != null) yield break;
        List<FloorTile> candidates = tileGrid.GetComponentsInChildren<FloorTile>(true)
            .Where(tile => tile.StartingType != TileType.Wall && Vector3.Distance(tile.transform.position, transform.position) >= tileGrid.CellSize * 2f).ToList();
        if (candidates.Count == 0) yield break;
        FloorTile selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        activeObjective = selected.GetComponent<BossDamageObjective>() ?? selected.gameObject.AddComponent<BossDamageObjective>();
        activeObjective.Activate(selected, this, player);
    }

    private IEnumerator Cooldown()
    {
        if (sequenceCooldown > 0f) yield return new WaitForSeconds(sequenceCooldown);
    }

    private IEnumerator FadeBoss(float from, float to, float duration, Vector3 movement)
    {
        EnsureFadeMaterials();
        Vector3 start = transform.position;
        float elapsed = 0f;
        duration = Mathf.Max(.05f, duration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            float alpha = Mathf.Lerp(from, to, t);
            transform.position = start + movement * t;
            SetMaterialAlpha(alpha);
            yield return null;
        }
        SetMaterialAlpha(to);
    }

    private void EnsureFadeMaterials()
    {
        if (fadeMaterials != null) return;
        fadeMaterials = new Material[bossRenderers.Length][];
        for (int rendererIndex = 0; rendererIndex < bossRenderers.Length; rendererIndex++)
        {
            fadeMaterials[rendererIndex] = bossRenderers[rendererIndex].materials;
            foreach (Material material in fadeMaterials[rendererIndex]) ConfigureFadeMaterial(material);
        }
    }

    private void SetMaterialAlpha(float alpha)
    {
        foreach (Material[] materials in fadeMaterials)
        foreach (Material material in materials)
        {
            if (material == null) continue;
            Color colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            colour.a = alpha;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            if (material.HasProperty("_Color")) material.color = colour;
        }
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

    private void SetBossPresentation(bool visible)
    {
        foreach (Renderer renderer in bossRenderers) renderer.enabled = visible;
        foreach (Light light in GetComponentsInChildren<Light>(true)) light.enabled = visible;
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = visible;
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            if (visible) particles.Play(true); else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void PositionBeam(Vector3 worldPosition)
    {
        if (beamRoot != null) beamRoot.transform.position = new Vector3(worldPosition.x, 0f, worldPosition.z);
    }

    private GameObject CreateOverlaySource(string sourceName)
    {
        GameObject source = new(sourceName);
        source.transform.SetParent(projectileRoot, false);
        return source;
    }

    private List<FloorTile> BuildSquarePlusCross(Vector2Int centre)
    {
        List<FloorTile> result = new();
        for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++) AddTile(result, centre + new Vector2Int(x, y));
        AddTile(result, centre + Vector2Int.up * 2);
        AddTile(result, centre + Vector2Int.down * 2);
        AddTile(result, centre + Vector2Int.left * 2);
        AddTile(result, centre + Vector2Int.right * 2);
        return result;
    }

    private List<FloorTile> BuildLineThroughPlayer()
    {
        Vector2Int playerCell = WorldToCell(player.transform.position);
        float angle = UnityEngine.Random.Range(0f, Mathf.PI);
        Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
        List<FloorTile> result = new();
        int radius = Mathf.Max(tileGrid.Width, tileGrid.Height) * 2;
        AddRay(result, playerCell, direction, radius);
        AddRay(result, playerCell, -direction, radius);
        return result;
    }

    private List<FloorTile> BuildRotatedCross(float angleDegrees)
    {
        Vector2Int centre = new(tileGrid.Width / 2, tileGrid.Height / 2);
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector2 direction = new(Mathf.Cos(radians), Mathf.Sin(radians));
        Vector2 perpendicular = new(-direction.y, direction.x);
        List<FloorTile> result = new();
        int radius = Mathf.Max(tileGrid.Width, tileGrid.Height);
        AddRay(result, centre, direction, radius);
        AddRay(result, centre, -direction, radius);
        AddRay(result, centre, perpendicular, radius);
        AddRay(result, centre, -perpendicular, radius);
        return result;
    }

    private void AddRay(List<FloorTile> result, Vector2Int centre, Vector2 direction, int radius)
    {
        Vector2Int previous = new(int.MinValue, int.MinValue);
        for (int distance = 0; distance <= radius; distance++)
        {
            Vector2Int cell = centre + new Vector2Int(Mathf.RoundToInt(direction.x * distance), Mathf.RoundToInt(direction.y * distance));
            if (cell == previous) continue;
            previous = cell;
            AddTile(result, cell);
        }
    }

    private List<FloorTile> BuildLine(Vector2Int start, Vector2Int end)
    {
        List<FloorTile> result = new();
        int x = start.x, y = start.y;
        int dx = Mathf.Abs(end.x - x), sx = x < end.x ? 1 : -1;
        int dy = -Mathf.Abs(end.y - y), sy = y < end.y ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            AddTile(result, new Vector2Int(x, y));
            if (x == end.x && y == end.y) break;
            int twice = error * 2;
            if (twice >= dy) { error += dy; x += sx; }
            if (twice <= dx) { error += dx; y += sy; }
        }
        return result;
    }

    private void AddTile(List<FloorTile> result, Vector2Int cell)
    {
        FloorTile tile = tileGrid.GetTile(cell.x, cell.y);
        if (tile != null && !result.Contains(tile)) result.Add(tile);
    }

    private void ApplyArea(UnityEngine.Object source, List<FloorTile> list, Vector2Int centre, int size, TileType type)
    {
        list.Clear();
        int min = -(size / 2), max = min + size - 1;
        for (int y = min; y <= max; y++) for (int x = min; x <= max; x++)
        {
            FloorTile tile = tileGrid.GetTile(centre.x + x, centre.y + y);
            if (tile != null && tile.ApplyTileOverride(source, type, attackPriority)) list.Add(tile);
        }
    }

    private void ApplyTiles(UnityEngine.Object source, List<FloorTile> tiles, TileType type)
    {
        foreach (FloorTile tile in tiles) tile?.ApplyTileOverride(source, type, attackPriority);
    }

    private void ReplaceTiles(UnityEngine.Object source, List<FloorTile> tiles, TileType type)
    {
        foreach (FloorTile tile in tiles)
        {
            if (tile == null) continue;
            tile.RemoveTileOverride(source);
            tile.ApplyTileOverride(source, type, attackPriority);
        }
    }

    private static void ClearTiles(UnityEngine.Object source, List<FloorTile> tiles)
    {
        foreach (FloorTile tile in tiles) if (tile != null) tile.RemoveTileOverride(source);
        tiles.Clear();
    }

    private static void ClearArea(UnityEngine.Object source, List<FloorTile> tiles) => ClearTiles(source, tiles);

    private void CleanupAreas(List<GameObject> sources, List<List<FloorTile>> areas)
    {
        for (int index = 0; index < sources.Count; index++)
        {
            ClearTiles(sources[index], areas[index]);
            Destroy(sources[index]);
        }
    }

    private Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = tileGrid.transform.InverseTransformPoint(world);
        return new Vector2Int(Mathf.Clamp(Mathf.RoundToInt(local.x / tileGrid.CellSize), 0, tileGrid.Width - 1),
            Mathf.Clamp(Mathf.RoundToInt(local.z / tileGrid.CellSize), 0, tileGrid.Height - 1));
    }

    private Vector3 CellWorld(Vector2Int cell)
    {
        FloorTile tile = tileGrid.GetTile(cell.x, cell.y);
        Vector3 world = tile != null ? tile.transform.position : tileGrid.transform.TransformPoint(new Vector3(cell.x * tileGrid.CellSize, 0f, cell.y * tileGrid.CellSize));
        world.y += hoverHeight;
        return world;
    }

    private void OnDestroy()
    {
        ClearArea(this, beamTiles);
        activeObjective?.Deactivate();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(TileGrid grid, TopDownPlayer targetPlayer, PlayerLife life,
        BossAttackProjectile attackPrefab, Transform spawnedProjectileRoot, GameObject beam,
        BossBattleHud hud)
    {
        tileGrid = grid;
        player = targetPlayer;
        playerLife = life;
        playerAttackPrefab = attackPrefab;
        projectileRoot = spawnedProjectileRoot;
        beamRoot = beam;
        battleHud = hud;
    }

    public void ConfigureFastTestTimings()
    {
        entranceDuration = .6f;
        bossFadeDuration = .2f;
        defeatBeamDuration = .25f;
        desperationReturnDuration = .08f;
        desperationAttackDelay = .05f;
        sequenceCooldown = .02f;
        stackedPlacementInterval = .02f;
        stackedWarningDuration = .08f;
        stackedHazardDuration = .08f;
        longStrikeWarningDuration = .08f;
        longStrikeHazardDuration = .12f;
        lineWarningDuration = .07f;
        lineHazardDuration = .07f;
        regularLaserLoopDuration = .2f;
        crossLaserWarningDuration = .08f;
        laserStepsPerLoop = 8;
        desperationLoopDurations = new[] { .15f, .13f, .11f, .09f };
    }

    public void StopBattleForTest()
    {
        if (battleRoutine != null) StopCoroutine(battleRoutine);
        battleRoutine = null;
    }

    public void StartAngledLineForTest()
    {
        lineAttackCount = 1;
        lineWarningDuration = .12f;
        lineHazardDuration = .12f;
        StartCoroutine(AngledLineSequence());
    }

    public void StartRotatingCrossForTest()
    {
        RunPersistentAttack(RotateCrossLaser(.2f, true, true, false));
    }

    public void StartPersistentLongStrikeForTest()
    {
        longStrikeCount = 1;
        longStrikeWarningDuration = .08f;
        longStrikeHazardDuration = .1f;
        RunPersistentAttack(LongLivedStrikes());
    }
#endif
}
