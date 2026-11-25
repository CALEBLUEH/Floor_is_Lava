using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class ElevatorFirstPersonMover : MonoBehaviour
{
    [SerializeField] private Camera eyeCamera;
    [SerializeField] private Transform visualRoot;
    [SerializeField] private float walkSpeed = 2.8f;
    [SerializeField] private float sprintSpeed = 4.5f;

    private CharacterController character;
    private float verticalVelocity;
    private bool canMove;

    public bool IsMoving { get; private set; }
    public bool IsSprinting { get; private set; }

    private void Awake()
    {
        character = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (character.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1.2f;
        verticalVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 direction = Vector3.zero;
        IsMoving = false;
        IsSprinting = false;
        if (canMove && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 forward = Vector3.ProjectOnPlane(eyeCamera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(eyeCamera.transform.right, Vector3.up).normalized;
            direction = forward * input.y + right * input.x;
            IsMoving = direction.sqrMagnitude > 0.001f;
            IsSprinting = IsMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (IsMoving && visualRoot != null)
                visualRoot.rotation = Quaternion.Slerp(visualRoot.rotation,
                    Quaternion.LookRotation(direction, Vector3.up), Time.deltaTime * 9f);
        }

        float speed = IsSprinting ? sprintSpeed : walkSpeed;
        character.Move((direction * speed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    public void SetCanMove(bool enabled)
    {
        canMove = enabled;
        if (!enabled) { IsMoving = false; IsSprinting = false; }
    }

#if UNITY_EDITOR
    public void Configure(Camera camera, Transform visual)
    {
        eyeCamera = camera;
        visualRoot = visual;
    }
#endif
}
