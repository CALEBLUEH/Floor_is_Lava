using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TopDownPlayer : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [SerializeField, Min(1f)] private float sprintMultiplier = 1.55f;
    [SerializeField, Min(0f)] private float turnSpeed = 14f;
    [SerializeField] private Transform visualRoot;

    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpHeight = 1.4f;
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.12f;
    [SerializeField] private LayerMask groundLayers = ~0;

    [Header("Gravity")]
    [SerializeField, Min(0.01f)] private float gravityMultiplier = 1f;
    [SerializeField, Min(0.1f)] private float maximumFallSpeed = 30f;

    [Header("Optional animation")]
    [SerializeField] private Animator animator;

    [Header("Prototype hazard feedback")]
    [SerializeField, Min(0f)] private float hazardLogCooldown = 0.5f;

    private static readonly int SpeedParameter = Animator.StringToHash("Speed");
    private static readonly int GroundedParameter = Animator.StringToHash("Grounded");
    private static readonly int JumpParameter = Animator.StringToHash("Jump");

    private readonly RaycastHit[] groundHits = new RaycastHit[8];
    private Rigidbody rb;
    private Collider playerCollider;
    private Vector3 movement;
    private bool jumpQueued;
    private Rigidbody groundBody;
    private float nextHazardLogTime;
    private bool hasSpeedParameter;
    private bool hasGroundedParameter;
    private bool hasJumpParameter;
    private bool controlsEnabled = true;

    public bool IsGrounded { get; private set; }
    public bool IsMoving => movement.sqrMagnitude > 0.001f;
    public bool IsSprinting { get; private set; }
    public bool ControlsEnabled => controlsEnabled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        foreach (Collider candidate in GetComponentsInChildren<Collider>())
        {
            if (!candidate.isTrigger)
            {
                playerCollider = candidate;
                break;
            }
        }
        rb.freezeRotation = true;
        // Gravity is applied below so every player instance can tune it without
        // changing the project-wide Physics gravity setting.
        rb.useGravity = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        CacheAnimatorParameters();
    }

    private void Update()
    {
        if (!controlsEnabled)
        {
            movement = Vector3.zero;
            jumpQueued = false;
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        movement = new Vector3(horizontal, 0f, vertical).normalized;
        IsSprinting = movement.sqrMagnitude > 0.001f &&
                      (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        if (Input.GetButtonDown("Jump"))
        {
            jumpQueued = true;
        }
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();

        Vector3 platformVelocity = groundBody != null
            ? groundBody.GetPointVelocity(rb.position)
            : Vector3.zero;

        Vector3 velocity = rb.linearVelocity;
        float gravityAcceleration = Physics.gravity.y * gravityMultiplier;
        velocity.y += gravityAcceleration * Time.fixedDeltaTime;
        velocity.y = Mathf.Max(velocity.y, -maximumFallSpeed);

        float currentSpeed = moveSpeed * (IsSprinting ? sprintMultiplier : 1f);
        Vector3 horizontalVelocity = movement * currentSpeed +
                                     new Vector3(platformVelocity.x, 0f, platformVelocity.z);
        velocity.x = horizontalVelocity.x;
        velocity.z = horizontalVelocity.z;

        if (IsGrounded && velocity.y < 0f)
        {
            velocity.y = -2f;
        }

        bool jumpedThisFrame = false;
        if (jumpQueued && IsGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravityAcceleration);
            IsGrounded = false;
            jumpedThisFrame = true;
        }

        jumpQueued = false;
        rb.linearVelocity = velocity;

        RotateVisual();
        UpdateAnimator(jumpedThisFrame);
    }

    private void UpdateGroundedState()
    {
        groundBody = null;

        if (playerCollider == null)
        {
            IsGrounded = false;
            return;
        }

        Bounds bounds = playerCollider.bounds;
        float radius = Mathf.Max(0.05f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.8f);
        float distance = bounds.extents.y + groundCheckDistance;
        int hitCount = Physics.SphereCastNonAlloc(
            bounds.center,
            radius,
            Vector3.down,
            groundHits,
            distance,
            groundLayers,
            QueryTriggerInteraction.Ignore);

        IsGrounded = false;
        for (int i = 0; i < hitCount; i++)
        {
            Rigidbody hitBody = groundHits[i].rigidbody;
            if (hitBody == rb)
            {
                continue;
            }

            IsGrounded = true;
            groundBody = hitBody;
            break;
        }
    }

    private void RotateVisual()
    {
        if (movement.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
        if (visualRoot != null && visualRoot != transform)
        {
            visualRoot.rotation = Quaternion.Slerp(
                visualRoot.rotation,
                targetRotation,
                turnSpeed * Time.fixedDeltaTime);
        }
        else
        {
            rb.MoveRotation(Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                turnSpeed * Time.fixedDeltaTime));
        }
    }

    public void ReportHazardHit(Object source, bool jumpable)
    {
        if (Time.time < nextHazardLogTime)
        {
            return;
        }

        PlayerLife playerLife = GetComponent<PlayerLife>();
        if (playerLife != null && !playerLife.TryTakeDamage(source))
        {
            return;
        }

        nextHazardLogTime = Time.time + hazardLogCooldown;
        string hazardKind = jumpable ? "jumpable" : "unjumpable";
        string sourceName = source != null ? source.name : "unknown source";
        Debug.LogWarning($"Player hit a {hazardKind} hazard on '{sourceName}'. Death/restart VFX will be added later.", source);
    }

    public void TeleportTo(Vector3 worldPosition)
    {
        rb.position = worldPosition;
        transform.position = worldPosition;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        Physics.SyncTransforms();
    }

    public void SetControlEnabled(bool enabledState)
    {
        controlsEnabled = enabledState;
        if (!controlsEnabled)
        {
            movement = Vector3.zero;
            IsSprinting = false;
            jumpQueued = false;
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rb.linearVelocity = velocity;
        }
    }

    private void CacheAnimatorParameters()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            hasSpeedParameter |= parameter.nameHash == SpeedParameter;
            hasGroundedParameter |= parameter.nameHash == GroundedParameter;
            hasJumpParameter |= parameter.nameHash == JumpParameter;
        }
    }

    private void UpdateAnimator(bool jumpedThisFrame)
    {
        if (animator == null)
        {
            return;
        }

        if (hasSpeedParameter)
        {
            animator.SetFloat(SpeedParameter, movement.magnitude * (IsSprinting ? 1f : .5f));
        }

        if (hasGroundedParameter)
        {
            animator.SetBool(GroundedParameter, IsGrounded);
        }

        if (hasJumpParameter && jumpedThisFrame)
        {
            animator.SetTrigger(JumpParameter);
        }
    }
}
