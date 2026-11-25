using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ScrollingColumnHazardPattern : MonoBehaviour
{
    [SerializeField] private TileGrid grid;
    [SerializeField] private bool playOnStart = true;
    [SerializeField, Min(0)] private int startColumn;
    [SerializeField, Min(0f)] private float initialTelegraphDuration = 1.25f;
    [SerializeField, Min(0.05f)] private float columnStepDuration = 0.65f;
    [SerializeField] private int overlayPriority = 100;

    private Coroutine routine;
    private int hazardColumn = -1;
    private int warningColumn = -1;

    private void Start()
    {
        if (playOnStart)
        {
            Play();
        }
    }

    private void OnDisable()
    {
        StopAndClear();
    }

    public void Play()
    {
        StopAndClear();
        if (grid == null || grid.Width <= 0)
        {
            Debug.LogWarning("Column hazard pattern needs a TileGrid reference.", this);
            return;
        }

        routine = StartCoroutine(RunPattern());
    }

    public void StopAndClear()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        ClearColumn(hazardColumn);
        ClearColumn(warningColumn);
        hazardColumn = -1;
        warningColumn = -1;
    }

    private IEnumerator RunPattern()
    {
        warningColumn = Mathf.Clamp(startColumn, 0, grid.Width - 1);
        ApplyColumn(warningColumn, TileType.WarningUnjumpable);
        yield return new WaitForSeconds(initialTelegraphDuration);

        while (true)
        {
            ClearColumn(hazardColumn);

            hazardColumn = warningColumn;
            ApplyColumn(hazardColumn, TileType.HazardUnjumpable);

            warningColumn = (hazardColumn + 1) % grid.Width;
            ApplyColumn(warningColumn, TileType.WarningUnjumpable);

            yield return new WaitForSeconds(columnStepDuration);
        }
    }

    private void ApplyColumn(int column, TileType type)
    {
        if (column < 0)
        {
            return;
        }

        for (int row = 0; row < grid.Height; row++)
        {
            grid.GetTile(column, row)?.ApplyTileOverride(this, type, overlayPriority);
        }
    }

    private void ClearColumn(int column)
    {
        if (grid == null || column < 0)
        {
            return;
        }

        for (int row = 0; row < grid.Height; row++)
        {
            grid.GetTile(column, row)?.RemoveTileOverride(this);
        }
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(TileGrid tileGrid)
    {
        grid = tileGrid;
        playOnStart = true;
        startColumn = 0;
        initialTelegraphDuration = 1.25f;
        columnStepDuration = 0.65f;
        overlayPriority = 100;
    }
#endif
}
