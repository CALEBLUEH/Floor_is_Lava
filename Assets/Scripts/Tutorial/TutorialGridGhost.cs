using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public sealed class TutorialGridGhost : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float moveSpeed = 3.5f;

    private TileGrid grid;
    private TopDownPlayer target;
    private Vector2Int coordinate;
    private Vector3 destination;
    private bool chooseHorizontalOnTie;

    public void Initialize(TileGrid tileGrid, TopDownPlayer player, Vector2Int startCoordinate, float speed)
    {
        grid = tileGrid;
        target = player;
        coordinate = ClampCoordinate(startCoordinate);
        moveSpeed = Mathf.Max(0.1f, speed);
        transform.position = CoordinateToWorld(coordinate);
        destination = transform.position;
    }

    private void Update()
    {
        if (grid == null || target == null)
        {
            return;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            destination,
            moveSpeed * Time.deltaTime);

        if ((transform.position - destination).sqrMagnitude <= 0.0025f)
        {
            coordinate = WorldToCoordinate(transform.position);
            ChooseNextStep();
        }
    }

    private void ChooseNextStep()
    {
        Vector2Int targetCoordinate = WorldToCoordinate(target.transform.position);
        int xDifference = targetCoordinate.x - coordinate.x;
        int yDifference = targetCoordinate.y - coordinate.y;
        Vector2Int step;

        if (Mathf.Abs(xDifference) > Mathf.Abs(yDifference) ||
            Mathf.Abs(xDifference) == Mathf.Abs(yDifference) && chooseHorizontalOnTie)
        {
            step = new Vector2Int(System.Math.Sign(xDifference), 0);
        }
        else
        {
            step = new Vector2Int(0, System.Math.Sign(yDifference));
        }

        chooseHorizontalOnTie = !chooseHorizontalOnTie;
        if (step == Vector2Int.zero)
        {
            return;
        }

        coordinate = ClampCoordinate(coordinate + step);
        destination = CoordinateToWorld(coordinate);
    }

    private Vector2Int WorldToCoordinate(Vector3 worldPosition)
    {
        Vector3 local = grid.transform.InverseTransformPoint(worldPosition);
        return ClampCoordinate(new Vector2Int(
            Mathf.RoundToInt(local.x / grid.CellSize),
            Mathf.RoundToInt(local.z / grid.CellSize)));
    }

    private Vector2Int ClampCoordinate(Vector2Int value)
    {
        if (grid == null)
        {
            return value;
        }

        return new Vector2Int(
            Mathf.Clamp(value.x, 0, grid.Width - 1),
            Mathf.Clamp(value.y, 0, grid.Height - 1));
    }

    private Vector3 CoordinateToWorld(Vector2Int value)
    {
        FloorTile tile = grid.GetTile(value.x, value.y);
        return tile != null
            ? tile.transform.position + Vector3.up * 1.8f
            : grid.transform.TransformPoint(new Vector3(
                value.x * grid.CellSize,
                1.8f,
                value.y * grid.CellSize));
    }

    private void OnTriggerEnter(Collider other)
    {
        ReportContact(other);
    }

    private void OnTriggerStay(Collider other)
    {
        ReportContact(other);
    }

    private void ReportContact(Collider other)
    {
        TopDownPlayer player = other != null && other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<TopDownPlayer>()
            : other != null ? other.GetComponentInParent<TopDownPlayer>() : null;
        player?.ReportHazardHit(this, false);
    }
}
