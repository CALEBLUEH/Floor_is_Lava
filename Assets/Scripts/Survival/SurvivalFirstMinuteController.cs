using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public enum SurvivalEnemyKind
{
    Red,
    Yellow,
    DarkOrange,
    Brown,
    Gold,
    Purple
}

[DisallowMultipleComponent]
public sealed class SurvivalFirstMinuteController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private TileGrid grid;
    [SerializeField] private TopDownPlayer player;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private GameplayUIController gameplayUi;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private Transform effectRoot;

    [Header("Enemy prefabs")]
    [SerializeField] private SurvivalEnemy redEnemyPrefab;
    [SerializeField] private SurvivalEnemy yellowEnemyPrefab;
    [SerializeField] private SurvivalEnemy darkOrangeEnemyPrefab;
    [SerializeField] private SurvivalEnemy brownEnemyPrefab;
    [SerializeField] private SurvivalEnemy goldEnemyPrefab;
    [SerializeField] private SurvivalEnemy purpleEnemyPrefab;

    [Header("Wave timing")]
    [SerializeField, Min(1f)] private float waveDuration = 60f;
    [SerializeField, Min(0f)] private float initialEnemyDelay = 1.5f;
    [SerializeField, Min(.1f)] private float firstWaveSpawnInterval = 6f;
    [SerializeField, Min(.1f)] private float secondWaveSpawnInterval = 5f;
    [SerializeField, Min(.1f)] private float thirdWaveSpawnInterval = 7f;
    [SerializeField, Min(.1f)] private float fourthWaveSpawnInterval = 5f;
    [SerializeField, Min(.1f)] private float fifthWaveSpawnInterval = 7f;
    [SerializeField, Min(1f)] private float minimumSpawnDistanceTiles = 5f;
    [SerializeField, Min(.05f)] private float enemySpawnWarningDuration = 1f;
    [SerializeField, Min(0)] private int healingPerNewMinute = 1;

    [Header("Enemy balance")]
    [SerializeField, Min(1)] private int redEnemyHealth = 2;
    [SerializeField, Min(.1f)] private float redEnemyMoveSpeed = 3.5f;
    [SerializeField, Min(1)] private int yellowEnemyHealth = 1;
    [SerializeField, Min(.1f)] private float yellowEnemyMoveSpeed = 5.25f;
    [SerializeField, Min(1)] private int darkOrangeEnemyHealth = 2;
    [SerializeField, Min(.1f)] private float darkOrangeEnemyMoveSpeed = .1f;
    [SerializeField, Min(1)] private int brownEnemyHealth = 2;
    [SerializeField, Min(.1f)] private float brownEnemyMoveSpeed = 5f;
    [SerializeField, Min(1)] private int goldEnemyHealth = 3;
    [SerializeField, Min(.1f)] private float goldEnemyMoveSpeed = .1f;
    [SerializeField, Min(1)] private int purpleEnemyHealth = 5;
    [SerializeField, Min(.1f)] private float purpleEnemyMoveSpeed = 1.8f;

    [Header("Wave 1 and 2 floor hazards")]
    [SerializeField, Min(.1f)] private float floorHazardInterval = 4f;
    [SerializeField, Min(1)] private int firstWaveHazardTileCount = 2;
    [SerializeField, Min(1)] private int secondWaveBlockCount = 3;
    [SerializeField, Min(1)] private int secondWaveBlockSize = 2;
    [SerializeField, Min(.05f)] private float floorWarningDuration = 1f;
    [SerializeField, Min(.05f)] private float floorHazardDuration = 2f;

    [Header("Wave 3 tracking bombs")]
    [SerializeField, Min(.1f)] private float trackingBombInterval = 5f;
    [SerializeField, Min(.03f)] private float trackingBombSecondsPerCell = .16f;
    [SerializeField, Min(1)] private int trackingBombTriggerDistanceTiles = 2;
    [SerializeField, Min(.05f)] private float trackingBombWarningDuration = .5f;
    [SerializeField, Min(.05f)] private float trackingBombHazardDuration = .65f;

    [Header("Wave 4 shrinking arena")]
    [SerializeField, Min(1)] private int fourthWaveSafeAreaSize = 11;
    [SerializeField, Min(.05f)] private float fourthWaveWarningDuration = 1f;

    [Header("Wave 5 line lasers")]
    [SerializeField, Min(.1f)] private float lineLaserInterval = 3f;
    [SerializeField, Min(.05f)] private float lineLaserWarningDuration = 1f;
    [SerializeField, Min(.05f)] private float lineLaserHazardDuration = .65f;

    [Header("Wave 6+ endless difficulty")]
    [SerializeField, Min(.1f)] private float lateGameStartingSpawnInterval = 5f;
    [SerializeField, Min(0f)] private float lateGameSpawnIntervalDecreasePerMinute = .35f;
    [SerializeField, Min(.1f)] private float lateGameMinimumSpawnInterval = 1.25f;
    [SerializeField, Min(1)] private int lateGameMaximumAliveEnemies = 30;
    [SerializeField, Min(1)] private int lateGameHealthIncreaseEveryMinutes = 2;
    [SerializeField, Min(0)] private int lateGameHealthIncreaseAmount = 1;
    [SerializeField, Min(0f)] private float lateGameSpeedIncreasePerMinute = .08f;
    [SerializeField, Min(1)] private int lateGameStartingSafeAreaSize = 19;
    [SerializeField, Min(0)] private int lateGameSafeAreaShrinkPerMinute = 2;
    [SerializeField, Min(1)] private int lateGameMinimumSafeAreaSize = 9;
    [SerializeField, Min(.05f)] private float lateGameArenaWarningDuration = 1f;
    [SerializeField, Min(.1f)] private float lateGameBombInterval = 5f;
    [SerializeField, Min(.1f)] private float lateGameLineLaserInterval = 5f;
    [SerializeField, Min(0f)] private float lateGameFootstepCooldown = .1f;
    [SerializeField, Min(.05f)] private float lateGameFootstepWarningDuration = 1f;
    [SerializeField, Min(.05f)] private float lateGameFootstepHazardDuration = 2f;

    [Header("Tile overlays")]
    [SerializeField] private int overlayPriority = 300;

    private readonly List<FloorTile> persistentWaveTiles = new();
    private float nextEnemySpawnTime;
    private float nextFloorHazardTime;
    private float nextBombTime;
    private float nextLineLaserTime;
    private float nextFootstepTime;
    private Vector2Int lastFootstepCell = new(int.MinValue, int.MinValue);
    private int currentWave;
    private int spawnSequence;
    private int waveGeneration;
    private bool scheduleStarted;
    private bool doubleSpeed;
    private GameObject persistentWaveSource;

    public int CurrentWave => currentWave;
    public bool IsDoubleSpeed => doubleSpeed;
    public float LastSpawnDistanceTiles { get; private set; }
    public int LastFloorHazardTileCount { get; private set; }
    public int TrackingBombsStarted { get; private set; }
    public int LineLasersStarted { get; private set; }
    public int FootstepHazardsStarted { get; private set; }
    public int CurrentLateGameSafeAreaSize => currentWave >= 6 ? LateGameSafeAreaSize(currentWave) : 0;
    public float CurrentEnemySpawnInterval => SpawnIntervalForWave(Mathf.Max(1, currentWave));

    private void Awake()
    {
        if (grid == null) grid = FindAnyObjectByType<TileGrid>();
        if (player == null) player = FindAnyObjectByType<TopDownPlayer>();
        if (playerLife == null && player != null) playerLife = player.GetComponent<PlayerLife>();
        if (gameplayUi == null) gameplayUi = FindAnyObjectByType<GameplayUIController>();
    }

    private void Update()
    {
        if (gameplayUi == null || !gameplayUi.IsGameStarted || player == null ||
            playerLife != null && playerLife.IsDefeated) return;

        float elapsed = gameplayUi.ElapsedTime;
        int wave = Mathf.FloorToInt(elapsed / waveDuration) + 1;
        if (!scheduleStarted || wave != currentWave) EnterWave(wave, scheduleStarted);
        if (elapsed >= nextEnemySpawnTime)
        {
            if (wave < 6 || CountAliveEnemies() < lateGameMaximumAliveEnemies)
                BeginEnemySpawn(ChooseEnemyForWave(wave));
            nextEnemySpawnTime = elapsed + SpawnIntervalForWave(wave);
        }

        if ((wave == 1 || wave == 2) && elapsed >= nextFloorHazardTime)
        {
            if (wave == 1) BeginFloorHazard(ChooseDistinctTiles(firstWaveHazardTileCount), "Two Tile Floor Hazard");
            else BeginFloorHazard(ChooseRandomBlocks(secondWaveBlockCount, secondWaveBlockSize), "Three 2x2 Floor Hazards");
            nextFloorHazardTime = elapsed + floorHazardInterval;
        }

        if ((wave == 3 || wave >= 7) && elapsed >= nextBombTime)
        {
            TrackingBombsStarted++;
            StartCoroutine(TrackingBomb(waveGeneration));
            nextBombTime = elapsed + (wave >= 7 ? lateGameBombInterval : trackingBombInterval);
        }

        if ((wave == 5 || wave >= 8) && elapsed >= nextLineLaserTime)
        {
            LineLasersStarted++;
            StartCoroutine(AngledLineLaser(waveGeneration));
            nextLineLaserTime = elapsed + (wave >= 8 ? lateGameLineLaserInterval : lineLaserInterval);
        }

        if (wave >= 9) UpdateFootstepHazard(elapsed);
    }

    private void EnterWave(int wave, bool healPlayer)
    {
        ClearPersistentWaveHazard();
        scheduleStarted = true;
        currentWave = Mathf.Max(1, wave);
        waveGeneration++;
        spawnSequence = 0;
        gameplayUi.SetWave(currentWave, currentWave);
        if (healPlayer && healingPerNewMinute > 0) playerLife?.Heal(healingPerNewMinute);

        float elapsed = gameplayUi.ElapsedTime;
        nextEnemySpawnTime = elapsed + initialEnemyDelay;
        nextFloorHazardTime = elapsed + floorHazardInterval;
        nextBombTime = elapsed + (currentWave >= 7 ? lateGameBombInterval : trackingBombInterval);
        nextLineLaserTime = elapsed + (currentWave >= 8 ? lateGameLineLaserInterval : lineLaserInterval);
        nextFootstepTime = elapsed;
        lastFootstepCell = new Vector2Int(int.MinValue, int.MinValue);
        if (currentWave == 4)
            StartCoroutine(ActivateOuterArena(waveGeneration, currentWave, fourthWaveSafeAreaSize,
                fourthWaveWarningDuration, "Wave 4 Outer Arena Hazard"));
        else if (currentWave >= 6)
            StartCoroutine(ActivateOuterArena(waveGeneration, currentWave, LateGameSafeAreaSize(currentWave),
                lateGameArenaWarningDuration, $"Wave {currentWave} Shrinking Arena"));
    }

    public void SkipToNextMinute()
    {
        if (gameplayUi == null) return;
        float nextMinute = (Mathf.Floor(gameplayUi.ElapsedTime / waveDuration) + 1f) * waveDuration;
        gameplayUi.SetElapsedTime(nextMinute);
        EnterWave(Mathf.FloorToInt(nextMinute / waveDuration) + 1, true);
    }

    public void ToggleDoubleSpeed()
    {
        doubleSpeed = !doubleSpeed;
        ApplyTimeScale(doubleSpeed ? 2f : 1f);
    }

    private void ApplyTimeScale(float scale)
    {
        if (PauseMenuController.Instance != null) PauseMenuController.Instance.SetResumeTimeScale(scale);
        else Time.timeScale = scale;
    }

    private void BeginEnemySpawn(SurvivalEnemyKind kind)
    {
        FloorTile tile = ChooseSpawnTile();
        SurvivalEnemy prefab = PrefabFor(kind);
        if (tile == null || prefab == null) return;

        GameObject telegraphObject = new($"{kind} Enemy Spawn Warning", typeof(SurvivalEnemySpawnTelegraph));
        telegraphObject.transform.SetParent(effectRoot, false);
        (int health, float speed) = ScaledBalanceFor(kind);
        telegraphObject.GetComponent<SurvivalEnemySpawnTelegraph>().Initialize(
            tile, prefab, enemyRoot, effectRoot, grid, player, health, speed,
            enemySpawnWarningDuration, overlayPriority);
    }

    private SurvivalEnemyKind ChooseEnemyForWave(int wave)
    {
        int index = spawnSequence++;
        return wave switch
        {
            1 => SurvivalEnemyKind.Red,
            2 => index % 2 == 0 ? SurvivalEnemyKind.Yellow : SurvivalEnemyKind.Red,
            3 => SurvivalEnemyKind.DarkOrange,
            4 => index % 2 == 0 ? SurvivalEnemyKind.Brown : SurvivalEnemyKind.Red,
            5 => index % 3 == 0 ? SurvivalEnemyKind.Gold : SurvivalEnemyKind.Purple,
            _ => (SurvivalEnemyKind)Random.Range(0, 6)
        };
    }

    private SurvivalEnemy PrefabFor(SurvivalEnemyKind kind) => kind switch
    {
        SurvivalEnemyKind.Yellow => yellowEnemyPrefab,
        SurvivalEnemyKind.DarkOrange => darkOrangeEnemyPrefab,
        SurvivalEnemyKind.Brown => brownEnemyPrefab,
        SurvivalEnemyKind.Gold => goldEnemyPrefab,
        SurvivalEnemyKind.Purple => purpleEnemyPrefab,
        _ => redEnemyPrefab
    };

    private (int health, float speed) BalanceFor(SurvivalEnemyKind kind) => kind switch
    {
        SurvivalEnemyKind.Yellow => (yellowEnemyHealth, yellowEnemyMoveSpeed),
        SurvivalEnemyKind.DarkOrange => (darkOrangeEnemyHealth, darkOrangeEnemyMoveSpeed),
        SurvivalEnemyKind.Brown => (brownEnemyHealth, brownEnemyMoveSpeed),
        SurvivalEnemyKind.Gold => (goldEnemyHealth, goldEnemyMoveSpeed),
        SurvivalEnemyKind.Purple => (purpleEnemyHealth, purpleEnemyMoveSpeed),
        _ => (redEnemyHealth, redEnemyMoveSpeed)
    };

    private (int health, float speed) ScaledBalanceFor(SurvivalEnemyKind kind)
    {
        (int health, float speed) = BalanceFor(kind);
        if (currentWave < 6) return (health, speed);

        int lateMinutes = currentWave - 6;
        int healthSteps = lateGameHealthIncreaseEveryMinutes > 0
            ? lateMinutes / lateGameHealthIncreaseEveryMinutes
            : 0;
        health += healthSteps * lateGameHealthIncreaseAmount;
        speed *= 1f + lateMinutes * lateGameSpeedIncreasePerMinute;
        return (Mathf.Max(1, health), Mathf.Max(.1f, speed));
    }

    private float SpawnIntervalForWave(int wave) => wave switch
    {
        2 => secondWaveSpawnInterval,
        3 => thirdWaveSpawnInterval,
        4 => fourthWaveSpawnInterval,
        5 => fifthWaveSpawnInterval,
        >= 6 => Mathf.Max(lateGameMinimumSpawnInterval,
            lateGameStartingSpawnInterval - (wave - 6) * lateGameSpawnIntervalDecreasePerMinute),
        _ => firstWaveSpawnInterval
    };

    private int CountAliveEnemies() => enemyRoot != null
        ? enemyRoot.GetComponentsInChildren<SurvivalEnemy>().Length
        : 0;

    private int LateGameSafeAreaSize(int wave)
    {
        int size = lateGameStartingSafeAreaSize - Mathf.Max(0, wave - 6) * lateGameSafeAreaShrinkPerMinute;
        return Mathf.Clamp(size, lateGameMinimumSafeAreaSize, Mathf.Min(grid.Width, grid.Height));
    }

    private void BeginFloorHazard(FloorTile[] tiles, string objectName)
    {
        if (tiles.Length == 0) return;
        LastFloorHazardTileCount = tiles.Length;
        GameObject pulseObject = new(objectName, typeof(SurvivalFloorHazardPulse));
        pulseObject.transform.SetParent(effectRoot, false);
        pulseObject.GetComponent<SurvivalFloorHazardPulse>().Initialize(
            tiles, floorWarningDuration, floorHazardDuration, overlayPriority);
    }

    private IEnumerator TrackingBomb(int generation)
    {
        FloorTile start = ChooseSpawnTile();
        if (start == null) yield break;
        GameObject source = new("Survival Tracking Bomb");
        source.transform.SetParent(effectRoot, false);
        Vector2Int cell = WorldToCell(start.transform.position);
        FloorTile currentTile = null;

        while (IsBombWave(generation) &&
               Chebyshev(cell, WorldToCell(player.transform.position)) > trackingBombTriggerDistanceTiles)
        {
            currentTile?.RemoveTileOverride(source);
            currentTile = grid.GetTile(cell.x, cell.y);
            currentTile?.ApplyTileOverride(source, TileType.WarningJumpable, overlayPriority + 10);
            yield return new WaitForSeconds(trackingBombSecondsPerCell);
            cell += DirectionToward(cell, WorldToCell(player.transform.position));
            cell.x = Mathf.Clamp(cell.x, 0, grid.Width - 1);
            cell.y = Mathf.Clamp(cell.y, 0, grid.Height - 1);
        }

        currentTile?.RemoveTileOverride(source);
        if (!IsBombWave(generation))
        {
            Destroy(source);
            yield break;
        }

        List<FloorTile> explosion = BuildSquarePlusCross(cell);
        ApplyTiles(source, explosion, TileType.WarningJumpable, overlayPriority + 20);
        yield return new WaitForSeconds(trackingBombWarningDuration);
        ReplaceTiles(source, explosion, TileType.HazardJumpable, overlayPriority + 20);
        yield return new WaitForSeconds(trackingBombHazardDuration);
        ClearTiles(source, explosion);
        Destroy(source);
    }

    private bool IsBombWave(int generation) =>
        generation == waveGeneration && (currentWave == 3 || currentWave >= 7);

    private IEnumerator ActivateOuterArena(int generation, int expectedWave, int requestedSafeSize,
        float warningDuration, string sourceName)
    {
        GameObject source = new(sourceName);
        source.transform.SetParent(effectRoot, false);
        List<FloorTile> tiles = new();
        persistentWaveSource = source;
        int safeSize = Mathf.Clamp(requestedSafeSize, 1, Mathf.Min(grid.Width, grid.Height));
        int safeMinX = (grid.Width - safeSize) / 2;
        int safeMinY = (grid.Height - safeSize) / 2;
        int safeMaxX = safeMinX + safeSize - 1;
        int safeMaxY = safeMinY + safeSize - 1;
        for (int y = 0; y < grid.Height; y++)
        for (int x = 0; x < grid.Width; x++)
        {
            if (x >= safeMinX && x <= safeMaxX && y >= safeMinY && y <= safeMaxY) continue;
            FloorTile tile = grid.GetTile(x, y);
            if (tile != null && tile.StartingType != TileType.Wall) tiles.Add(tile);
        }

        persistentWaveTiles.Clear();
        persistentWaveTiles.AddRange(tiles);
        ApplyTiles(source, tiles, TileType.WarningJumpable, overlayPriority + 30);
        yield return new WaitForSeconds(warningDuration);
        if (generation == waveGeneration && currentWave == expectedWave && source != null)
            ReplaceTiles(source, tiles, TileType.HazardJumpable, overlayPriority + 30);
        else
        {
            ClearTiles(source, tiles);
            if (source != null) Destroy(source);
            if (persistentWaveSource == source)
            {
                persistentWaveSource = null;
                persistentWaveTiles.Clear();
            }
        }
    }

    private IEnumerator AngledLineLaser(int generation)
    {
        GameObject source = new("Survival Angled Line Laser");
        source.transform.SetParent(effectRoot, false);
        List<FloorTile> line = BuildLineThroughPlayer();
        ApplyTiles(source, line, TileType.WarningJumpable, overlayPriority + 40);
        yield return new WaitForSeconds(lineLaserWarningDuration);
        if (generation == waveGeneration)
            ReplaceTiles(source, line, TileType.HazardJumpable, overlayPriority + 40);
        yield return new WaitForSeconds(lineLaserHazardDuration);
        ClearTiles(source, line);
        Destroy(source);
    }

    private void UpdateFootstepHazard(float elapsed)
    {
        if (elapsed < nextFootstepTime) return;
        Vector2Int cell = WorldToCell(player.transform.position);
        if (cell == lastFootstepCell) return;
        lastFootstepCell = cell;
        nextFootstepTime = elapsed + lateGameFootstepCooldown;
        FloorTile tile = grid.GetTile(cell.x, cell.y);
        if (tile == null || tile.StartingType == TileType.Wall) return;

        FootstepHazardsStarted++;
        GameObject pulseObject = new("Player Footstep Hazard", typeof(SurvivalFloorHazardPulse));
        pulseObject.transform.SetParent(effectRoot, false);
        pulseObject.GetComponent<SurvivalFloorHazardPulse>().Initialize(
            new[] { tile }, lateGameFootstepWarningDuration, lateGameFootstepHazardDuration,
            overlayPriority + 50);
    }

    private FloorTile ChooseSpawnTile()
    {
        if (grid == null || player == null) return null;
        Vector3 local = grid.transform.InverseTransformPoint(player.transform.position);
        Vector2 playerCell = new(local.x / grid.CellSize, local.z / grid.CellSize);
        float minimumSqr = minimumSpawnDistanceTiles * minimumSpawnDistanceTiles;
        FloorTile fallback = null;
        float farthestSqr = -1f;
        for (int attempt = 0; attempt < 80; attempt++)
        {
            int x = Random.Range(0, grid.Width);
            int y = Random.Range(0, grid.Height);
            FloorTile candidate = grid.GetTile(x, y);
            if (candidate == null || candidate.StartingType == TileType.Wall) continue;
            float distanceSqr = (new Vector2(x, y) - playerCell).sqrMagnitude;
            if (distanceSqr >= minimumSqr)
            {
                LastSpawnDistanceTiles = Mathf.Sqrt(distanceSqr);
                return candidate;
            }
            if (distanceSqr > farthestSqr)
            {
                fallback = candidate;
                farthestSqr = distanceSqr;
            }
        }
        LastSpawnDistanceTiles = farthestSqr > 0f ? Mathf.Sqrt(farthestSqr) : 0f;
        return fallback;
    }

    private FloorTile[] ChooseDistinctTiles(int count)
    {
        List<FloorTile> chosen = new(Mathf.Max(1, count));
        HashSet<Vector2Int> coordinates = new();
        int attempts = 0;
        while (chosen.Count < count && attempts++ < 200)
        {
            Vector2Int coordinate = new(Random.Range(0, grid.Width), Random.Range(0, grid.Height));
            if (!coordinates.Add(coordinate)) continue;
            FloorTile tile = grid.GetTile(coordinate.x, coordinate.y);
            if (tile != null && tile.StartingType != TileType.Wall) chosen.Add(tile);
        }
        return chosen.ToArray();
    }

    private FloorTile[] ChooseRandomBlocks(int blockCount, int blockSize)
    {
        List<FloorTile> result = new();
        HashSet<Vector2Int> coordinates = new();
        int size = Mathf.Clamp(blockSize, 1, Mathf.Min(grid.Width, grid.Height));
        int block = 0;
        int attempts = 0;
        while (block < blockCount && attempts++ < 200)
        {
            int startX = Random.Range(0, Mathf.Max(1, grid.Width - size + 1));
            int startY = Random.Range(0, Mathf.Max(1, grid.Height - size + 1));
            bool overlaps = false;
            for (int y = 0; y < size && !overlaps; y++)
            for (int x = 0; x < size; x++)
                if (coordinates.Contains(new Vector2Int(startX + x, startY + y))) { overlaps = true; break; }
            if (overlaps) continue;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2Int coordinate = new(startX + x, startY + y);
                coordinates.Add(coordinate);
                FloorTile tile = grid.GetTile(coordinate.x, coordinate.y);
                if (tile != null && tile.StartingType != TileType.Wall) result.Add(tile);
            }
            block++;
        }
        return result.ToArray();
    }

    private List<FloorTile> BuildSquarePlusCross(Vector2Int centre)
    {
        List<FloorTile> result = new();
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++) AddTile(result, centre + new Vector2Int(x, y));
        AddTile(result, centre + Vector2Int.up * 2);
        AddTile(result, centre + Vector2Int.down * 2);
        AddTile(result, centre + Vector2Int.left * 2);
        AddTile(result, centre + Vector2Int.right * 2);
        return result;
    }

    private List<FloorTile> BuildLineThroughPlayer()
    {
        Vector2Int playerCell = WorldToCell(player.transform.position);
        float angle = Random.Range(0f, Mathf.PI);
        Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
        List<FloorTile> result = new();
        int radius = Mathf.Max(grid.Width, grid.Height) * 2;
        AddRay(result, playerCell, direction, radius);
        AddRay(result, playerCell, -direction, radius);
        return result;
    }

    private void AddRay(List<FloorTile> result, Vector2Int centre, Vector2 direction, int radius)
    {
        Vector2Int previous = new(int.MinValue, int.MinValue);
        for (int distance = 0; distance <= radius; distance++)
        {
            Vector2Int cell = centre + new Vector2Int(
                Mathf.RoundToInt(direction.x * distance), Mathf.RoundToInt(direction.y * distance));
            if (cell == previous) continue;
            previous = cell;
            AddTile(result, cell);
        }
    }

    private void AddTile(List<FloorTile> result, Vector2Int cell)
    {
        FloorTile tile = grid.GetTile(cell.x, cell.y);
        if (tile != null && !result.Contains(tile)) result.Add(tile);
    }

    private static void ApplyTiles(Object source, List<FloorTile> tiles, TileType type, int priority)
    {
        foreach (FloorTile tile in tiles) tile?.ApplyTileOverride(source, type, priority);
    }

    private static void ReplaceTiles(Object source, List<FloorTile> tiles, TileType type, int priority)
    {
        foreach (FloorTile tile in tiles)
        {
            if (tile == null) continue;
            tile.RemoveTileOverride(source);
            tile.ApplyTileOverride(source, type, priority);
        }
    }

    private static void ClearTiles(Object source, List<FloorTile> tiles)
    {
        foreach (FloorTile tile in tiles) if (tile != null) tile.RemoveTileOverride(source);
    }

    private void ClearPersistentWaveHazard()
    {
        if (persistentWaveSource != null)
        {
            ClearTiles(persistentWaveSource, persistentWaveTiles);
            Destroy(persistentWaveSource);
        }
        persistentWaveSource = null;
        persistentWaveTiles.Clear();
    }

    private Vector2Int WorldToCell(Vector3 world)
    {
        Vector3 local = grid.transform.InverseTransformPoint(world);
        return new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt(local.x / grid.CellSize), 0, grid.Width - 1),
            Mathf.Clamp(Mathf.RoundToInt(local.z / grid.CellSize), 0, grid.Height - 1));
    }

    private static Vector2Int DirectionToward(Vector2Int from, Vector2Int to) =>
        new(System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y));

    private static int Chebyshev(Vector2Int a, Vector2Int b) =>
        Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

    private void OnDisable()
    {
        ClearPersistentWaveHazard();
        if (doubleSpeed || Time.timeScale > 1f)
        {
            doubleSpeed = false;
            ApplyTimeScale(1f);
        }
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(TileGrid tileGrid, TopDownPlayer targetPlayer, GameplayUIController ui,
        TMP_Text waveLabel, SurvivalEnemy red, SurvivalEnemy yellow, SurvivalEnemy darkOrange,
        SurvivalEnemy brown, SurvivalEnemy gold, SurvivalEnemy purple, Transform enemies, Transform effects)
    {
        grid = tileGrid;
        player = targetPlayer;
        playerLife = player != null ? player.GetComponent<PlayerLife>() : null;
        gameplayUi = ui;
        waveText = waveLabel;
        redEnemyPrefab = red;
        yellowEnemyPrefab = yellow;
        darkOrangeEnemyPrefab = darkOrange;
        brownEnemyPrefab = brown;
        goldEnemyPrefab = gold;
        purpleEnemyPrefab = purple;
        enemyRoot = enemies;
        effectRoot = effects;
    }

    public void ConfigureFastTestTimings()
    {
        initialEnemyDelay = .1f;
        firstWaveSpawnInterval = .15f;
        secondWaveSpawnInterval = .15f;
        thirdWaveSpawnInterval = .15f;
        fourthWaveSpawnInterval = .15f;
        fifthWaveSpawnInterval = .15f;
        enemySpawnWarningDuration = .1f;
        floorHazardInterval = .2f;
        floorWarningDuration = .15f;
        floorHazardDuration = .15f;
        trackingBombInterval = .2f;
        trackingBombSecondsPerCell = .02f;
        trackingBombWarningDuration = .1f;
        trackingBombHazardDuration = .1f;
        fourthWaveWarningDuration = .1f;
        lineLaserInterval = .2f;
        lineLaserWarningDuration = .1f;
        lineLaserHazardDuration = .1f;
        lateGameStartingSpawnInterval = .15f;
        lateGameSpawnIntervalDecreasePerMinute = .01f;
        lateGameMinimumSpawnInterval = .05f;
        lateGameArenaWarningDuration = .1f;
        lateGameBombInterval = .2f;
        lateGameLineLaserInterval = .2f;
        lateGameFootstepCooldown = 0f;
        lateGameFootstepWarningDuration = .1f;
        lateGameFootstepHazardDuration = .1f;
    }
#endif
}
