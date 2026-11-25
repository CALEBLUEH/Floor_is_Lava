using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossDamageObjective : MonoBehaviour
{
    private FloorTile tile;
    private MonoBehaviour bossBehaviour;
    private IBossDamageTarget boss;
    private TopDownPlayer player;
    private Collider playerCollider;
    private bool active;

    public FloorTile Tile => tile;
    public bool IsActive => active;

    public void Activate(FloorTile targetTile, MonoBehaviour targetBoss, TopDownPlayer targetPlayer)
    {
        Deactivate();
        tile = targetTile;
        bossBehaviour = targetBoss;
        boss = targetBoss as IBossDamageTarget;
        player = targetPlayer;
        playerCollider = player != null
            ? player.GetComponentsInChildren<Collider>(true).FirstOrDefault(candidate => !candidate.isTrigger)
            : null;
        active = tile != null && bossBehaviour != null && boss != null && tile.ApplyTileOverride(this, TileType.DisplayGreen, 650);
        enabled = active;
    }

    public void Deactivate()
    {
        if (tile != null) tile.RemoveTileOverride(this);
        tile = null;
        boss = null;
        bossBehaviour = null;
        player = null;
        playerCollider = null;
        active = false;
        enabled = false;
    }

    private void Update()
    {
        if (active && playerCollider != null) TryCollect(playerCollider);
    }

    private void OnTriggerEnter(Collider other) => TryCollect(other);
    private void OnTriggerStay(Collider other) => TryCollect(other);

    private void TryCollect(Collider other)
    {
        if (!active || boss == null) return;
        TopDownPlayer touchingPlayer = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<TopDownPlayer>()
            : other.GetComponentInParent<TopDownPlayer>();
        if (touchingPlayer == null || touchingPlayer != player || tile == null || !IsOverTile(other)) return;

        Vector3 launchPosition = tile != null ? tile.transform.position + Vector3.up * 1.1f : transform.position;
        IBossDamageTarget receiver = boss;
        Deactivate();
        SceneAudioDirector.PlayPlayerShot();
        receiver.LaunchPlayerAttack(launchPosition);
    }

    private bool IsOverTile(Collider candidate)
    {
        Collider solid = tile.GetComponents<Collider>().FirstOrDefault(tileCollider => !tileCollider.isTrigger);
        float surface = solid != null ? solid.bounds.max.y : tile.transform.position.y;
        Bounds tileBounds = solid != null ? solid.bounds : new Bounds(tile.transform.position, Vector3.one);
        Vector3 position = candidate.bounds.center;
        return position.x >= tileBounds.min.x && position.x <= tileBounds.max.x &&
               position.z >= tileBounds.min.z && position.z <= tileBounds.max.z &&
               candidate.bounds.min.y <= surface + .2f;
    }

    private void OnDisable()
    {
        if (active && tile != null) tile.RemoveTileOverride(this);
    }
}
