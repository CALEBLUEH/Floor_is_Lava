using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DarkRedBossController : MonoBehaviour, IBossDamageTarget
{
    [Header("Scene references")]
    [SerializeField] private TileGrid tileGrid;
    [SerializeField] private TopDownPlayer player;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private BlueBossController blueBoss;
    [SerializeField] private BossAttackProjectile playerAttackPrefab;
    [SerializeField] private Transform projectileRoot;

    [Header("Health and entrance")]
    [SerializeField, Min(1)] private int maxHealth = 5;
    [SerializeField, Min(1)] private int damagePerGreenTile = 1;
    [SerializeField, Min(.1f)] private float playerAttackSpeed = 16f;
    [SerializeField, Min(0f)] private float delayAfterBlue = 2f;
    [SerializeField, Min(1f)] private float riseDepth = 12f;
    [SerializeField, Min(.05f)] private float riseDuration = .8f;
    [SerializeField, Min(0f)] private float entranceWarningDuration = 1f;
    [SerializeField, Range(1, 11)] private int entranceFootprint = 5;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float hoverHeight = 1.8f;
    [SerializeField, Min(.01f)] private float turnDuration = .2f;
    [SerializeField, Min(.05f)] private float moveSecondsPerCell = .11f;
    [SerializeField, Min(0f)] private float hopHeight = .55f;
    [SerializeField, Min(0f)] private float sequenceCooldown = 1.1f;

    [Header("Charge and bombs")]
    [SerializeField, Min(1)] private int chargeWallBounces = 2;
    [SerializeField, Min(.03f)] private float chargeSecondsPerCell = .08f;
    [SerializeField, Min(.05f)] private float bombDropInterval = 1f;
    [SerializeField, Min(0f)] private float bombWarningDuration = 1f;
    [SerializeField, Min(.05f)] private float bombHazardDuration = .65f;

    [Header("Fan attack")]
    [SerializeField, Min(1)] private int fanApproachTiles = 5;
    [SerializeField, Min(1)] private int fanPreferredDistance = 2;
    [SerializeField, Min(0f)] private float fanWarningDuration = 1f;
    [SerializeField, Min(.05f)] private float fanHazardDuration = .7f;

    [Header("Orange seekers")]
    [SerializeField, Min(.03f)] private float fastSeekerSecondsPerCell = .13f;
    [SerializeField, Min(.03f)] private float slowSeekerSecondsPerCell = .24f;
    [SerializeField, Min(0f)] private float seekerExplosionWarning = .5f;
    [SerializeField, Min(.05f)] private float seekerExplosionDuration = .65f;

    [Header("Objectives and overlays")]
    [SerializeField, Min(0f)] private float objectiveRespawnDelay = .65f;
    [SerializeField] private int attackPriority = 560;

    [Header("Defeat presentation")]
    [SerializeField, Min(.05f)] private float defeatFadeDuration = 1.5f;
    [SerializeField, Min(0f)] private float defeatRiseDistance = 4f;

    private Coroutine battleRoutine;
    private Coroutine objectiveRoutine;
    private BossDamageObjective activeObjective;
    private readonly List<FloorTile> entranceTiles = new();
    private BossAttackCoroutineHost attackHost;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDefeated => CurrentHealth <= 0;
    public bool HasEnteredArena { get; private set; }
    public Transform TargetTransform => transform;
    public event Action<int, int> HealthChanged;
    public event Action DarkRedDefeated;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        if (projectileRoot == null) projectileRoot = transform.parent != null ? transform.parent : transform;
        attackHost = BossAttackCoroutineHost.GetOrAdd(projectileRoot);
    }

    private void Start()
    {
        tileGrid?.RebuildLookup();
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        battleRoutine = StartCoroutine(WaitForBlueAndEnter());
    }

    private IEnumerator WaitForBlueAndEnter()
    {
        SetPresentationVisible(false);
        while (blueBoss != null && !blueBoss.IsDefeated) yield return null;
        if (delayAfterBlue > 0f) yield return new WaitForSeconds(delayAfterBlue);

        Vector2Int centre = new(tileGrid.Width / 2, tileGrid.Height / 2);
        Vector3 destination = CellWorld(centre);
        transform.position = destination - Vector3.up * riseDepth;
        ApplyArea(this, entranceTiles, centre, entranceFootprint, TileType.WarningUnjumpable);
        SetPresentationVisible(true);
        float elapsed = 0f;
        while (elapsed < riseDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / Mathf.Max(.01f, riseDuration)));
            transform.position = Vector3.Lerp(destination - Vector3.up * riseDepth, destination, t);
            transform.Rotate(Vector3.up, -540f * Time.deltaTime / Mathf.Max(.01f, riseDuration), Space.World);
            yield return null;
        }
        transform.position = destination;
        if (entranceWarningDuration > 0f) yield return new WaitForSeconds(entranceWarningDuration);
        ReplaceArea(this, entranceTiles, centre, entranceFootprint, TileType.HazardUnjumpable);
        yield return new WaitForSeconds(.55f);
        ClearArea(this, entranceTiles);
        HasEnteredArena = true;
        objectiveRoutine = StartCoroutine(SpawnObjectiveAfter(objectiveRespawnDelay));

        while (!IsDefeated && (playerLife == null || !playerLife.IsDefeated))
        {
            yield return RunPersistentAttack(ChargeAndBombs());
            yield return Cooldown();
            yield return RunPersistentAttack(FanAttack());
            yield return Cooldown();
            yield return RunPersistentAttack(SeekersAttack());
            yield return Cooldown();
        }
        battleRoutine = null;
    }

    private IEnumerator ChargeAndBombs()
    {
        Vector2Int cell = WorldToCell(transform.position);
        Vector2Int direction = DirectionToward(cell, WorldToCell(player.transform.position));
        if (direction == Vector2Int.zero) direction = Vector2Int.right;
        yield return RotateToward(direction);
        int bounces = 0;
        float lastBomb = -bombDropInterval;
        List<Vector2Int> bombs = new();
        while (bounces < chargeWallBounces && !IsDefeated)
        {
            Vector2Int next = cell + direction;
            bool hitX = next.x < 0 || next.x >= tileGrid.Width;
            bool hitY = next.y < 0 || next.y >= tileGrid.Height;
            if (hitX || hitY)
            {
                if (hitX) direction.x *= -1;
                if (hitY) direction.y *= -1;
                bounces++;
                yield return RotateToward(direction);
                continue;
            }
            cell = next;
            yield return MoveToCell(cell, chargeSecondsPerCell, false);
            if (WorldToCell(player.transform.position) == cell) player.ReportHazardHit(this, false);
            if (Time.time - lastBomb >= bombDropInterval)
            {
                bombs.Add(cell);
                lastBomb = Time.time;
            }
        }
        foreach (Vector2Int bomb in bombs) attackHost.Run(TimedAreaHazard(bomb, 3, bombWarningDuration, bombHazardDuration));
        if (bombs.Count > 0) yield return new WaitForSeconds(bombWarningDuration + bombHazardDuration);
    }

    private IEnumerator FanAttack()
    {
        Vector2Int current = WorldToCell(transform.position);
        Vector2Int playerCell = WorldToCell(player.transform.position);
        Vector2Int away = DirectionToward(playerCell, current);
        Vector2Int desired = playerCell + away * fanPreferredDistance;
        desired.x = Mathf.Clamp(desired.x, 0, tileGrid.Width - 1);
        desired.y = Mathf.Clamp(desired.y, 0, tileGrid.Height - 1);
        Vector2Int delta = desired - current;
        Vector2Int destination = current + new Vector2Int(
            Mathf.Clamp(delta.x, -fanApproachTiles, fanApproachTiles),
            Mathf.Clamp(delta.y, -fanApproachTiles, fanApproachTiles));
        yield return MoveToCell(destination, moveSecondsPerCell * Mathf.Max(1, Chebyshev(current, destination)), true);
        Vector2Int direction = DirectionToward(destination, WorldToCell(player.transform.position));
        yield return RotateToward(direction);

        object source = new GameObject("Dark Red Fan Overlay");
        ((GameObject)source).transform.SetParent(projectileRoot, false);
        List<FloorTile> tiles = BuildFan(destination, direction);
        foreach (FloorTile tile in tiles) tile.ApplyTileOverride((UnityEngine.Object)source, TileType.WarningJumpable, attackPriority);
        if (fanWarningDuration > 0f) yield return new WaitForSeconds(fanWarningDuration);
        foreach (FloorTile tile in tiles)
        {
            tile.RemoveTileOverride((UnityEngine.Object)source);
            tile.ApplyTileOverride((UnityEngine.Object)source, TileType.HazardJumpable, attackPriority);
        }
        yield return new WaitForSeconds(fanHazardDuration);
        foreach (FloorTile tile in tiles) if (tile != null) tile.RemoveTileOverride((UnityEngine.Object)source);
        Destroy((GameObject)source);
    }

    private IEnumerator SeekersAttack()
    {
        Coroutine fast = attackHost.Run(RunSeeker(new Vector2Int(0, 0), fastSeekerSecondsPerCell));
        Coroutine slow = attackHost.Run(RunSeeker(new Vector2Int(tileGrid.Width - 1, tileGrid.Height - 1), slowSeekerSecondsPerCell));
        yield return fast;
        yield return slow;
    }

    private IEnumerator RunSeeker(Vector2Int cell, float secondsPerCell)
    {
        GameObject source = new("Dark Red Orange Seeker");
        source.transform.SetParent(projectileRoot, false);
        FloorTile currentTile = null;
        while (!IsDefeated && Chebyshev(cell, WorldToCell(player.transform.position)) > 2)
        {
            if (currentTile != null) currentTile.RemoveTileOverride(source);
            currentTile = tileGrid.GetTile(cell.x, cell.y);
            currentTile?.ApplyTileOverride(source, TileType.WarningJumpable, attackPriority + 5);
            yield return new WaitForSeconds(secondsPerCell);
            cell += DirectionToward(cell, WorldToCell(player.transform.position));
            cell.x = Mathf.Clamp(cell.x, 0, tileGrid.Width - 1);
            cell.y = Mathf.Clamp(cell.y, 0, tileGrid.Height - 1);
        }
        if (currentTile != null) currentTile.RemoveTileOverride(source);
        Destroy(source);
        yield return TimedSeekerExplosion(cell);
    }

    private IEnumerator TimedSeekerExplosion(Vector2Int centre)
    {
        GameObject source = new("Dark Red Seeker Explosion");
        source.transform.SetParent(projectileRoot, false);
        List<FloorTile> tiles = BuildSquarePlusCross(centre);
        foreach (FloorTile tile in tiles) tile.ApplyTileOverride(source, TileType.WarningJumpable, attackPriority + 10);
        if (seekerExplosionWarning > 0f) yield return new WaitForSeconds(seekerExplosionWarning);
        foreach (FloorTile tile in tiles)
        {
            tile.RemoveTileOverride(source);
            tile.ApplyTileOverride(source, TileType.HazardJumpable, attackPriority + 10);
        }
        if (seekerExplosionDuration > 0f) yield return new WaitForSeconds(seekerExplosionDuration);
        foreach (FloorTile tile in tiles) if (tile != null) tile.RemoveTileOverride(source);
        Destroy(source);
    }

    private List<FloorTile> BuildSquarePlusCross(Vector2Int centre)
    {
        List<FloorTile> result = new();
        for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
            AddTile(result, centre + new Vector2Int(x, y));
        AddTile(result, centre + Vector2Int.up * 2);
        AddTile(result, centre + Vector2Int.down * 2);
        AddTile(result, centre + Vector2Int.left * 2);
        AddTile(result, centre + Vector2Int.right * 2);
        return result;
    }

    private void AddTile(List<FloorTile> result, Vector2Int cell)
    {
        FloorTile tile = tileGrid.GetTile(cell.x, cell.y);
        if (tile != null && !result.Contains(tile)) result.Add(tile);
    }

    private IEnumerator TimedAreaHazard(Vector2Int centre, int size, float warning, float hazard)
    {
        GameObject source = new("Dark Red Timed Hazard");
        source.transform.SetParent(projectileRoot, false);
        List<FloorTile> tiles = new();
        ApplyArea(source, tiles, centre, size, TileType.WarningUnjumpable);
        if (warning > 0f) yield return new WaitForSeconds(warning);
        ReplaceArea(source, tiles, centre, size, TileType.HazardUnjumpable);
        if (hazard > 0f) yield return new WaitForSeconds(hazard);
        ClearArea(source, tiles);
        Destroy(source);
    }

    private List<FloorTile> BuildFan(Vector2Int origin, Vector2Int direction)
    {
        List<FloorTile> result = new();
        Vector2Int perpendicular = new(-direction.y, direction.x);
        for (int distance = 1; distance <= 3; distance++)
        {
            int halfWidth = distance - 1;
            for (int offset = -halfWidth; offset <= halfWidth; offset++)
            {
                Vector2Int cell = origin + direction * distance + perpendicular * offset;
                FloorTile tile = tileGrid.GetTile(cell.x, cell.y);
                if (tile != null && !result.Contains(tile)) result.Add(tile);
            }
        }
        return result;
    }

    private IEnumerator MoveToCell(Vector2Int destination, float duration, bool turn)
    {
        Vector2Int current = WorldToCell(transform.position);
        if (turn) yield return RotateToward(destination - current);
        Vector3 start = transform.position;
        Vector3 end = CellWorld(destination);
        float elapsed = 0f;
        duration = Mathf.Max(.01f, duration);
        while (elapsed < duration && !IsDefeated)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            Vector3 position = Vector3.Lerp(start, end, t);
            position.y += Mathf.Sin(t * Mathf.PI) * hopHeight;
            transform.position = position;
            yield return null;
        }
        if (!IsDefeated) transform.position = end;
    }

    private IEnumerator RotateToward(Vector2Int direction)
    {
        if (direction == Vector2Int.zero) yield break;
        Vector3 world = tileGrid.transform.TransformDirection(new Vector3(direction.x, 0f, direction.y)).normalized;
        Quaternion start = transform.rotation;
        Quaternion end = Quaternion.LookRotation(world, Vector3.up);
        float elapsed = 0f;
        while (elapsed < turnDuration)
        {
            elapsed += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, elapsed / Mathf.Max(.01f, turnDuration)));
            yield return null;
        }
        transform.rotation = end;
    }

    private IEnumerator Cooldown()
    {
        if (sequenceCooldown > 0f) yield return new WaitForSeconds(sequenceCooldown);
    }

    private Coroutine RunPersistentAttack(IEnumerator routine)
    {
        if (attackHost == null) attackHost = BossAttackCoroutineHost.GetOrAdd(projectileRoot);
        return attackHost != null ? attackHost.Run(routine) : StartCoroutine(routine);
    }

    private IEnumerator SpawnObjectiveAfter(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);
        objectiveRoutine = null;
        SpawnObjective();
    }

    private void SpawnObjective()
    {
        if (!HasEnteredArena || IsDefeated || activeObjective != null) return;
        List<FloorTile> candidates = tileGrid.GetComponentsInChildren<FloorTile>(true)
            .Where(tile => tile.StartingType != TileType.Wall && Vector3.Distance(tile.transform.position, transform.position) >= tileGrid.CellSize * 2f).ToList();
        if (candidates.Count == 0) return;
        FloorTile selected = candidates[UnityEngine.Random.Range(0, candidates.Count)];
        activeObjective = selected.GetComponent<BossDamageObjective>() ?? selected.gameObject.AddComponent<BossDamageObjective>();
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
        else TakeDamage(damagePerGreenTile);
    }

    public void TakeDamage(int amount)
    {
        if (IsDefeated || amount <= 0) return;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (IsDefeated)
        {
            if (battleRoutine != null) StopCoroutine(battleRoutine);
            if (objectiveRoutine != null) StopCoroutine(objectiveRoutine);
            activeObjective?.Deactivate();
            activeObjective = null;
            DarkRedDefeated?.Invoke();
            StartCoroutine(RiseAndFade());
            return;
        }
        if (objectiveRoutine != null) StopCoroutine(objectiveRoutine);
        objectiveRoutine = StartCoroutine(SpawnObjectiveAfter(objectiveRespawnDelay));
    }

    private IEnumerator RiseAndFade()
    {
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
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
        SetPresentationVisible(false);
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

    private void SetPresentationVisible(bool visible)
    {
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
        foreach (Light light in GetComponentsInChildren<Light>(true)) light.enabled = visible;
        foreach (Collider collider in GetComponentsInChildren<Collider>(true)) collider.enabled = visible;
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            if (visible) particles.Play(true); else particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
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

    private void ReplaceArea(UnityEngine.Object source, List<FloorTile> list, Vector2Int centre, int size, TileType type)
    {
        ClearArea(source, list);
        ApplyArea(source, list, centre, size, type);
    }

    private static void ClearArea(UnityEngine.Object source, List<FloorTile> list)
    {
        foreach (FloorTile tile in list) if (tile != null) tile.RemoveTileOverride(source);
        list.Clear();
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

    private static Vector2Int DirectionToward(Vector2Int from, Vector2Int to) => new(Math.Sign(to.x - from.x), Math.Sign(to.y - from.y));
    private static int Chebyshev(Vector2Int a, Vector2Int b) => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private void OnDestroy()
    {
        ClearArea(this, entranceTiles);
        activeObjective?.Deactivate();
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(TileGrid grid, TopDownPlayer targetPlayer, PlayerLife life, BlueBossController blue,
        BossAttackProjectile attackPrefab, Transform spawnedProjectileRoot)
    {
        tileGrid = grid;
        player = targetPlayer;
        playerLife = life;
        blueBoss = blue;
        playerAttackPrefab = attackPrefab;
        projectileRoot = spawnedProjectileRoot;
    }

    public void StartSeekerExplosionForTest(Vector2Int cell) => StartCoroutine(TimedSeekerExplosion(cell));
#endif
}
