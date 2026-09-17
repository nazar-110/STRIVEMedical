using UnityEngine;

/*
 * Kinematics Follower
 * -------------------
 * Drives the tool assembly from live PacketProcessor telemetry or mock sliders.
 *
 * Each wrist joint drives one object in the hierarchy as a LOCAL rotation so
 * the axes are always relative to the parent — giving visually distinct motion.
 *
 * Recommended scene hierarchy:
 *   ToolController              ← this script (arm joints: ROTATE, REACH, LIFT)
 *     HugoInspoToolBase
 *       HugoInspoToolRotator    ← rollTransform   (j6 ROLL  — spins shaft)
 *         HugoInspoToolFirstGear  ← pitchTransform (j4 PITCH — first bend)
 *           HugoInspoToolSecondGear ← yawTransform (j5 YAW   — second bend)
 *
 * Joint mapping (matches firmware):
 *   J0 = ROTATE  (ODrive 0)   J3 = PITCH  (Encoder 0)
 *   J1 = REACH   (ODrive 1)   J4 = YAW    (Encoder 1)
 *   J2 = LIFT    (ODrive 2)   J5 = ROLL   (Encoder 2)
 */
public class KinematicsFollower : MonoBehaviour
{
    [Header("Wrist Transforms")]
    [Tooltip("HugoInspoToolRotator — driven by j6 ROLL (spins shaft around its local Z).")]
    public Transform rollTransform;

    [Tooltip("HugoInspoToolFirstGear — driven by j4 PITCH (first wrist bend, local X).")]
    public Transform pitchTransform;

    [Tooltip("HugoInspoToolSecondGear — driven by j5 YAW (second wrist bend, local Y).")]
    public Transform yawTransform;

    [Header("Arm Mapping")]
    [Tooltip("How far 1 degree of REACH (j2) moves the tool forward.")]
    public float forwardMultiplier = 0.005f;
    [Tooltip("Maximum forward/back reach allowed from joint 2.")]
    public float maxForwardOffset = 0.35f;
    [Tooltip("Overall response scaling for reach before limit easing is applied.")]
    public float reachResponseGain = 0.65f;
    [Tooltip("Higher values make reach slow down more as the angle approaches its extremes.")]
    public float reachResponseExponent = 2.2f;
    [Tooltip("How far 1 degree of joint 1 moves the tool horizontally.")]
    public float horizontalMultiplier = 0.0015f;
    [Tooltip("Degrees of joint 1 travel needed to reach full left/right response.")]
    public float maxHorizontalInputDegrees = 90f;
    [Tooltip("Maximum horizontal drift allowed from joint 1 to keep the tool in frame.")]
    public float maxHorizontalOffset = 0.2f;
    [Tooltip("Maximum yaw introduced as joint 1 approaches the horizontal limit.")]
    public float maxHorizontalTurn = 18f;
    [Tooltip("How far 1 degree of lift (j3) moves the tool downward.")]
    public float liftVerticalMultiplier = 0.0012f;
    [Tooltip("Maximum downward lift offset allowed to keep the tool in view.")]
    public float maxLiftVerticalOffset = 0.12f;
    [Tooltip("Maximum downward tilt introduced as lift approaches its limit.")]
    public float maxLiftTilt = 16f;
    [Tooltip("Positional offset from the arm's resting point.")]
    public Vector3 baseOffset = Vector3.zero;
    [Tooltip("Scales how much arm rotation is applied.")]
    public float rotationGain = 1f;
    [Tooltip("Scales forward/backward reach movement.")]
    public float positionGain = 1f;

    [Header("Direction Modifiers")]
    [Tooltip("Set to -1 if ROTATE (j1 / packet J0) rotates backwards in Unity compared to real life.")]
    public float joint1Direction = 1f;
    [Tooltip("Set to -1 if REACH (j2 / packet J1) moves backwards in Unity compared to real life.")]
    public float joint2Direction = -1f;
    [Tooltip("Set to -1 if LIFT (j3 / packet J2) rotates backwards in Unity compared to real life.")]
    public float joint3Direction = -1f;
    [Tooltip("Set to -1 if PITCH (j4 / packet J3) rotates backwards in Unity compared to real life.")]
    public float joint4Direction = -1f;
    [Tooltip("Set to -1 if YAW (j5 / packet J4) rotates backwards in Unity compared to real life.")]
    public float joint5Direction = -1f;
    [Tooltip("Set to -1 if ROLL (j6 / packet J5) rotates backwards in Unity compared to real life.")]
    public float joint6Direction = -1f;

    [Header("Wrist Compensation")]
    [Tooltip("How much joint 3's direction-modified angle should be added back into joint 4 to cancel the automatic straight-hand compensation from the real arm.")]
    public float joint4StraightHoldCompensation = 1f;
    [Tooltip("How much joint 1's direction-modified angle should be added back into joint 5 to cancel the automatic straight-hand compensation from the real arm.")]
    public float joint5StraightHoldCompensation = 1f;

    [Header("Movement Settings")]
    public float positionLerpSpeed = 20f;
    public float rotationLerpSpeed = 20f;
    public float positionScaleMultiplier = 1f;
    [Tooltip("How long the visual arm stays at home before telemetry is captured as the new zero after a home command.")]
    public float homeTelemetryRecaptureDelay = 3f;

    [Header("Mock Data Testing")]
    [Tooltip("Use the sliders below instead of live PacketProcessor data.")]
    public bool useMockData = false;
    [Tooltip("Automatically sweep all mock joints back and forth.")]
    public bool animateMockData = false;
    public float animationSpeed = 1f;
    public float animationAmplitude = 45f;

    [Range(-90f, 90f)] public float mockJoint1 = 0f;   // ROTATE
    [Range(-180f, 180f)] public float mockJoint2 = 0f;   // REACH
    [Range(-180f, 180f)] public float mockJoint3 = 0f;   // LIFT
    [Range(-90f, 90f)] public float mockJoint4 = 0f;   // PITCH
    [Range(-90f, 90f)] public float mockJoint5 = 0f;   // YAW
    [Range(-180f, 180f)] public float mockJoint6 = 0f;   // ROLL

    private bool       isInitialized;
    private Vector3    initialWorldPos;
    private Quaternion initialWorldRot;
    private Quaternion initialArmRotation;
    private float      initialRotateDeg;
    private float      initialReachDeg;
    private float      initialPitchDeg;
    private float      initialYawDeg;
    private float      initialRollDeg;
    private bool       isVisualHomeResetPending;
    private float      visualHomeResetTimer;
    private float      initialLiftDeg;
    private Rigidbody  rb;

    // Rest-pose local rotations of each wrist transform. Live joint angles are
    // applied relative to the first homed telemetry reading captured in Update.
    private Quaternion initialRollRot;
    private Quaternion initialPitchRot;
    private Quaternion initialYawRot;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        // Capture rest-pose rotations BEFORE stripping physics — the Rigidbody
        // may have offset the transform slightly if the HingeJoint was active.
        initialRollRot  = rollTransform  != null ? rollTransform.localRotation  : Quaternion.identity;
        initialPitchRot = pitchTransform != null ? pitchTransform.localRotation : Quaternion.identity;
        initialYawRot   = yawTransform   != null ? yawTransform.localRotation   : Quaternion.identity;

        // Strip all Rigidbodies/Joints from the wrist subtrees immediately so no
        // physics frame can move them before Update runs.
        MakeKinematicDirect(rollTransform);
        MakeKinematicDirect(pitchTransform);
        MakeKinematicDirect(yawTransform);
    }

    // Strips all physics joints and Rigidbodies from a wrist transform AND every
    // object in its subtree, turning them into pure transforms.
    //
    // Two important details:
    //   1. DestroyImmediate is used so the removal takes effect before the very
    //      first FixedUpdate — Object.Destroy is deferred to end-of-frame, which
    //      lets physics simulate for one tick and launch the gear.
    //   2. The call is recursive so nested Rigidbodies on child objects (e.g. the
    //      gear mesh children) can't fight the transform either.
    private static void MakeKinematicDirect(Transform t)
    {
        if (t == null) return;
        StripPhysicsRecursive(t);
    }

    private static void StripPhysicsRecursive(Transform t)
    {
        // Joints must go before the Rigidbody they reference.
        foreach (var joint in t.GetComponents<Joint>())
            DestroyImmediate(joint);

        var body = t.GetComponent<Rigidbody>();
        if (body != null) DestroyImmediate(body);

        for (int i = 0; i < t.childCount; i++)
            StripPhysicsRecursive(t.GetChild(i));
    }

    void Update()
    {
        if (isVisualHomeResetPending)
        {
            HoldVisualHomePose();
            visualHomeResetTimer -= Time.deltaTime;
            if (visualHomeResetTimer > 0f)
                return;

            isVisualHomeResetPending = false;
            isInitialized = false;
        }

        if (!TryGetJointAngles(out float j1, out float j2, out float j3,
                               out float j4, out float j5, out float j6))
            return;

        if (!isInitialized)
        {
            initialRotateDeg = j1;
            initialReachDeg = j2;
            initialLiftDeg = j3;
        }

        // ── Arm joints → THIS transform (root) ───────────────────────────────
        Quaternion armTargetRot = ComputeArmRotation(j1, j3);

        if (!isInitialized)
        {
            initialWorldPos    = transform.position;
            initialWorldRot    = transform.rotation;
            initialArmRotation = armTargetRot;
            initialReachDeg    = j2;
            initialPitchDeg    = j4;
            initialYawDeg      = j5;
            initialRollDeg     = j6;
            isInitialized      = true;
        }

        Quaternion armDelta = armTargetRot * Quaternion.Inverse(initialArmRotation);
        armDelta.ToAngleAxis(out float deltaAngleDeg, out Vector3 deltaAxis);
        if (deltaAxis.sqrMagnitude < Mathf.Epsilon) deltaAxis = Vector3.up;

        Quaternion scaledArmDelta = Quaternion.AngleAxis(deltaAngleDeg * rotationGain, deltaAxis.normalized);
        Quaternion worldTargetRot = scaledArmDelta * initialWorldRot;

        float forwardOffset = ComputeReachOffset(j2);
        float horizontalOffset = ComputeHorizontalOffset(j1);
        float liftOffset = ComputeLiftVerticalOffset(j3);
        // Position stays on global axes so reach remains a straight linear
        // push/pull even if the gripper starts with a rotational offset.
        Vector3 worldTargetPos = initialWorldPos
                               + baseOffset
                               + Vector3.forward * forwardOffset
                               + Vector3.right * horizontalOffset
                               + Vector3.down * liftOffset;

        Vector3    nextPos = Vector3.Lerp(transform.position, worldTargetPos, Time.deltaTime * positionLerpSpeed);
        Quaternion nextRot = Quaternion.Slerp(transform.rotation, worldTargetRot, Time.deltaTime * rotationLerpSpeed);

        if (rb != null && rb.isKinematic)
        {
            rb.MovePosition(nextPos);
            rb.MoveRotation(nextRot);
        }
        else
        {
            transform.position = nextPos;
            transform.rotation = nextRot;
        }

        // ── Wrist joints → each gear's LOCAL rotation ────────────────────────
        // Joint angles are applied as OFFSETS from the homed startup reading so
        // the gear sits in its designed scene pose when the lesson starts.
        // If a gear bends the wrong way, flip that joint's direction modifier.

        // ROLL (j6): shaft spins around its own forward axis → local X
        float pitchOffset = ComputeCompensatedPitchOffset(j3, j4);
        float yawOffset   = ComputeCompensatedYawOffset(j1, j5);
        float rollOffset  = (j6 - initialRollDeg) * joint6Direction;

        ApplyLocalRotation(rollTransform,  initialRollRot  * Quaternion.Euler(rollOffset, 0f, 0f));

        // PITCH (j4): first gear bends up/down → local Z (PitchPivot's orientation)
        ApplyLocalRotation(pitchTransform, initialPitchRot * Quaternion.Euler(0f, 0f, pitchOffset));

        // YAW (j5): second gear bends left/right → local Y
        ApplyLocalRotation(yawTransform,   initialYawRot   * Quaternion.Euler(0f, yawOffset, 0f));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Smoothly lerps a wrist transform to a target local rotation.
    // The Rigidbody was destroyed in Start so this is a simple transform op.
    private void ApplyLocalRotation(Transform t, Quaternion targetLocal)
    {
        if (t == null) return;
        t.localRotation = Quaternion.Slerp(t.localRotation, targetLocal, Time.deltaTime * rotationLerpSpeed);
    }

    public void ResetVisualToHome()
    {
        if (!isInitialized)
            return;

        isVisualHomeResetPending = true;
        visualHomeResetTimer = Mathf.Max(0f, homeTelemetryRecaptureDelay);
        HoldVisualHomePose();
    }

    private void HoldVisualHomePose()
    {
        if (!isInitialized)
            return;

        if (rb != null && rb.isKinematic)
        {
            rb.MovePosition(initialWorldPos);
            rb.MoveRotation(initialWorldRot);
        }
        else
        {
            transform.position = initialWorldPos;
            transform.rotation = initialWorldRot;
        }

        if (rollTransform != null) rollTransform.localRotation = initialRollRot;
        if (pitchTransform != null) pitchTransform.localRotation = initialPitchRot;
        if (yawTransform != null) yawTransform.localRotation = initialYawRot;
    }

    private Quaternion ComputeArmRotation(float j1Deg, float j3Deg)
    {
        float horizontalTurn = ComputeHorizontalTurn(j1Deg);
        float liftTilt = ComputeLiftTilt(j3Deg);
        Quaternion baseYaw  = Quaternion.Euler(0f, horizontalTurn, 0f);
        Quaternion armPitch = Quaternion.Euler(liftTilt, 0f, 0f);
        
        return baseYaw * armPitch;
    }

    private float ComputeReachOffset(float j2Deg)
    {
        float reachDelta = (j2Deg - initialReachDeg) * joint2Direction * forwardMultiplier * positionScaleMultiplier * positionGain;
        return ShapeReachOffset(reachDelta * reachResponseGain, maxForwardOffset);
    }

    private float ComputeHorizontalOffset(float j1Deg)
    {
        float rotateDelta = j1Deg - initialRotateDeg;
        if (maxHorizontalInputDegrees <= Mathf.Epsilon)
            return 0f;

        float normalizedDelta = Mathf.Clamp((rotateDelta * joint1Direction) / maxHorizontalInputDegrees, -1f, 1f);
        return normalizedDelta * maxHorizontalOffset;
    }

    private float ComputeHorizontalTurn(float j1Deg)
    {
        if (maxHorizontalOffset <= Mathf.Epsilon || maxHorizontalTurn <= 0f)
            return 0f;

        float horizontalOffset = ComputeHorizontalOffset(j1Deg);
        float normalizedTravel = Mathf.Clamp(horizontalOffset / maxHorizontalOffset, -1f, 1f);
        return normalizedTravel * maxHorizontalTurn;
    }

    private float ComputeLiftVerticalOffset(float j3Deg)
    {
        float liftDelta = (j3Deg - initialLiftDeg) * joint3Direction * liftVerticalMultiplier;
        return SoftClamp(liftDelta, maxLiftVerticalOffset);
    }

    private float ComputeCompensatedPitchOffset(float j3Deg, float j4Deg)
    {
        float pitchDelta = (j4Deg - initialPitchDeg) * joint4Direction;
        float liftDelta = (j3Deg - initialLiftDeg) * joint3Direction;
        return pitchDelta + (liftDelta * joint4StraightHoldCompensation);
    }

    private float ComputeCompensatedYawOffset(float j1Deg, float j5Deg)
    {
        float yawDelta = (j5Deg - initialYawDeg) * joint5Direction;
        float rotateDelta = (j1Deg - initialRotateDeg) * joint1Direction;
        return yawDelta + (rotateDelta * joint5StraightHoldCompensation);
    }

    private float ComputeLiftTilt(float j3Deg)
    {
        if (maxLiftVerticalOffset <= Mathf.Epsilon || maxLiftTilt <= 0f)
            return 0f;

        float liftOffset = ComputeLiftVerticalOffset(j3Deg);
        float normalizedTravel = Mathf.Clamp(liftOffset / maxLiftVerticalOffset, -1f, 1f);
        return normalizedTravel * maxLiftTilt;
    }

    private float SoftClamp(float value, float limit)
    {
        if (limit <= Mathf.Epsilon)
            return 0f;

        float normalized = value / limit;
        float softened = normalized / (1f + Mathf.Abs(normalized));
        return limit * softened;
    }

    private float ShapeReachOffset(float value, float limit)
    {
        if (limit <= Mathf.Epsilon)
            return 0f;

        float softlyClamped = SoftClamp(value, limit);
        float normalized = Mathf.Clamp(softlyClamped / limit, -1f, 1f);
        float magnitude = Mathf.Pow(Mathf.Abs(normalized), Mathf.Max(1f, reachResponseExponent));
        return Mathf.Sign(normalized) * magnitude * limit;
    }

    private bool TryGetJointAngles(
        out float j1, out float j2, out float j3,
        out float j4, out float j5, out float j6)
    {
        if (useMockData)
        {
            if (animateMockData)
            {
                float wave = Mathf.Sin(Time.time * animationSpeed) * animationAmplitude;
                j1 = mockJoint1 + wave; j2 = mockJoint2 + wave; j3 = mockJoint3 + wave;
                j4 = mockJoint4 + wave; j5 = mockJoint5 + wave; j6 = mockJoint6 + wave;
            }
            else
            {
                j1 = mockJoint1; j2 = mockJoint2; j3 = mockJoint3;
                j4 = mockJoint4; j5 = mockJoint5; j6 = mockJoint6;
            }
            return true;
        }

        var pp = PacketProcessor.PacketProcessor.Instance;
        if (pp == null || !pp.HasJointData)
        {
            j1 = j2 = j3 = j4 = j5 = j6 = 0f;
            return false;
        }

        j1 = pp.J0;   // ROTATE
        j2 = pp.J1;   // REACH
        j3 = pp.J2;   // LIFT
        j4 = pp.J3;   // PITCH
        j5 = pp.J4;   // YAW
        j6 = pp.J5;   // ROLL

        return !(float.IsNaN(j1) || float.IsNaN(j2) || float.IsNaN(j3) ||
                 float.IsNaN(j4) || float.IsNaN(j5) || float.IsNaN(j6));
    }
}
