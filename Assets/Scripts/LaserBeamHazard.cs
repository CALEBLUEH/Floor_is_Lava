using UnityEngine;

[DisallowMultipleComponent]
public class LaserBeamHazard : MonoBehaviour
{
    [SerializeField] private Collider beamCollider;
    [SerializeField, Min(0f)] private float jumpClearance = .08f;
    [SerializeField, Min(0f)] private float hitCooldown = .45f;
    private float nextHitTime;

    private void Awake()
    {
        if (beamCollider == null) beamCollider = GetComponent<Collider>();
    }

    private void OnTriggerEnter(Collider other) => TryHit(other);
    private void OnTriggerStay(Collider other) => TryHit(other);

    private void TryHit(Collider other)
    {
        if (Time.time < nextHitTime || beamCollider == null) return;
        TopDownPlayer player = other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<TopDownPlayer>()
            : other.GetComponentInParent<TopDownPlayer>();
        if (player == null || other.bounds.min.y > beamCollider.bounds.max.y - jumpClearance) return;
        nextHitTime = Time.time + hitCooldown;
        player.ReportHazardHit(this, true);
    }
}
