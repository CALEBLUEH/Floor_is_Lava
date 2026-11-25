using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineCamera), typeof(CinemachineFollow))]
public sealed class BossCinemachineFollow : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private Transform followTarget;
    [SerializeField] private Camera previewCamera;

    [Header("Framing")]
    [SerializeField] private Vector3 followOffset = new(0f, 20f, -12f);
    [SerializeField] private Vector3 cameraEulerAngles = new(60f, 0f, 0f);
    [SerializeField, Range(20f, 90f)] private float fieldOfView = 55f;

    [Header("Delayed follow")]
    [SerializeField, Min(0f)] private float horizontalDamping = 1.2f;
    [SerializeField, Min(0f)] private float verticalDamping = .75f;
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

    private void LateUpdate()
    {
        if (!Application.isPlaying) ApplyEditorPreview();
    }

    private void ResolveComponents()
    {
        if (virtualCamera == null) virtualCamera = GetComponent<CinemachineCamera>();
        if (follow == null) follow = GetComponent<CinemachineFollow>();
    }

    private void ApplySettings()
    {
        if (virtualCamera == null || follow == null) return;
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
        ApplyEditorPreview();
    }

    private void ApplyEditorPreview()
    {
        if (Application.isPlaying || followTarget == null) return;
        transform.SetPositionAndRotation(
            followTarget.position + followOffset,
            Quaternion.Euler(cameraEulerAngles));
        if (previewCamera != null)
        {
            previewCamera.transform.SetPositionAndRotation(transform.position, transform.rotation);
            previewCamera.fieldOfView = fieldOfView;
        }
    }

#if UNITY_EDITOR
    public void ConfigureForEditor(Transform target, Camera mainCamera)
    {
        followTarget = target;
        previewCamera = mainCamera;
        ResolveComponents();
        ApplySettings();
    }
#endif
}
