using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SurvivalEnemy))]
public sealed class SurvivalEnemyGun : MonoBehaviour
{
    [SerializeField] private SurvivalEnemyProjectile projectilePrefab;
    [SerializeField, Min(.1f)] private float fireInterval = 1.5f;
    [SerializeField, Min(0f)] private float initialDelay = 1f;
    [SerializeField, Min(.1f)] private float projectileSpeed = 10f;
    [SerializeField, Min(.1f)] private float projectileLifetime = 6f;

    private SurvivalEnemy owner;
    private Transform projectileRoot;
    private float nextShotTime;

    private void Awake() => owner = GetComponent<SurvivalEnemy>();

    public void Initialize(Transform spawnedProjectileRoot)
    {
        projectileRoot = spawnedProjectileRoot;
        nextShotTime = Time.time + initialDelay;
    }

    private void Update()
    {
        if (owner == null || owner.Target == null || projectilePrefab == null ||
            owner.CurrentHealth <= 0 || Time.time < nextShotTime) return;

        Vector3 origin = transform.position;
        Vector3 target = owner.Target.transform.position;
        origin.y = target.y = owner.CombatPlaneWorldHeight;
        Vector3 direction = Vector3.ProjectOnPlane(target - origin, Vector3.up).normalized;
        if (direction.sqrMagnitude > .001f)
        {
            SurvivalEnemyProjectile projectile = Instantiate(
                projectilePrefab, origin, Quaternion.LookRotation(direction, Vector3.up), projectileRoot);
            projectile.Initialize(direction, projectileSpeed, projectileLifetime);
        }
        nextShotTime = Time.time + fireInterval;
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(SurvivalEnemyProjectile prefab, float interval, float delay, float speed)
    {
        projectilePrefab = prefab;
        fireInterval = Mathf.Max(.1f, interval);
        initialDelay = Mathf.Max(0f, delay);
        projectileSpeed = Mathf.Max(.1f, speed);
    }
#endif
}
