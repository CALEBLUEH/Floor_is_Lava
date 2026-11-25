using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BossArenaBoundary : MonoBehaviour
{
    [SerializeField] private TileGrid tileGrid;
    [SerializeField, Min(.1f)] private float wallThickness = 1f;
    [SerializeField, Min(1f)] private float wallHeight = 6f;
    [SerializeField] private BoxCollider westWall;
    [SerializeField] private BoxCollider eastWall;
    [SerializeField] private BoxCollider southWall;
    [SerializeField] private BoxCollider northWall;

    public TileGrid Grid => tileGrid;

    private void Awake() => RefreshBoundary();
    private void OnValidate() => RefreshBoundary();

    [ContextMenu("Refresh Boundary From Tile Grid")]
    public void RefreshBoundary()
    {
        if (tileGrid == null || westWall == null || eastWall == null || southWall == null || northWall == null)
            return;

        float cellSize = tileGrid.CellSize;
        float width = tileGrid.Width * cellSize;
        float depth = tileGrid.Height * cellSize;
        Vector3 gridCentre = tileGrid.transform.TransformPoint(new Vector3(
            (tileGrid.Width - 1) * cellSize * .5f,
            0f,
            (tileGrid.Height - 1) * cellSize * .5f));
        transform.position = gridCentre;

        ConfigureWall(westWall, new Vector3(-width * .5f - wallThickness * .5f, wallHeight * .5f, 0f),
            new Vector3(wallThickness, wallHeight, depth + wallThickness * 2f));
        ConfigureWall(eastWall, new Vector3(width * .5f + wallThickness * .5f, wallHeight * .5f, 0f),
            new Vector3(wallThickness, wallHeight, depth + wallThickness * 2f));
        ConfigureWall(southWall, new Vector3(0f, wallHeight * .5f, -depth * .5f - wallThickness * .5f),
            new Vector3(width, wallHeight, wallThickness));
        ConfigureWall(northWall, new Vector3(0f, wallHeight * .5f, depth * .5f + wallThickness * .5f),
            new Vector3(width, wallHeight, wallThickness));
    }

    private static void ConfigureWall(BoxCollider wall, Vector3 localPosition, Vector3 size)
    {
        wall.transform.localPosition = localPosition;
        wall.transform.localRotation = Quaternion.identity;
        wall.size = size;
        wall.center = Vector3.zero;
        wall.isTrigger = false;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(TileGrid grid, BoxCollider west, BoxCollider east, BoxCollider south, BoxCollider north)
    {
        tileGrid = grid;
        westWall = west;
        eastWall = east;
        southWall = south;
        northWall = north;
        RefreshBoundary();
    }
#endif
}
