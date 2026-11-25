using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
public class MemoryGridController : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private TopDownPlayer player;
    [SerializeField] private PlayerLife playerLife;
    [SerializeField] private int[] pathLengths = { 10, 15, 20 };
    [SerializeField, Min(1f)] private float previewDuration = 10f;
    [SerializeField, Range(2, 5)] private int guideRepeats = 3;
    private readonly List<FloorTile> path = new();
    private readonly List<Vector2Int> pathCells = new();
    private GameplayUIController ui;
    private FloorTile lastTile;
    private LineRenderer routeLine;
    private MeshRenderer routeArrows;
    private MeshFilter arrowFilter;
    private Mesh arrowMesh;
    private Material arrowMaterial;
    private int wave;
    private int progress;
    private bool acceptingInput;
    private bool previewing;
    private Vector3 resetPosition;

    private void Start()
    {
        if (grid == null) grid = FindAnyObjectByType<TileGrid>();
        if (player == null) player = FindAnyObjectByType<TopDownPlayer>();
        if (playerLife == null && player != null) playerLife = player.GetComponent<PlayerLife>();
        if (player != null) resetPosition = player.transform.position;
        ui = FindAnyObjectByType<GameplayUIController>();
        ui?.SetLevelInstructions($"Watch the connected route light up for {Mathf.CeilToInt(previewDuration)} seconds, then follow it from the purple start tile. A wrong tile restarts the current wave.");
        CreateRouteLine();
        StartCoroutine(BeginWave(1));
    }

    private void Update()
    {
        if (previewing)
        {
            player?.SetControlEnabled(false);
            return;
        }
        if (!acceptingInput || player == null || !player.IsGrounded) return;
        FloorTile tile = TileUnderPlayer();
        if (tile == null || tile == lastTile) return;
        lastTile = tile;
        if (tile != path[progress]) { StartCoroutine(FailWave()); return; }
        tile.ApplyTileOverride(this, TileType.DisplayGreen, 900);
        SceneAudioDirector.PlayObjectiveTrigger();
        progress++;
        ui?.SetObjectiveStatus($"FOLLOW PATH {progress}/{path.Count}");
        if (progress >= path.Count) StartCoroutine(CompleteWave());
    }

    private IEnumerator BeginWave(int nextWave)
    {
        acceptingInput = false; previewing = true; wave = nextWave; progress = 0; lastTile = null;
        player?.SetControlEnabled(false);
        player?.TeleportTo(resetPosition);
        ui?.SetWave(wave, 3); ClearAll();
        int length = Mathf.Min(pathLengths[Mathf.Clamp(wave - 1, 0, pathLengths.Length - 1)], grid.Width * grid.Height);
        BuildConnectedPath(length);
        ShowRouteLine(true);
        float previewEnd = Time.time + previewDuration;
        int repeats = Mathf.Clamp(guideRepeats, 2, 5);
        float chaseBudget = previewDuration * .68f;
        float stepDelay = Mathf.Clamp(chaseBudget / Mathf.Max(1, repeats * path.Count), .045f, .18f);
        for (int repeat = 0; repeat < repeats; repeat++)
        {
            foreach (FloorTile tile in path) tile.RemoveTileOverride(this);
            yield return PreviewDelay(.16f, previewEnd, repeat + 1, repeats);
            for (int i = 0; i < path.Count; i++)
            {
                path[i].ApplyTileOverride(this, i == 0 ? TileType.DisplayStart : TileType.DisplayGreen, 900);
                yield return PreviewDelay(stepDelay, previewEnd, repeat + 1, repeats);
            }
            yield return PreviewDelay(.17f, previewEnd, repeat + 1, repeats);
        }

        foreach (FloorTile tile in path) tile.RemoveTileOverride(this);
        for (int i = 0; i < path.Count; i++)
            path[i].ApplyTileOverride(this, i == 0 ? TileType.DisplayStart : TileType.DisplayGreen, 900);
        while (Time.time < previewEnd)
        {
            UpdatePreviewHud(previewEnd, repeats, repeats);
            yield return null;
        }

        foreach (FloorTile tile in path) tile.RemoveTileOverride(this);
        ShowRouteLine(false);
        path[0].ApplyTileOverride(this, TileType.DisplayStart, 900);
        progress = 1;
        ui?.SetObjectiveStatus($"START HERE - FOLLOW PATH {progress}/{path.Count}");
        lastTile = path[0];
        previewing = false;
        acceptingInput = true;
        ui?.ClearTimerCountdown();
        player?.SetControlEnabled(true);
    }

    private IEnumerator PreviewDelay(float duration, float previewEnd, int pass, int passCount)
    {
        float elapsed = 0f;
        while (elapsed < duration && Time.time < previewEnd)
        {
            elapsed += Time.deltaTime;
            UpdatePreviewHud(previewEnd, pass, passCount);
            yield return null;
        }
    }

    private void UpdatePreviewHud(float previewEnd, int pass, int passCount)
    {
        float remaining = Mathf.Max(0f, previewEnd - Time.time);
        ui?.SetObjectiveStatus($"FOLLOW THE LIGHTS  {pass}/{passCount}");
        ui?.SetTimerCountdown(remaining, "MEMORIZE");
    }

    private void BuildConnectedPath(int targetLength)
    {
        path.Clear(); pathCells.Clear();
        bool[,] used = new bool[grid.Width, grid.Height];
        if (!TryBuildPath(Vector2Int.zero, targetLength, used))
            throw new System.InvalidOperationException("Unable to create a connected Memory Grid route.");
        foreach (Vector2Int cell in pathCells) path.Add(grid.GetTile(cell.x, cell.y));
    }

    private bool TryBuildPath(Vector2Int cell, int targetLength, bool[,] used)
    {
        used[cell.x, cell.y] = true; pathCells.Add(cell);
        if (pathCells.Count >= targetLength) return true;
        List<Vector2Int> neighbours = new()
        {
            cell + Vector2Int.right, cell + Vector2Int.up,
            cell + Vector2Int.left, cell + Vector2Int.down
        };
        for (int i = neighbours.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (neighbours[i], neighbours[swap]) = (neighbours[swap], neighbours[i]);
        }
        foreach (Vector2Int next in neighbours)
        {
            if (next.x < 0 || next.x >= grid.Width || next.y < 0 || next.y >= grid.Height || used[next.x, next.y]) continue;
            if (TryBuildPath(next, targetLength, used)) return true;
        }
        used[cell.x, cell.y] = false; pathCells.RemoveAt(pathCells.Count - 1);
        return false;
    }

    private IEnumerator CompleteWave()
    {
        acceptingInput = false; previewing = true; player?.SetControlEnabled(false); ui?.ClearTimerCountdown(); ui?.SetObjectiveStatus("ROUTE COMPLETE");
        yield return FlashAll(TileType.DisplayGreen, 3, .22f);
        ClearAll();
        yield return new WaitForSeconds(.4f);
        if (wave >= 3) { ui?.ShowLevelComplete(); yield break; }
        yield return BeginWave(wave + 1);
    }

    private IEnumerator FailWave()
    {
        acceptingInput = false; previewing = true; player?.SetControlEnabled(false); ui?.ClearTimerCountdown(); ui?.SetObjectiveStatus("WRONG TILE - RESTARTING ROUTE");
        playerLife?.TryTakeDamage(this);
        yield return FlashAll(TileType.DisplayRed, 3, .24f);
        ClearAll();
        if (playerLife != null && playerLife.IsDefeated) yield break;
        player?.TeleportTo(resetPosition);
        yield return new WaitForSeconds(.4f);
        yield return BeginWave(wave);
    }

    private IEnumerator FlashAll(TileType type, int count, float duration)
    {
        FloorTile[] tiles = grid.GetComponentsInChildren<FloorTile>(true);
        for (int i = 0; i < count; i++)
        {
            foreach (FloorTile tile in tiles) tile.ApplyTileOverride(this, type, 1000);
            yield return new WaitForSeconds(duration);
            foreach (FloorTile tile in tiles) tile.RemoveTileOverride(this);
            yield return new WaitForSeconds(duration);
        }
    }

    private void CreateRouteLine()
    {
        GameObject lineObject = new("Memory Route Guide", typeof(LineRenderer));
        lineObject.transform.SetParent(transform, false);
        routeLine = lineObject.GetComponent<LineRenderer>();
        routeLine.useWorldSpace = true; routeLine.widthMultiplier = .14f;
        routeLine.numCornerVertices = 4; routeLine.numCapVertices = 4;
        routeLine.material = new Material(Shader.Find("Sprites/Default"));
        routeLine.startColor = new Color(.2f, 1f, .45f, .95f);
        routeLine.endColor = new Color(.05f, .75f, 1f, .95f);
        routeLine.enabled = false;
        GameObject arrows = new("Memory Route Arrows", typeof(MeshFilter), typeof(MeshRenderer));
        arrows.transform.SetParent(transform, false);
        arrowFilter = arrows.GetComponent<MeshFilter>();
        routeArrows = arrows.GetComponent<MeshRenderer>();
        arrowMaterial = new Material(Shader.Find("Sprites/Default"));
        arrowMaterial.color = new Color(.65f, 1f, .86f, 1f);
        routeArrows.sharedMaterial = arrowMaterial;
        routeArrows.enabled = false;
    }

    private void ShowRouteLine(bool visible)
    {
        routeLine.enabled = visible;
        if (routeArrows != null) routeArrows.enabled = visible;
        if (!visible) return;
        routeLine.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++) routeLine.SetPosition(i, path[i].transform.position + Vector3.up * 1.08f);
        BuildRouteArrows();
    }

    private void BuildRouteArrows()
    {
        if (arrowMesh != null) Destroy(arrowMesh);
        arrowMesh = new Mesh { name = "Route Direction Arrows" };
        int segments = Mathf.Max(0, path.Count - 1);
        Vector3[] vertices = new Vector3[segments * 3];
        int[] triangles = new int[vertices.Length];
        for (int i = 0; i < segments; i++)
        {
            Vector3 from = routeLine.GetPosition(i);
            Vector3 to = routeLine.GetPosition(i + 1);
            Vector3 direction = (to - from).normalized;
            Vector3 sideways = Vector3.Cross(Vector3.up, direction).normalized;
            Vector3 center = Vector3.Lerp(from, to, .58f) + Vector3.up * .035f;
            vertices[i * 3] = arrowFilter.transform.InverseTransformPoint(center + direction * .62f);
            vertices[i * 3 + 1] = arrowFilter.transform.InverseTransformPoint(center - direction * .38f - sideways * .39f);
            vertices[i * 3 + 2] = arrowFilter.transform.InverseTransformPoint(center - direction * .38f + sideways * .39f);
            triangles[i * 3] = i * 3;
            triangles[i * 3 + 1] = i * 3 + 2;
            triangles[i * 3 + 2] = i * 3 + 1;
        }
        arrowMesh.vertices = vertices;
        arrowMesh.triangles = triangles;
        arrowMesh.RecalculateNormals();
        arrowFilter.sharedMesh = arrowMesh;
    }

    private void ClearAll()
    {
        if (grid != null) foreach (FloorTile tile in grid.GetComponentsInChildren<FloorTile>(true)) tile.RemoveTileOverride(this);
        if (routeLine != null) routeLine.enabled = false;
        if (routeArrows != null) routeArrows.enabled = false;
    }

    private void OnDestroy()
    {
        if (arrowMesh != null) Destroy(arrowMesh);
        if (arrowMaterial != null) Destroy(arrowMaterial);
        if (routeLine != null && routeLine.material != null) Destroy(routeLine.material);
    }

    private FloorTile TileUnderPlayer()
    {
        Vector3 local = grid.transform.InverseTransformPoint(player.transform.position);
        return grid.GetTile(Mathf.RoundToInt(local.x / grid.CellSize), Mathf.RoundToInt(local.z / grid.CellSize));
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(TileGrid targetGrid, TopDownPlayer targetPlayer) { grid = targetGrid; player = targetPlayer; }
#endif
}
