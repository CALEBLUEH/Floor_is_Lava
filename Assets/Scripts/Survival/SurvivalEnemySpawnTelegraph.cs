using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SurvivalEnemySpawnTelegraph : MonoBehaviour
{
    private FloorTile tile;

    public void Initialize(FloorTile spawnTile, SurvivalEnemy enemyPrefab, Transform enemyRoot,
        Transform effectRoot, TileGrid grid, TopDownPlayer target, int health, float speed,
        float warningDuration, int overlayPriority)
    {
        tile = spawnTile;
        tile?.ApplyTileOverride(this, TileType.WarningJumpable, overlayPriority);
        StartCoroutine(Spawn(enemyPrefab, enemyRoot, effectRoot, grid, target, health, speed,
            Mathf.Max(.05f, warningDuration)));
    }

    private IEnumerator Spawn(SurvivalEnemy prefab, Transform enemyRoot, Transform effectRoot,
        TileGrid grid, TopDownPlayer target, int health, float speed, float warningDuration)
    {
        yield return new WaitForSeconds(warningDuration);
        tile?.RemoveTileOverride(this);
        if (prefab != null && tile != null && target != null)
        {
            SurvivalEnemy enemy = Instantiate(prefab, tile.transform.position + Vector3.up * 1.8f,
                Quaternion.identity, enemyRoot);
            enemy.Initialize(target, health, speed, grid, enemyRoot, effectRoot);
        }
        Destroy(gameObject);
    }

    private void OnDisable() => tile?.RemoveTileOverride(this);
}
