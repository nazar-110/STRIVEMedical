using UnityEngine;
using UnityEngine.Events;
using StriveMedical.UI;

namespace StriveMedical.Training
{
    /// <summary>
    /// Drop this component on any GameObject in the scene to define a training objective.
    ///
    /// ═══════════════════════════════════════════════════════════════════
    ///  HOW TO SET UP OBJECTIVES WITHOUT EXTRA PROGRAMMING
    /// ═══════════════════════════════════════════════════════════════════
    ///
    ///  APPROACH A — Invisible Trigger Zone (most common for positional goals)
    ///  ──────────────────────────────────────────────────────────────────
    ///  1. In the scene, create an empty GameObject (e.g. "Objective_HoldPosition").
    ///  2. Add a BoxCollider (or SphereCollider).  Tick "Is Trigger" ✓.
    ///  3. Scale / position it around the target area where the gripper must go.
    ///  4. Add THIS component (TrainingObjective).
    ///  5. Drag the HUD GameObject into the "Hud" slot in the Inspector.
    ///  6. Set "Tag To Detect" to whatever tag your gripper has (e.g. "Gripper").
    ///  7. Set "Trigger Mode" to OnEnter, OnExit, or OnStay depending on the goal.
    ///     • OnEnter  — fires the moment the gripper enters the zone
    ///     • OnStay   — fires only after the gripper has been inside for N seconds
    ///     • OnExit   — fires when the gripper leaves the zone
    ///  8. Play the scene — when the gripper reaches the zone the step auto-completes.
    ///
    ///  APPROACH B — Inspector UnityEvent (for non-collider objectives)
    ///  ──────────────────────────────────────────────────────────────────
    ///  Use the "On Objective Reached" UnityEvent in the Inspector to chain
    ///  calls to other components without writing code.  For example:
    ///    • Call Animator.SetTrigger to play a success animation
    ///    • Call AudioSource.Play to play a completion sound
    ///    • Call another TrainingObjective.Activate to chain objectives
    ///
    ///  APPROACH C — Call from code
    ///  ──────────────────────────────────────────────────────────────────
    ///  From any MonoBehaviour (e.g. your gripper script, a timer, a
    ///  force-feedback handler):
    ///      objective.TriggerObjective();
    ///  Or reach the HUD directly if you have a reference:
    ///      hud.CompleteCurrentStep();
    ///
    /// ═══════════════════════════════════════════════════════════════════
    /// </summary>
    public class TrainingObjective : MonoBehaviour
    {
        // ── Inspector fields ──────────────────────────────────────────────
        [Header("HUD Connection")]
        [Tooltip("Drag the GameObject that has SimulatorHUDController here.")]
        public SimulatorHUDController hud;

        [Header("Trigger Settings")]
        [Tooltip("Which tag on the colliding object fires this objective? " +
                 "Set the same tag on your gripper/tool in the Inspector.")]
        public string tagToDetect = "Gripper";

        public enum TriggerMode
        {
            OnEnter,   // fires the moment the object enters the collider
            OnStay,    // fires after the object has been inside for 'stayDuration' seconds
            OnExit,    // fires when the object leaves the collider
            Manual,    // never fires automatically — call TriggerObjective() from code
        }
        [Tooltip("When should this objective fire?")]
        public TriggerMode triggerMode = TriggerMode.OnEnter;

        [Tooltip("Only used when Trigger Mode = OnStay. " +
                 "How long (seconds) the gripper must remain inside the zone.")]
        [Min(0f)]
        public float stayDuration = 3f;

        [Header("One-Shot")]
        [Tooltip("If true, this objective can only fire once. " +
                 "Prevents double-counting if the gripper re-enters the zone.")]
        public bool fireOnce = true;

        [Header("Events")]
        [Tooltip("Optional: wire up additional effects in the Inspector " +
                 "(play sound, show particle, call Animator, etc.)")]
        public UnityEvent onObjectiveReached;

        // ── Private state ─────────────────────────────────────────────────
        bool  _triggered  = false;
        bool  _inside     = false;
        float _timeInside = 0f;

        // ── Unity messages ────────────────────────────────────────────────
        void Update()
        {
            if (triggerMode != TriggerMode.OnStay) return;
            if (!_inside || (_triggered && fireOnce)) return;

            _timeInside += Time.deltaTime;
            if (_timeInside >= stayDuration)
                TriggerObjective();
        }

        void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(tagToDetect)) return;
            _inside = true;
            _timeInside = 0f;

            if (triggerMode == TriggerMode.OnEnter)
                TriggerObjective();
        }

        void OnTriggerStay(Collider other)
        {
            // OnStay accumulation is handled in Update for frame-rate independence.
            // This method intentionally left empty.
        }

        void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(tagToDetect)) return;
            _inside = false;
            _timeInside = 0f;

            if (triggerMode == TriggerMode.OnExit)
                TriggerObjective();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>
        /// Fire this objective manually from code or from another UnityEvent.
        /// Safe to call from any script — checks fireOnce automatically.
        /// </summary>
        public void TriggerObjective()
        {
            if (_triggered && fireOnce) return;
            _triggered = true;

            // Tell the HUD to mark the current step done + show banner + advance
            if (hud != null)
                hud.CompleteCurrentStep();
            else
                Debug.LogWarning($"[TrainingObjective] '{name}' has no HUD reference assigned!", this);

            // Fire any additional Inspector-wired callbacks (sound, FX, etc.)
            onObjectiveReached?.Invoke();
        }

        /// <summary>
        /// Reset this objective so it can fire again (e.g. for repeat-cycle exercises).
        /// </summary>
        public void Reset()
        {
            _triggered  = false;
            _inside     = false;
            _timeInside = 0f;
        }

        /// <summary>Returns true if this objective has already been triggered.</summary>
        public bool IsTriggered => _triggered;

        // ── Editor gizmo — shows the zone in the Scene view ──────────────
#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            Gizmos.color = _triggered
                ? new Color(0.2f, 0.9f, 0.3f, 0.25f)
                : new Color(0.2f, 0.6f, 1f, 0.20f);

            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(
                    transform.TransformPoint(box.center),
                    transform.rotation,
                    transform.lossyScale);
                Gizmos.DrawCube(Vector3.zero, box.size);
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.8f);
                Gizmos.DrawWireCube(Vector3.zero, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.TransformPoint(sphere.center), sphere.radius);
            }

            // Label
            UnityEditor.Handles.color = Color.white;
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.15f,
                $"Objective\n[{triggerMode}]");
        }
#endif
    }
}
