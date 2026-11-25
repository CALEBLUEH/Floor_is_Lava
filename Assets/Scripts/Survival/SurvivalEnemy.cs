using UnityEngine;

public enum SurvivalEnemyMovement
{
    SeekPlayer,
    Bounce,
    Stationary
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider), typeof(Rigidbody))]
public sealed class SurvivalEnemy : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 2;
    [SerializeField, Min(.1f)] private float moveSpeed = 3.5f;
    [SerializeField, Min(0f)] private float stoppingDistance = .15f;
    [SerializeField] private SurvivalEnemyMovement movementMode = SurvivalEnemyMovement.SeekPlayer;
    [SerializeField, Min(0f)] private float combatPlaneHeight = 1.8f;

    private TopDownPlayer target;
    private TileGrid grid;
    private Transform enemyRoot;
    private Transform effectRoot;
    private int currentHealth;
    private float lockedHeight;
    private Vector2 bounceDirection;
    private SurvivalEnemyHealthBar healthBar;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public float MoveSpeed => moveSpeed;
    public SurvivalEnemyMovement MovementMode => movementMode;
    public TopDownPlayer Target => target;
    public TileGrid Grid => grid;
    public Transform EnemyRoot => enemyRoot;
    public Transform EffectRoot => effectRoot;
    public float CombatPlaneWorldHeight => (grid != null ? grid.transform.position.y : 0f) + combatPlaneHeight;

    private void Awake()
    {
        currentHealth = maxHealth;
        lockedHeight = transform.position.y;
        healthBar = GetComponent<SurvivalEnemyHealthBar>();
    }

    public void Initialize(TopDownPlayer player, int health, float speed)
    {
        Initialize(player, health, speed, null, transform.parent, transform.parent);
    }

    public void Initialize(TopDownPlayer player, int health, float speed, TileGrid tileGrid,
        Transform spawnedEnemyRoot, Transform spawnedEffectRoot)
    {
        target = player;
        grid = tileGrid;
        enemyRoot = spawnedEnemyRoot;
        effectRoot = spawnedEffectRoot;
        maxHealth = Mathf.Max(1, health);
        currentHealth = maxHealth;
        moveSpeed = Mathf.Max(.1f, speed);
        lockedHeight = grid != null ? CombatPlaneWorldHeight : transform.position.y;
        transform.position = new Vector3(transform.position.x, lockedHeight, transform.position.z);
        bounceDirection = Random.insideUnitCircle.normalized;
        if (bounceDirection.sqrMagnitude < .01f) bounceDirection = Vector2.right;
        GetComponent<SurvivalEnemyGun>()?.Initialize(effectRoot);
        GetComponent<SurvivalEnemySummoner>()?.Initialize();
        healthBar?.Refresh();
    }

    private void Update()
    {
        if (target == null || currentHealth <= 0) return;

        switch (movementMode)
        {
            case SurvivalEnemyMovement.SeekPlayer:
                SeekPlayer();
                break;
            case SurvivalEnemyMovement.Bounce:
                BounceInsideArena();
                break;
        }
    }

    private void SeekPlayer()
    {
        Vector3 offset = target.transform.position - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= stoppingDistance * stoppingDistance) return;
        MoveAndTurn(offset.normalized);
    }

    private void BounceInsideArena()
    {
        if (grid == null) return;
        Vector3 local = grid.transform.InverseTransformPoint(transform.position);
        Vector3 localStep = new(bounceDirection.x, 0f, bounceDirection.y);
        Vector3 next = local + localStep * (moveSpeed * Time.deltaTime);
        float maximumX = (grid.Width - 1) * grid.CellSize;
        float maximumZ = (grid.Height - 1) * grid.CellSize;
        bool bounced = false;
        if (next.x < 0f || next.x > maximumX)
        {
            bounceDirection.x *= -1f;
            bounced = true;
        }
        if (next.z < 0f || next.z > maximumZ)
        {
            bounceDirection.y *= -1f;
            bounced = true;
        }
        if (bounced) localStep = new Vector3(bounceDirection.x, 0f, bounceDirection.y);
        next = local + localStep * (moveSpeed * Time.deltaTime);
        next.x = Mathf.Clamp(next.x, 0f, maximumX);
        next.z = Mathf.Clamp(next.z, 0f, maximumZ);
        Vector3 world = grid.transform.TransformPoint(next);
        world.y = lockedHeight;
        transform.position = world;
        TurnToward(grid.transform.TransformDirection(localStep).normalized);
    }

    private void MoveAndTurn(Vector3 direction)
    {
        Vector3 next = transform.position + direction * (moveSpeed * Time.deltaTime);
        next.y = lockedHeight;
        transform.position = grid != null ? ClampToArena(next) : next;
        TurnToward(direction);
    }

    private void TurnToward(Vector3 direction)
    {
        if (direction.sqrMagnitude < .001f) return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction, Vector3.up),
            12f * Time.deltaTime);
    }

    public Vector3 ClampToArena(Vector3 worldPosition)
    {
        if (grid == null) return worldPosition;
        Vector3 local = grid.transform.InverseTransformPoint(worldPosition);
        local.x = Mathf.Clamp(local.x, 0f, (grid.Width - 1) * grid.CellSize);
        local.z = Mathf.Clamp(local.z, 0f, (grid.Height - 1) * grid.CellSize);
        Vector3 world = grid.transform.TransformPoint(local);
        world.y = lockedHeight;
        return world;
    }

    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0 || amount <= 0) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        healthBar?.Refresh();
        if (currentHealth > 0) return;
        Debug.Log($"Survival enemy defeated: {name}", this);
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other) => DamagePlayer(other);
    private void OnTriggerStay(Collider other) => DamagePlayer(other);

    private void DamagePlayer(Collider other)
    {
        TopDownPlayer player = other != null && other.attachedRigidbody != null
            ? other.attachedRigidbody.GetComponent<TopDownPlayer>()
            : other != null ? other.GetComponentInParent<TopDownPlayer>() : null;
        player?.ReportHazardHit(this, false);
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(int health, float speed,
        SurvivalEnemyMovement movement = SurvivalEnemyMovement.SeekPlayer)
    {
        maxHealth = Mathf.Max(1, health);
        moveSpeed = Mathf.Max(.1f, speed);
        movementMode = movement;
    }
#endif
}
