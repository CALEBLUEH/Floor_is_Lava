using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera), typeof(CinemachineFollow))]
public sealed class TutorialCinemachineFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform followTarget;

    [Header("Framing")]
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 20f, -12f);
    [SerializeField] private Vector3 cameraEulerAngles = new Vector3(60f, 0f, 0f);
    [SerializeField, Range(20f, 90f)] private float fieldOfView = 55f;

    [Header("Delayed follow")]
    [SerializeField, Min(0f)] private float horizontalDamping = 1.2f;
    [SerializeField, Min(0f)] private float verticalDamping = 0.75f;
    [SerializeField, Min(0f)] private float depthDamping = 1.2f;

    private CinemachineCamera virtualCamera;
    private CinemachineFollow follow;

    private void Awake()
    {
        ResolveComponents();
        ApplySettings();
    }

    private void OnValidate()
    {
        ResolveComponents();
        ApplySettings();
    }

    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        ApplySettings();
    }

    [ContextMenu("Snap Camera To Target")]
    public void SnapCameraToTarget()
    {
        ResolveComponents();
        ApplySettings();
        if (followTarget == null || virtualCamera == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(cameraEulerAngles);
        virtualCamera.ForceCameraPosition(followTarget.position + followOffset, rotation);
    }

    private void ResolveComponents()
    {
        if (virtualCamera == null)
        {
            virtualCamera = GetComponent<CinemachineCamera>();
        }

        if (follow == null)
        {
            follow = GetComponent<CinemachineFollow>();
        }
    }

    private void ApplySettings()
    {
        if (virtualCamera == null || follow == null)
        {
            return;
        }

        virtualCamera.Follow = followTarget;
        LensSettings lens = virtualCamera.Lens;
        lens.FieldOfView = fieldOfView;
        virtualCamera.Lens = lens;

        follow.FollowOffset = followOffset;
        TrackerSettings tracking = follow.TrackerSettings;
        tracking.BindingMode = BindingMode.WorldSpace;
        tracking.PositionDamping = new Vector3(horizontalDamping, verticalDamping, depthDamping);
        tracking.RotationDamping = Vector3.zero;
        tracking.QuaternionDamping = 0f;
        follow.TrackerSettings = tracking;

        transform.rotation = Quaternion.Euler(cameraEulerAngles);
        if (!Application.isPlaying && followTarget != null)
        {
            transform.position = followTarget.position + followOffset;
        }
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(Transform target)
    {
        followTarget = target;
        ApplySettings();
        SnapCameraToTarget();
    }
#endif
}
