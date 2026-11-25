using UnityEngine;

public sealed class ElevatorCharacterMotionAnimator : MonoBehaviour
{
    [SerializeField] private Animator humanoid;
    [SerializeField] private ElevatorFirstPersonMover mover;

    private Transform leftArm;
    private Transform rightArm;
    private Transform leftLeg;
    private Transform rightLeg;
    private Transform leftForearm;
    private Transform rightForearm;
    private Transform leftCalf;
    private Transform rightCalf;
    private float cycle;

    private void Awake()
    {
        if (humanoid == null) humanoid = GetComponentInChildren<Animator>();
        if (mover == null) mover = GetComponentInParent<ElevatorFirstPersonMover>();
        if (humanoid == null || !humanoid.isHuman) return;
        leftArm = humanoid.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightArm = humanoid.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftForearm = humanoid.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightForearm = humanoid.GetBoneTransform(HumanBodyBones.RightLowerArm);
        leftLeg = humanoid.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightLeg = humanoid.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        leftCalf = humanoid.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightCalf = humanoid.GetBoneTransform(HumanBodyBones.RightLowerLeg);
    }

    private void LateUpdate()
    {
        if (humanoid == null || !humanoid.isHuman || mover == null) return;
        bool moving = mover.IsMoving;
        cycle += Time.deltaTime * (mover.IsSprinting ? 11f : 7f);
        float swing = moving ? Mathf.Sin(cycle) * (mover.IsSprinting ? 0.45f : 0.28f) : 0f;
        float blend = 1f - Mathf.Exp(-Time.deltaTime * 12f);
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Point(leftArm, leftForearm, Vector3.down + forward * swing - right * 0.10f, blend);
        Point(rightArm, rightForearm, Vector3.down - forward * swing + right * 0.10f, blend);
        Point(leftLeg, leftCalf, Vector3.down - forward * swing, blend);
        Point(rightLeg, rightCalf, Vector3.down + forward * swing, blend);
    }

    private static void Point(Transform upper, Transform lower, Vector3 direction, float blend)
    {
        if (upper == null || lower == null) return;
        Vector3 current = lower.position - upper.position;
        if (current.sqrMagnitude < 0.0001f) return;
        Quaternion rotation = Quaternion.FromToRotation(current.normalized, direction.normalized) * upper.rotation;
        upper.rotation = Quaternion.Slerp(upper.rotation, rotation, blend);
    }

#if UNITY_EDITOR
    public void Configure(Animator animator, ElevatorFirstPersonMover player)
    {
        humanoid = animator;
        mover = player;
    }
#endif
}
