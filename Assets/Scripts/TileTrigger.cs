using UnityEngine;

[DisallowMultipleComponent]
public class TileTrigger : MonoBehaviour
{
    [SerializeField] private FloorTile floorTile;

    private void Awake()
    {
        if (floorTile == null)
        {
            floorTile = GetComponentInParent<FloorTile>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        floorTile?.TryInteract(other, true);
    }

    private void OnTriggerStay(Collider other)
    {
        floorTile?.TryInteract(other, false);
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(FloorTile tile)
    {
        floorTile = tile;
    }
#endif
}
