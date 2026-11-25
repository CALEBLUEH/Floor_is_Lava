using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SurvivalFloorHazardPulse : MonoBehaviour
{
    private FloorTile[] tiles;
    private int priority;

    public void Initialize(FloorTile[] targets, float warningDuration, float hazardDuration, int overlayPriority)
    {
        tiles = targets;
        priority = overlayPriority;
        StartCoroutine(Run(Mathf.Max(.05f, warningDuration), Mathf.Max(.05f, hazardDuration)));
    }

    private IEnumerator Run(float warningDuration, float hazardDuration)
    {
        Apply(TileType.WarningJumpable);
        yield return new WaitForSeconds(warningDuration);
        Apply(TileType.HazardJumpable);
        yield return new WaitForSeconds(hazardDuration);
        Clear();
        Destroy(gameObject);
    }

    private void Apply(TileType type)
    {
        if (tiles == null) return;
        foreach (FloorTile tile in tiles) tile?.ApplyTileOverride(this, type, priority);
    }

    private void Clear()
    {
        if (tiles == null) return;
        foreach (FloorTile tile in tiles) tile?.RemoveTileOverride(this);
    }

    private void OnDisable() => Clear();
}
