using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public sealed class SurvivalPlayerProjectile : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float defaultSpeed = 18f;
    [SerializeField, Min(.1f)] private float defaultLifetime = 4f;
    [SerializeField, Min(1)] private int damage = 1;

    private Rigidbody body;
    private Vector3 direction;
    private float speed;
    private float expiresAt;
    private float lockedHeight;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    public void Initialize(Vector3 flatDirection, float travelSpeed, float lifetime, int attackDamage)
    {
        direction = Vector3.ProjectOnPlane(flatDirection, Vector3.up).normalized;
        speed = travelSpeed > 0f ? travelSpeed : defaultSpeed;
        damage = Mathf.Max(1, attackDamage);
        expiresAt = Time.time + (lifetime > 0f ? lifetime : defaultLifetime);
        lockedHeight = transform.position.y;
        if (direction.sqrMagnitude > .001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void FixedUpdate()
    {
        if (Time.time >= expiresAt || direction.sqrMagnitude < .001f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 next = body.position + direction * (speed * Time.fixedDeltaTime);
        next.y = lockedHeight;
        body.MovePosition(next);
    }

    private void OnTriggerEnter(Collider other)
    {
        SurvivalEnemy enemy = other != null ? other.GetComponentInParent<SurvivalEnemy>() : null;
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        if (other != null && !other.isTrigger && other.GetComponentInParent<TopDownPlayer>() == null)
            Destroy(gameObject);
    }
}
