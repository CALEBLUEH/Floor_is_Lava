using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class MovingTile : MonoBehaviour
{
    private enum MovementPhase
    {
        Warning,
        Moving,
        Paused
    }

    [SerializeField] private Vector3 localOffset = new Vector3(4f, 0f, 0f);
    [SerializeField, Min(0.05f)] private float moveDuration = 2f;
    [SerializeField, Min(0f)] private float movementWarningDuration = 0.75f;
    [SerializeField, Min(0f)] private float pauseAtEndpoint = 0.5f;
    [SerializeField] private bool playOnStart = true;

    private Rigidbody rb;
    private FloorTile floorTile;
    private Vector3 startPosition;
    private Vector3 endPosition;
    private float phaseTime;
    private bool movingTowardEnd = true;
    private MovementPhase phase;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        floorTile = GetComponent<FloorTile>();
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        startPosition = rb.position;
        Vector3 worldOffset = transform.parent != null
            ? transform.parent.TransformVector(localOffset)
            : localOffset;
        endPosition = startPosition + worldOffset;

        SetPhase(playOnStart ? MovementPhase.Warning : MovementPhase.Paused);
    }

    private void FixedUpdate()
    {
        if (!playOnStart)
        {
            return;
        }

        phaseTime += Time.fixedDeltaTime;

        switch (phase)
        {
            case MovementPhase.Warning:
                if (phaseTime >= movementWarningDuration)
                {
                    SetPhase(MovementPhase.Moving);
                }
                break;

            case MovementPhase.Moving:
                float t = Mathf.Clamp01(phaseTime / moveDuration);
                float easedT = t * t * (3f - 2f * t);
                Vector3 from = movingTowardEnd ? startPosition : endPosition;
                Vector3 to = movingTowardEnd ? endPosition : startPosition;
                rb.MovePosition(Vector3.Lerp(from, to, easedT));

                if (t >= 1f)
                {
                    movingTowardEnd = !movingTowardEnd;
                    SetPhase(MovementPhase.Paused);
                }
                break;

            case MovementPhase.Paused:
                if (phaseTime >= pauseAtEndpoint)
                {
                    SetPhase(MovementPhase.Warning);
                }
                break;
        }
    }

    private void OnDisable()
    {
        floorTile?.SetMovementIndicator(false);
    }

    private void SetPhase(MovementPhase newPhase)
    {
        phase = newPhase;
        phaseTime = 0f;
        floorTile?.SetMovementIndicator(newPhase != MovementPhase.Paused);
    }
}
