using UnityEngine;

[DisallowMultipleComponent]
public class PongBallHazard : MonoBehaviour
{
    [SerializeField] private bool jumpable;
    [SerializeField, Min(0f)] private float hitCooldown = 0.4f;
    [SerializeField, Min(0f)] private float jumpClearance = 0.08f;

    private float nextHitTime;

    private void OnTriggerEnter(Collider other)
    {
        TryHitPlayer(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryHitPlayer(other);
    }

    private void TryHitPlayer(Collider other)
    {
        if (Time.time < nextHitTime)
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


        if (jumpable)
        {
            Collider ballCollider = GetComponent<Collider>();
            if (ballCollider != null && other.bounds.min.y > ballCollider.bounds.max.y - jumpClearance)
            {
                return;
            }
        }

        nextHitTime = Time.time + hitCooldown;
        player.ReportHazardHit(this, jumpable);
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(bool canJumpOver)
    {
        jumpable = canJumpOver;
    }
#endif
}
