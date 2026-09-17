// using UnityEngine;

// /*
//  * Direct Mapping Kinematics (Cylindrical Mapping)
//  * ---------------------------------------------------------
//  * This script completely ignores complex trigonometry and maps joints
//  * directly to specific 1:1 positional and rotational axes.
//  */
// public class ForwardKinematics : MonoBehaviour
// {
//     [Header("End Effector Target")]
//     public Transform endEffector;

//     [Header("Mapping Multipliers")]
//     [Tooltip("How far 1 degree of Joint 2 moves the arm forward/backward.")]
//     public float forwardMultiplier = 0.005f; 
//     [Tooltip("Offset for the starting position.")]
//     public Vector3 baseOffset = new Vector3(0, 0, 0);

//     [Header("User Joint Angles (degrees)")]
//     public float joint1Deg = 0f;   // ROTATE  (J0)
//     public float joint2Deg = 0f;   // REACH   (J1)
//     public float joint3Deg = 0f;   // LIFT    (J2)
//     public float joint4Deg = 0f;   // PITCH   (J3)
//     public float joint5Deg = 0f;   // YAW     (J4)
//     public float joint6Deg = 0f;   // ROLL    (J5)

//     // --------------------------------------------------------
//     //  DH chain: joints 1, 2, 3, 5
//     //  DHParams(a, alpha_rad, d, thetaOffset_deg)
//     //
//     //  Row  2 — joint 1: d=0.092177, theta_offset=-180, r=0.030485, alpha=-90.0001
//     //  Row  6 — joint 2: d=0.122285, theta_offset=-66.3862, r=0, alpha=-90
//     //  Row  7 — joint 3: d=0.122285, theta_offset=-66.3863, r=0, alpha=90
//     //  Row 15 — joint 5: d=-0.010251, theta_offset=-108.602, r=0.2, alpha=0
//     // --------------------------------------------------------
//     private DHParams[] dh;

//     private void ApplyPacketProcessorJointData()
//     {
//         if (PacketProcessor.PacketProcessor.Instance == null) return;

//         float j0 = PacketProcessor.PacketProcessor.Instance.J0; // ROTATE
//         float j1 = PacketProcessor.PacketProcessor.Instance.J1; // REACH
//         float j2 = PacketProcessor.PacketProcessor.Instance.J2; // LIFT
//         float j3 = PacketProcessor.PacketProcessor.Instance.J3; // PITCH
//         float j4 = PacketProcessor.PacketProcessor.Instance.J4; // YAW
//         float j5 = PacketProcessor.PacketProcessor.Instance.J5; // ROLL
//         float j6 = 0f;                                          // Gripper (removed)

//         if (float.IsNaN(j0) || float.IsNaN(j1) || float.IsNaN(j2) ||
//             float.IsNaN(j3) || float.IsNaN(j4) || float.IsNaN(j5))
//         {
//             return;

//         joint1Deg = j0;
//         joint2Deg = j1;
//         joint3Deg = j2;
//         joint4Deg = j3;
//         joint5Deg = j4;
//         joint6Deg = j5;
//     }

//     void Start()
//     {
//         dh = new DHParams[6];

//         // Joint 1 — ROTATE  (table row 2)
//         dh[0] = new DHParams(
//             a: 0.030485f,
//             alpha: -90.0001f * Mathf.Deg2Rad,
//             d: 0.092177f,
//             thetaOffsetDeg: -180f
//         );

//         // Joint 2 — REACH  (table row 6)
//         dh[1] = new DHParams(
//             a: 0f,
//             alpha: -90f * Mathf.Deg2Rad,
//             d: 0.122285f,
//             thetaOffsetDeg: -66.3862f
//         );

//         // Joint 3 — LIFT  (table row 7)
//         dh[2] = new DHParams(
//             a: 0f,
//             alpha: 90f * Mathf.Deg2Rad,
//             d: 0.122285f,
//             thetaOffsetDeg: -66.3863f
//         );

//         // Joint 4 — PITCH (placeholder — update with actual DH parameters)
//         dh[3] = new DHParams(
//             a: 0f,
//             alpha: 0f,
//             d: 0f,
//             thetaOffsetDeg: 0f
//         );

//         // Joint 5 — YAW  (table row 15)
//         dh[4] = new DHParams(
//             a: 0.2f,
//             alpha: 0f,
//             d: -0.010251f,
//             thetaOffsetDeg: -108.602f
//         );

//         // Joint 6 — ROLL (placeholder — update with actual DH parameters)
//         dh[5] = new DHParams(
//             a: 0f,
//             alpha: 0f,
//             d: 0f,
//             thetaOffsetDeg: 0f
//         );
//     }

//     // --------------------------------------------------------
//     //  Standard DH transform matrix for one joint:
//     //    T = Rot_z(theta_total) * Trans_z(d) * Trans_x(a) * Rot_x(alpha)
//     // --------------------------------------------------------
//     Matrix4x4 DHMatrix(DHParams p)
//     {
//         float theta = p.TotalTheta;

//         float ct = Mathf.Cos(theta);
//         float st = Mathf.Sin(theta);
//         float ca = Mathf.Cos(p.alpha);
//         float sa = Mathf.Sin(p.alpha);

//         // Column-major layout (Unity Matrix4x4 is column-major)
//         //
//         //  | ct   -st*ca   st*sa   a*ct |
//         //  | st    ct*ca  -ct*sa   a*st |
//         //  |  0      sa      ca      d  |
//         //  |  0       0       0      1  |

//         Matrix4x4 m = new Matrix4x4();
//         m.SetColumn(0, new Vector4(ct, st, 0f, 0f));
//         m.SetColumn(1, new Vector4(-st * ca, ct * ca, sa, 0f));
//         m.SetColumn(2, new Vector4(st * sa, -ct * sa, ca, 0f));
//         m.SetColumn(3, new Vector4(p.a * ct, p.a * st, p.d, 1f));

//         return m;
//     }

//     void Update()
//     {
//         if (PacketProcessor.PacketProcessor.Instance != null && PacketProcessor.PacketProcessor.Instance.HasJointData)
//         {
//             ApplyPacketProcessorJointData();
//         }

//         var pose = ComputePose(joint1Deg, joint2Deg, joint3Deg, joint4Deg, joint5Deg, joint6Deg, joint7Deg);

//         if (forward != Vector3.zero && up != Vector3.zero)
//         {
//             Quaternion rot = Quaternion.LookRotation(forward, up);
//             if (endEffector != null)
//             {
//                 endEffector.position = pos;
//                 endEffector.rotation = rot;
//             }
//         }

//         Debug.Log($"[FK] J1(ROTATE):{joint1Deg:F2}, J2(REACH):{joint2Deg:F2}, J3(LIFT):{joint3Deg:F2}, J4(PITCH):{joint4Deg:F2}, J5(YAW):{joint5Deg:F2}, J6(ROLL):{joint6Deg:F2} | Pos: {pos}");
//     }
// }
//     // --------------------------------------------------------
//     //  Utility: call this from editor or other scripts to get
//     //  the end-effector pose without needing a Transform.
//     // --------------------------------------------------------
//     public (Vector3 position, Quaternion rotation) ComputePose(
//         float j1Deg, float j2Deg, float j3Deg, float j4Deg, float j5Deg, float j6Deg)
//     {
//         DHParams[] local = new DHParams[6];
//         System.Array.Copy(dh, local, 6);

//         local[0].theta = j1Deg * Mathf.Deg2Rad;
//         local[1].theta = j2Deg * Mathf.Deg2Rad;
//         local[2].theta = j3Deg * Mathf.Deg2Rad;
//         local[3].theta = j4Deg * Mathf.Deg2Rad;
//         local[4].theta = j5Deg * Mathf.Deg2Rad;
//         local[5].theta = j6Deg * Mathf.Deg2Rad;

//         Matrix4x4 T = Matrix4x4.identity;
//         for (int i = 0; i < 4; i++)
//         {
//             T = T * DHMatrix(local[i]);
//         }

//         Vector3 pos = new Vector3(T.m03, T.m13, T.m23);
//         Vector3 forward = new Vector3(T.m02, T.m12, T.m22);
//         Vector3 up = new Vector3(T.m01, T.m11, T.m21);

//         Quaternion rot = (forward != Vector3.zero && up != Vector3.zero)
//             ? Quaternion.LookRotation(forward, up)
//             : Quaternion.identity;

//         return (finalPos, finalRot);
//     }
// }
