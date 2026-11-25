using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossAttackProjectile : MonoBehaviour
{
    [SerializeField, Min(.1f)] private float defaultSpeed = 16f;
    [SerializeField, Min(.5f)] private float maximumLifetime = 5f;

    private MonoBehaviour targetBehaviour;
    private IBossDamageTarget target;
    private int damage;
    private float speed;
    private float expiresAt;

    public void Initialize(MonoBehaviour boss, int attackDamage, float travelSpeed)
    {
        targetBehaviour = boss;
        target = boss as IBossDamageTarget;
        damage = Mathf.Max(1, attackDamage);
        speed = travelSpeed > 0f ? travelSpeed : defaultSpeed;
        expiresAt = Time.time + maximumLifetime;
    }

    private void Update()
    {
        if (targetBehaviour == null || target == null || target.IsDefeated || Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 destination = target.TargetTransform.position;
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
        if ((transform.position - destination).sqrMagnitude <= .04f)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
