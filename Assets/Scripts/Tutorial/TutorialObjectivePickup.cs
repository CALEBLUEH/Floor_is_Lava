using UnityEngine;

[DisallowMultipleComponent]
public sealed class TutorialObjectivePickup : MonoBehaviour
{
    private FloorTile tile;
    private TutorialTvDialogueController owner;
    private bool activeObjective;

    public bool IsActiveObjective => activeObjective;

    public void Activate(FloorTile targetTile, TutorialTvDialogueController objectiveOwner)
    {
        tile = targetTile;
        owner = objectiveOwner;
        activeObjective = tile != null && owner != null;
        if (activeObjective)
        {
            tile.ApplyTileOverride(this, TileType.DisplayGreen, 500);
        }
    }

    public void Deactivate()
    {
        if (tile != null)
        {
            tile.RemoveTileOverride(this);
        }

        activeObjective = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryCollect(other);
    }

    private void TryCollect(Collider other)
    {
        if (!activeObjective || other == null)
        {
            return;
        }

        TopDownPlayer player = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<TopDownPlayer>()
            : other.GetComponentInParent<TopDownPlayer>();
        if (player == null)
        {
            return;
        }

        activeObjective = false;
        tile.RemoveTileOverride(this);
        SceneAudioDirector.PlayObjectiveTrigger();
        owner.NotifyObjectiveCollected(this);
    }

    private void OnDestroy()
    {
        if (tile != null)
        {
            tile.RemoveTileOverride(this);
        }
    }
}
