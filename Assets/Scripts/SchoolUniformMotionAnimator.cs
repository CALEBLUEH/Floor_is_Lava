using UnityEngine;

[DisallowMultipleComponent]
public sealed class SchoolUniformMotionAnimator : MonoBehaviour
{
    [SerializeField] private Animator humanoid;
    [SerializeField] private TopDownPlayer player;
    [SerializeField] private Rigidbody playerBody;
    [SerializeField, Range(10f, 60f)] private float walkArmAngle = 28f;
    [SerializeField, Range(10f, 75f)] private float sprintArmAngle = 48f;

    private Transform hips;
    private Transform chest;
    private Transform leftUpperArm;
    private Transform rightUpperArm;
    private Transform leftLowerArm;
    private Transform rightLowerArm;
    private Transform leftUpperLeg;
    private Transform rightUpperLeg;
    private Transform leftLowerLeg;
    private Transform rightLowerLeg;
    private Quaternion hipsBase;
    private Quaternion chestBase;
    private Quaternion leftArmBase;
    private Quaternion rightArmBase;
    private Quaternion leftLegBase;
    private Quaternion rightLegBase;
    private Vector3 visualBasePosition;
    private float cycle;
    private float airborneWeight;
    private float landingWeight;
    private bool wasGrounded = true;

    private void Awake()
    {
        if (player == null) player = GetComponentInParent<TopDownPlayer>();
        if (playerBody == null && player != null) playerBody = player.GetComponent<Rigidbody>();
        if (humanoid == null) humanoid = GetComponentInChildren<Animator>();
        visualBasePosition = transform.localPosition;
        if (humanoid == null || !humanoid.isHuman) return;

        hips = humanoid.GetBoneTransform(HumanBodyBones.Hips);
        chest = humanoid.GetBoneTransform(HumanBodyBones.Chest);
        leftUpperArm = humanoid.GetBoneTransform(HumanBodyBones.LeftUpperArm);
        rightUpperArm = humanoid.GetBoneTransform(HumanBodyBones.RightUpperArm);
        leftLowerArm = humanoid.GetBoneTransform(HumanBodyBones.LeftLowerArm);
        rightLowerArm = humanoid.GetBoneTransform(HumanBodyBones.RightLowerArm);
        leftUpperLeg = humanoid.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
        rightUpperLeg = humanoid.GetBoneTransform(HumanBodyBones.RightUpperLeg);
        leftLowerLeg = humanoid.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
        rightLowerLeg = humanoid.GetBoneTransform(HumanBodyBones.RightLowerLeg);
        hipsBase = RotationOf(hips);
        chestBase = RotationOf(chest);
        leftArmBase = RotationOf(leftUpperArm);
        rightArmBase = RotationOf(rightUpperArm);
        leftLegBase = RotationOf(leftUpperLeg);
        rightLegBase = RotationOf(rightUpperLeg);
    }

    private void LateUpdate()
    {
        if (player == null || playerBody == null || humanoid == null || !humanoid.isHuman) return;

        Vector3 velocity = playerBody.linearVelocity;
        float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
        bool moving = player.IsMoving && horizontalSpeed > .1f;
        bool airborne = !player.IsGrounded;
        bool sprinting = player.IsSprinting && moving;

        if (!wasGrounded && !airborne) landingWeight = 1f;
        wasGrounded = !airborne;
        airborneWeight = Mathf.MoveTowards(airborneWeight, airborne ? 1f : 0f,
            Time.deltaTime * (airborne ? 5f : 8f));
        landingWeight = Mathf.MoveTowards(landingWeight, 0f, Time.deltaTime * 4.5f);

        float cadence = sprinting ? 11f : 7.2f;
        cycle += Time.deltaTime * cadence;
        float stride = moving ? Mathf.Sin(cycle) : 0f;
        float targetAngle = sprinting ? sprintArmAngle : walkArmAngle;
        float blend = 1f - Mathf.Exp(-Time.deltaTime * 14f);

        float armSwing = moving ? stride * Mathf.Sin(targetAngle * Mathf.Deg2Rad) : 0f;
        float legSwing = moving ? stride * Mathf.Sin(targetAngle * .7f * Mathf.Deg2Rad) : 0f;
        Vector3 leftArmWalk = Vector3.down + transform.forward * armSwing - transform.right * .08f;
        Vector3 rightArmWalk = Vector3.down - transform.forward * armSwing + transform.right * .08f;
        Vector3 leftLegWalk = Vector3.down - transform.forward * legSwing;
        Vector3 rightLegWalk = Vector3.down + transform.forward * legSwing;

        float rising = Mathf.InverseLerp(-3f, 4.5f, velocity.y);
        Vector3 jumpArm = Vector3.Lerp(transform.forward * .42f + Vector3.down * .5f,
            transform.forward * .8f + Vector3.up * .12f, rising);
        Vector3 leftJumpLeg = transform.forward * Mathf.Lerp(.48f, .18f, rising) + Vector3.down;
        Vector3 rightJumpLeg = -transform.forward * Mathf.Lerp(.12f, .3f, rising) + Vector3.down;
        Vector3 landingLeg = transform.forward * .28f + Vector3.down;

        Vector3 leftArmDirection = Vector3.Slerp(leftArmWalk, jumpArm - transform.right * .15f, airborneWeight);
        Vector3 rightArmDirection = Vector3.Slerp(rightArmWalk, jumpArm + transform.right * .15f, airborneWeight);
        Vector3 leftLegDirection = Vector3.Slerp(leftLegWalk, leftJumpLeg, airborneWeight);
        Vector3 rightLegDirection = Vector3.Slerp(rightLegWalk, rightJumpLeg, airborneWeight);
        leftLegDirection = Vector3.Slerp(leftLegDirection, landingLeg - transform.right * .08f, landingWeight);
        rightLegDirection = Vector3.Slerp(rightLegDirection, landingLeg + transform.right * .08f, landingWeight);

        PointBone(leftUpperArm, leftLowerArm, leftArmBase, leftArmDirection, blend);
        PointBone(rightUpperArm, rightLowerArm, rightArmBase, rightArmDirection, blend);
        PointBone(leftUpperLeg, leftLowerLeg, leftLegBase, leftLegDirection, blend);
        PointBone(rightUpperLeg, rightLowerLeg, rightLegBase, rightLegDirection, blend);
        float chestPitch = Mathf.Lerp(sprinting ? 8f : 2f, rising > .5f ? -7f : 10f, airborneWeight) + landingWeight * 9f;
        Apply(chest, chestBase, Quaternion.Euler(chestPitch, 0f, -stride * 2f * (1f - airborneWeight)), blend);
        Apply(hips, hipsBase, Quaternion.Euler(0f, 0f, stride * 2.5f), blend);
        float bob = moving ? Mathf.Abs(Mathf.Sin(cycle * 2f)) * (sprinting ? .045f : .025f) : 0f;
        float jumpLift = airborneWeight * Mathf.Lerp(-.015f, .035f, rising);
        float landingCrouch = landingWeight * .09f;
        transform.localPosition = Vector3.Lerp(transform.localPosition,
            visualBasePosition + Vector3.up * (bob + jumpLift - landingCrouch), blend);
    }

    private static Quaternion RotationOf(Transform bone) => bone != null ? bone.localRotation : Quaternion.identity;

    private static void Apply(Transform bone, Quaternion baseRotation, Quaternion offset, float blend)
    {
        if (bone != null) bone.localRotation = Quaternion.Slerp(bone.localRotation, baseRotation * offset, blend);
    }

    private static void PointBone(Transform upper, Transform lower, Quaternion baseRotation,
        Vector3 desiredDirection, float blend)
    {
        if (upper == null || lower == null || desiredDirection.sqrMagnitude < .001f) return;

        Quaternion previousLocalRotation = upper.localRotation;
        upper.localRotation = baseRotation;
        Vector3 baseDirection = lower.position - upper.position;
        if (baseDirection.sqrMagnitude < .0001f)
        {
            upper.localRotation = previousLocalRotation;
            return;
        }

        Quaternion targetWorldRotation =
            Quaternion.FromToRotation(baseDirection.normalized, desiredDirection.normalized) * upper.rotation;
        Quaternion targetLocalRotation = upper.parent != null
            ? Quaternion.Inverse(upper.parent.rotation) * targetWorldRotation
            : targetWorldRotation;
        upper.localRotation = Quaternion.Slerp(previousLocalRotation, targetLocalRotation, blend);
    }

#if UNITY_EDITOR
    public void ConfigurePrefab(Animator targetAnimator, TopDownPlayer targetPlayer)
    {
        humanoid = targetAnimator;
        player = targetPlayer;
        playerBody = targetPlayer != null ? targetPlayer.GetComponent<Rigidbody>() : null;
    }
#endif
}
