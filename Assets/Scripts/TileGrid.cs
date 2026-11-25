using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TileGrid : MonoBehaviour
{
    [SerializeField, Min(1)] private int width = 10;
    [SerializeField, Min(1)] private int height = 10;
    [SerializeField, Min(0.01f)] private float cellSize = 2f;

    private readonly Dictionary<Vector2Int, FloorTile> tiles = new Dictionary<Vector2Int, FloorTile>();

    public int Width => width;
    public int Height => height;
    public float CellSize => cellSize;

    private void Awake()
    {
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        tiles.Clear();
        foreach (FloorTile tile in GetComponentsInChildren<FloorTile>(true))
        {
            Vector3 local = transform.InverseTransformPoint(tile.transform.position);
            Vector2Int coordinate = new Vector2Int(
                Mathf.RoundToInt(local.x / cellSize),
                Mathf.RoundToInt(local.z / cellSize));

            if (coordinate.x < 0 || coordinate.x >= width ||
                coordinate.y < 0 || coordinate.y >= height)
            {
                Debug.LogWarning($"Tile '{tile.name}' is outside the declared grid at {coordinate}.", tile);
                continue;
            }

            if (!tiles.TryAdd(coordinate, tile))
            {
                Debug.LogWarning($"More than one tile occupies grid coordinate {coordinate}.", tile);
            }
        }
    }

    public FloorTile GetTile(int x, int y)
    {
        if (tiles.Count == 0)
        {
            RebuildLookup();
        }

        tiles.TryGetValue(new Vector2Int(x, y), out FloorTile tile);
        return tile;
    }

    public void ConfigureRuntimeDimensions(int gridWidth, int gridHeight)
    {
        width = Mathf.Max(1, gridWidth);
        height = Mathf.Max(1, gridHeight);
        RebuildLookup();
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(int gridWidth, int gridHeight, float gridCellSize)
    {
        width = Mathf.Max(1, gridWidth);
        height = Mathf.Max(1, gridHeight);
        cellSize = Mathf.Max(0.01f, gridCellSize);
    }
#endif
}
