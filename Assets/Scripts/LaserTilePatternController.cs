using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LaserTilePatternController : MonoBehaviour
{
    private enum LaserPattern
    {
        HorizontalBands,
        VerticalBands,
        Cross,
        Diagonal,
        Checkerboard
    }

    [SerializeField] private TileGrid grid;
    [SerializeField, Min(0.1f)] private float indicatorDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float firingDuration = 0.8f;
    [SerializeField, Min(0f)] private float restDuration = 0.65f;
    [SerializeField, Range(1, 3)] private int bandCount = 2;
    [SerializeField] private bool randomizePatterns = true;
    [SerializeField] private int overlayPriority = 200;

    private readonly List<FloorTile> activeTiles = new List<FloorTile>();
    private Coroutine sequenceRoutine;
    private LaserPattern previousPattern;
    private bool hasPreviousPattern;

    private void Start()
    {
        Play();
    }

    private void OnDisable()
    {
        StopAndClear();
    }

    public void Play()
    {
        StopAndClear();
        if (grid == null)
        {
            Debug.LogWarning("Laser pattern controller needs a TileGrid reference.", this);
            return;
        }

        sequenceRoutine = StartCoroutine(RunSequence());
    }

    public void StopAndClear()
    {
        if (sequenceRoutine != null)
        {
            StopCoroutine(sequenceRoutine);
            sequenceRoutine = null;
        }

        ClearActiveTiles();
    }

    private IEnumerator RunSequence()
    {
        int sequenceIndex = 0;
        while (true)
        {
            LaserPattern pattern = ChoosePattern(sequenceIndex++);
            BuildPattern(pattern, activeTiles);

            // WarningJumpable is the bright yellow tile material. During this
            // phase it is only a visual telegraph and does not damage the player.
            ApplyToActiveTiles(TileType.WarningJumpable);
            yield return new WaitForSeconds(indicatorDuration);

            ApplyToActiveTiles(TileType.HazardJumpable);
            yield return new WaitForSeconds(firingDuration);

            ClearActiveTiles();
            yield return new WaitForSeconds(restDuration);
        }
    }

    public void SetWave(int wave)
    {
        int level = Mathf.Clamp(wave, 1, 3);
        indicatorDuration = level == 1 ? 1.55f : level == 2 ? 1.15f : .8f;
        firingDuration = level == 1 ? .75f : level == 2 ? .95f : 1.15f;
        restDuration = level == 1 ? .75f : level == 2 ? .48f : .28f;
        bandCount = level == 1 ? 1 : level == 2 ? 2 : 3;
        if (Application.isPlaying && isActiveAndEnabled && sequenceRoutine == null)
        {
            Play();
        }
    }

    private LaserPattern ChoosePattern(int sequenceIndex)
    {
        int patternCount = System.Enum.GetValues(typeof(LaserPattern)).Length;
        LaserPattern chosen = randomizePatterns
            ? (LaserPattern)Random.Range(0, patternCount)
            : (LaserPattern)(sequenceIndex % patternCount);

        if (hasPreviousPattern && chosen == previousPattern && patternCount > 1)
        {
            chosen = (LaserPattern)(((int)chosen + Random.Range(1, patternCount)) % patternCount);
        }

        previousPattern = chosen;
        hasPreviousPattern = true;
        return chosen;
    }

    private void BuildPattern(LaserPattern pattern, List<FloorTile> destination)
    {
        destination.Clear();
        HashSet<FloorTile> uniqueTiles = new HashSet<FloorTile>();

        switch (pattern)
        {
            case LaserPattern.HorizontalBands:
                AddRandomRows(uniqueTiles);
                break;
            case LaserPattern.VerticalBands:
                AddRandomColumns(uniqueTiles);
                break;
            case LaserPattern.Cross:
                AddRow(Random.Range(0, grid.Height), uniqueTiles);
                AddColumn(Random.Range(0, grid.Width), uniqueTiles);
                break;
            case LaserPattern.Diagonal:
                AddDiagonal(Random.value >= 0.5f, uniqueTiles);
                break;
            case LaserPattern.Checkerboard:
                AddCheckerboard(Random.Range(0, 2), uniqueTiles);
                break;
        }

        destination.AddRange(uniqueTiles);
    }

    private void AddRandomRows(HashSet<FloorTile> tiles)
    {
        HashSet<int> selected = new HashSet<int>();
        int count = Mathf.Min(bandCount, grid.Height);
        while (selected.Count < count)
        {
            selected.Add(Random.Range(0, grid.Height));
        }

        foreach (int row in selected)
        {
            AddRow(row, tiles);
        }
    }

    private void AddRandomColumns(HashSet<FloorTile> tiles)
    {
        HashSet<int> selected = new HashSet<int>();
        int count = Mathf.Min(bandCount, grid.Width);
        while (selected.Count < count)
        {
            selected.Add(Random.Range(0, grid.Width));
        }

        foreach (int column in selected)
        {
            AddColumn(column, tiles);
        }
    }

    private void AddRow(int row, HashSet<FloorTile> tiles)
    {
        for (int column = 0; column < grid.Width; column++)
        {
            AddTile(grid.GetTile(column, row), tiles);
        }
    }

    private void AddColumn(int column, HashSet<FloorTile> tiles)
    {
        for (int row = 0; row < grid.Height; row++)
        {
            AddTile(grid.GetTile(column, row), tiles);
        }
    }

    private void AddDiagonal(bool reverse, HashSet<FloorTile> tiles)
    {
        int length = Mathf.Min(grid.Width, grid.Height);
        for (int index = 0; index < length; index++)
        {
            int column = reverse ? grid.Width - 1 - index : index;
            AddTile(grid.GetTile(column, index), tiles);

            // A two-tile-wide diagonal is easier to read and dodge.
            int neighbour = column + (reverse ? -1 : 1);
            AddTile(grid.GetTile(neighbour, index), tiles);
        }
    }

    private void AddCheckerboard(int parity, HashSet<FloorTile> tiles)
    {
        for (int row = 0; row < grid.Height; row++)
        {
            for (int column = 0; column < grid.Width; column++)
            {
                if ((row + column) % 2 == parity)
                {
                    AddTile(grid.GetTile(column, row), tiles);
                }
            }
        }
    }

    private static void AddTile(FloorTile tile, HashSet<FloorTile> tiles)
    {
        if (tile != null && tile.StartingType != TileType.Wall)
        {
            tiles.Add(tile);
        }
    }

    private void ApplyToActiveTiles(TileType type)
    {
        foreach (FloorTile tile in activeTiles)
        {
            tile.ApplyTileOverride(this, type, overlayPriority);
        }
    }

    private void ClearActiveTiles()
    {
        foreach (FloorTile tile in activeTiles)
        {
            if (tile != null)
            {
                tile.RemoveTileOverride(this);
            }
        }

        activeTiles.Clear();
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(TileGrid tileGrid)
    {
        grid = tileGrid;
        indicatorDuration = 1.5f;
        firingDuration = 0.8f;
        restDuration = 0.65f;
        bandCount = 2;
        randomizePatterns = true;
        overlayPriority = 200;
    }
#endif
}
