using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossTileProjectile : MonoBehaviour
{
    private TileGrid grid;
    private Vector2Int cell;
    private Vector2Int direction;
    private int bouncesRemaining;
    private int stepsRemaining;
    private float secondsPerCell;
    private float activeTime;
    private int overlayPriority;
    private FloorTile activeTile;

    public int InitialBounceCount { get; private set; }

    public void Initialize(TileGrid targetGrid, Vector2Int startCell, Vector2Int travelDirection,
        int bounceCount, int maximumSteps, float travelSecondsPerCell, float tileActiveTime, int priority)
    {
        grid = targetGrid;
        cell = startCell;
        direction = travelDirection;
        bouncesRemaining = Mathf.Max(0, bounceCount);
        InitialBounceCount = bouncesRemaining;
        stepsRemaining = Mathf.Max(1, maximumSteps);
        secondsPerCell = Mathf.Max(.03f, travelSecondsPerCell);
        activeTime = Mathf.Clamp(tileActiveTime, .02f, secondsPerCell);
        overlayPriority = priority;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        while (grid != null && stepsRemaining-- > 0)
        {
            Vector2Int next = cell + direction;
            bool hitX = next.x < 0 || next.x >= grid.Width;
            bool hitY = next.y < 0 || next.y >= grid.Height;
            if (hitX || hitY)
            {
                if (bouncesRemaining <= 0) break;
                if (hitX) direction.x *= -1;
                if (hitY) direction.y *= -1;
                bouncesRemaining--;
                next = cell + direction;
            }

            cell = next;
            activeTile = grid.GetTile(cell.x, cell.y);
            activeTile?.ApplyTileOverride(this, TileType.HazardJumpable, overlayPriority);
            yield return new WaitForSeconds(activeTime);
            ClearTile();
            float remainder = secondsPerCell - activeTime;
            if (remainder > 0f) yield return new WaitForSeconds(remainder);
        }
        Destroy(gameObject);
    }

    private void ClearTile()
    {
        if (activeTile != null) activeTile.RemoveTileOverride(this);
        activeTile = null;
    }

    private void OnDisable() => ClearTile();
}
