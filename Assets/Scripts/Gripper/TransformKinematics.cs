using UnityEngine;
using UnityEngine.Serialization;

/*
 * Digital Twin Skeleton Kinematics
 * ---------------------------------------------------------
 * This script drives a hierarchy of empty GameObjects (the "Digital Twin")
 * by directly applying motor angles to their local rotations.
 * Because the Unity hierarchy perfectly mirrors the physical hardware 
 * (and uses a Position Constraint for the parallelogram), this completely 
 * eliminates all complex forward kinematics math!
 */
[ExecuteAlways]
public class TransformKinematics : MonoBehaviour
{
    [Header("Testing & Mock Data")]
    [Tooltip("Check this to test the arm using the sliders below instead of the USB data.")]
    public bool useMockData = true;
    
    [Tooltip("Check this to use the Forward Push slider below to move J1 and J2 together!")]
    public bool useForwardPushSlider = false;
    [Range(-90f, 90f)] public float mockForwardPush = 0f;
    
    [FormerlySerializedAs("mockJ1")]
    public float mockJ0 = 0f;
    [FormerlySerializedAs("mockJ2")]
    public float mockJ1 = 0f;
    [FormerlySerializedAs("mockJ3")]
    public float mockJ2 = 0f;
    [FormerlySerializedAs("mockJ4")]
    public float mockJ3 = 0f;
    [FormerlySerializedAs("mockJ5")]
    public float mockJ4 = 0f;
    [FormerlySerializedAs("mockJ6")]
    public float mockJ5 = 0f;

    [Header("Digital Twin Skeleton Pivot Points")]
    [Tooltip("Packet J0 (ROTATE - Base Turntable)")]
    [FormerlySerializedAs("baseTurntable")]
    public Transform baseRotate;

    [Tooltip("Packet J1 (REACH - Lower Arm Pitch)")]
    [FormerlySerializedAs("lowerArm")]
    public Transform lowerArmReach;

    [Tooltip("Packet J2 (LIFT - Upper Arm Pitch) - Should be a child of the Base, NOT the Lower Arm!")]
    [FormerlySerializedAs("upperArm")]
    public Transform upperArmLift;

    [Tooltip("Packet J3 (PITCH - Gimbal Pitch)")]
    public Transform gimbalPitch;

    [Tooltip("Packet J4 (YAW - Gimbal Yaw)")]
    public Transform gimbalYaw;

    [Tooltip("Packet J5 (ROLL - Gimbal Roll)")]
    public Transform gimbalRoll;

    [Header("Rotation Axes")]
    [Tooltip("Which local axis does this joint rotate around? Usually (0,1,0) for Yaw and (1,0,0) for Pitch.")]
    [FormerlySerializedAs("j1Axis")]
    public Vector3 j0Axis = new Vector3(0, 1, 0); // J0 Base Rotate (Yaw)
    [FormerlySerializedAs("j2Axis")]
    public Vector3 j1Axis = new Vector3(1, 0, 0); // J1 Reach (Pitch)
    [FormerlySerializedAs("j3Axis")]
    public Vector3 j2Axis = new Vector3(1, 0, 0); // J2 Lift (Pitch)
    [FormerlySerializedAs("j4Axis")]
    public Vector3 j3Axis = new Vector3(1, 0, 0); // J3 Gimbal Pitch
    [FormerlySerializedAs("j5Axis")]
    public Vector3 j4Axis = new Vector3(0, 1, 0); // J4 Gimbal Yaw
    [FormerlySerializedAs("j6Axis")]
    public Vector3 j5Axis = new Vector3(0, 0, 1); // J5 Gimbal Roll

    [Header("Calibration Offsets (Degrees)")]
    [FormerlySerializedAs("j1Offset")]
    public float j0Offset = 0f;
    [FormerlySerializedAs("j2Offset")]
    public float j1Offset = 0f;
    [FormerlySerializedAs("j3Offset")]
    public float j2Offset = 0f;
    [FormerlySerializedAs("j4Offset")]
    public float j3Offset = 0f;
    [FormerlySerializedAs("j5Offset")]
    public float j4Offset = 0f;
    [FormerlySerializedAs("j6Offset")]
    public float j5Offset = 0f;

    [Header("Direction Modifiers")]
    [Tooltip("Set to -1 if the joint rotates backwards in Unity compared to real life.")]
    [FormerlySerializedAs("j1Direction")]
    public float j0Direction = 1f;
    [FormerlySerializedAs("j2Direction")]
    public float j1Direction = 1f;
    [FormerlySerializedAs("j3Direction")]
    public float j2Direction = 1f;
    [FormerlySerializedAs("j4Direction")]
    public float j3Direction = 1f;
    [FormerlySerializedAs("j5Direction")]
    public float j4Direction = 1f;
    [FormerlySerializedAs("j6Direction")]
    public float j5Direction = 1f;

    [Header("Gear Ratios / Multipliers")]
    [Tooltip("Multiply the raw incoming degrees by this value (e.g., for gear reductions).")]
    [FormerlySerializedAs("j1Multiplier")]
    public float j0Multiplier = 1f;
    [FormerlySerializedAs("j2Multiplier")]
    public float j1Multiplier = 1f;
    [FormerlySerializedAs("j3Multiplier")]
    public float j2Multiplier = 1f;
    [FormerlySerializedAs("j4Multiplier")]
    public float j3Multiplier = 1f;
    [FormerlySerializedAs("j5Multiplier")]
    public float j4Multiplier = 1f;
    [FormerlySerializedAs("j6Multiplier")]
    public float j5Multiplier = 1f;

    void OnValidate()
    {
        // This guarantees the arm moves instantly when you drag a slider in the Unity Editor!
        if (!Application.isPlaying && useMockData)
        {
            ApplyKinematics(mockJ0, mockJ1, mockJ2, mockJ3, mockJ4, mockJ5);
        }
    }

    void Update()
    {
        float j0, j1, j2, j3, j4, j5;

        if (useMockData)
        {
            if (useForwardPushSlider)
            {
                // Exact Inverse Kinematics to draw a perfectly straight horizontal line!
                // Based on your CAD lengths:
                float L1 = 0.14792f;
                float L2 = 0.19033f;
                
                // J1 (Reach) swings forward
                float theta1_rad = mockForwardPush * Mathf.Deg2Rad;
                
                // J2 (Lift) pitches UP by the exact mathematical amount required to keep the wrist at a constant height!
                float sinTheta2 = (L1 / L2) * (1f - Mathf.Cos(theta1_rad));
                float theta2_rad = Mathf.Asin(Mathf.Clamp(sinTheta2, -1f, 1f));
                
                j0 = mockJ0;
                j1 = mockForwardPush;
                j2 = theta2_rad * Mathf.Rad2Deg; 
                j3 = mockJ3;
                j4 = mockJ4;
                j5 = mockJ5;
            }
            else
            {
                j0 = mockJ0;
                j1 = mockJ1;
                j2 = mockJ2;
                j3 = mockJ3;
                j4 = mockJ4;
                j5 = mockJ5;
            }
        }
        else
        {
            // If not playing or no instance exists, don't crash
            if (!Application.isPlaying || PacketProcessor.PacketProcessor.Instance == null || !PacketProcessor.PacketProcessor.Instance.HasJointData)
                return;

            j0 = PacketProcessor.PacketProcessor.Instance.J0;
            j1 = PacketProcessor.PacketProcessor.Instance.J1;
            j2 = PacketProcessor.PacketProcessor.Instance.J2;
            j3 = PacketProcessor.PacketProcessor.Instance.J3;
            j4 = PacketProcessor.PacketProcessor.Instance.J4;
            j5 = PacketProcessor.PacketProcessor.Instance.J5;
        }

        ApplyKinematics(j0, j1, j2, j3, j4, j5);
    }

    private void ApplyKinematics(float j0, float j1, float j2, float j3, float j4, float j5)
    {
        // Apply angles directly to the virtual skeleton pivots!
        // We use Quaternion.AngleAxis so it rotates around the specific axis you define (X, Y, or Z).

        if (baseRotate != null)
            baseRotate.localRotation = Quaternion.AngleAxis((j0 * j0Multiplier + j0Offset) * j0Direction, j0Axis);

        if (lowerArmReach != null)
            lowerArmReach.localRotation = Quaternion.AngleAxis((j1 * j1Multiplier + j1Offset) * j1Direction, j1Axis);

        if (upperArmLift != null)
            upperArmLift.localRotation = Quaternion.AngleAxis((j2 * j2Multiplier + j2Offset) * j2Direction, j2Axis);

        if (gimbalPitch != null)
            gimbalPitch.localRotation = Quaternion.AngleAxis((j3 * j3Multiplier + j3Offset) * j3Direction, j3Axis);

        if (gimbalYaw != null)
            gimbalYaw.localRotation = Quaternion.AngleAxis((j4 * j4Multiplier + j4Offset) * j4Direction, j4Axis);

        if (gimbalRoll != null)
            gimbalRoll.localRotation = Quaternion.AngleAxis((j5 * j5Multiplier + j5Offset) * j5Direction, j5Axis);
    }
}
