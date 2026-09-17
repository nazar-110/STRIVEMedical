using UnityEngine;
using StriveMedical.UI;

/// <summary>
/// Manages the 3-step Demo training objective sequence:
///   Step 1: Place Cube(1) in the box
///   Step 2: Remove Cube(1) from the box (triggers on exit from box interior)
///   Step 3: Poke the needle into the flesh target (triggers on collision contact)
///
/// This script listens for physics trigger events on the box interior
/// and collision events on the flesh target, all created by DemoSceneSetup.
///
/// SETUP:
///   1. Add this component to the same "DemoSetup" GameObject as DemoSceneSetup.
///   2. Drag the HUD GameObject (with SimulatorHUDController) into the "Hud" slot.
///   3. Drag the existing "Cube (1)" object into the "Cube" slot.
///   4. Press Play — objectives fire automatically as triggers are hit.
///
/// NOTE: The cube and needle must have Rigidbodies for trigger detection to work.
///       DemoSceneSetup creates the needle with a Rigidbody already.
///       Cube(1) already has a Rigidbody in the scene.
/// </summary>
public class DemoObjectives : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Drag the HUD GameObject here")]
    public SimulatorHUDController hud;

    [Tooltip("Drag the Cube (1) GameObject here")]
    public GameObject cube;

    [Header("State (read-only in Inspector)")]
    [SerializeField] private int currentStep = 0; // 0 = waiting for step 1

    // Internal references to trigger zones (set by DemoSceneSetup)
    private DemoSceneSetup _setup;

    void Start()
    {
        _setup = GetComponent<DemoSceneSetup>();
        if (_setup == null)
        {
            Debug.LogError("[DemoObjectives] DemoSceneSetup component not found on the same GameObject!");
            return;
        }

        if (hud == null)
        {
            hud = FindFirstObjectByType<SimulatorHUDController>();
            if (hud == null)
                Debug.LogError("[DemoObjectives] SimulatorHUDController not found in scene!");
        }

        // Attach trigger listener to the box interior zone (handles Step 1 enter + Step 2 exit)
        if (_setup.boxInteriorTrigger != null)
        {
            var boxListener = _setup.boxInteriorTrigger.AddComponent<BoxTriggerListener>();
            boxListener.objectives = this;
            boxListener.zoneType = BoxTriggerListener.ZoneType.Box;
        }

        // Attach trigger listener to the flesh target itself (Step 3: needle pierces flesh)
        if (_setup.fleshTarget != null)
        {
            var fleshListener = _setup.fleshTarget.AddComponent<FleshTriggerListener>();
            fleshListener.objectives = this;
        }
    }

    /// <summary>
    /// Called by BoxTriggerListener when an object ENTERS a trigger zone.
    /// Step 1: cube enters the box.
    /// </summary>
    public void OnObjectEnteredZone(BoxTriggerListener.ZoneType zone, GameObject obj)
    {
        if (currentStep == 0 && zone == BoxTriggerListener.ZoneType.Box && IsCube(obj))
        {
            Debug.Log("[DemoObjectives] Step 1 complete: Cube placed in box!");
            currentStep = 1;
            if (hud != null) hud.CompleteCurrentStep();
        }
    }

    /// <summary>
    /// Called by BoxTriggerListener when an object EXITS a trigger zone.
    /// Step 2: cube removed from the box.
    /// </summary>
    public void OnObjectExitedZone(BoxTriggerListener.ZoneType zone, GameObject obj)
    {
        if (currentStep == 1 && zone == BoxTriggerListener.ZoneType.Box && IsCube(obj))
        {
            Debug.Log("[DemoObjectives] Step 2 complete: Cube removed from box!");
            currentStep = 2;
            if (hud != null) hud.CompleteCurrentStep();
        }
    }

    /// <summary>
    /// Called by FleshTriggerListener when the needle physically enters the flesh trigger.
    /// Step 3: needle pierces flesh target.
    /// </summary>
    public void OnNeedleTouchedFlesh(GameObject obj)
    {
        if (IsNeedle(obj))
        {
            if (currentStep == 2)
            {
                Debug.Log("[DemoObjectives] Step 3 complete: Needle in flesh! Module complete!");
                currentStep = 3;
                if (hud != null) hud.CompleteCurrentStep();
            }

            // Make the needle "stick" using thick drag instead of completely freezing
            // This allows the grabber to still pull it out
            var needleRb = obj.GetComponent<Rigidbody>();
            if (needleRb != null)
            {
                needleRb.useGravity = false;
                needleRb.linearDamping = 100f;
                needleRb.angularDamping = 100f;
            }
        }
    }

    /// <summary>
    /// Called when the needle is pulled out of the flesh.
    /// Restores normal physics.
    /// </summary>
    public void OnNeedleExitedFlesh(GameObject obj)
    {
        if (IsNeedle(obj))
        {
            var needleRb = obj.GetComponent<Rigidbody>();
            if (needleRb != null)
            {
                needleRb.useGravity = true;
                needleRb.linearDamping = 0f;
                needleRb.angularDamping = 0.05f;
            }
        }
    }

    bool IsCube(GameObject obj)
    {
        // Match by reference or name — the scene object is "Cube (1)"
        if (cube != null)
            return obj == cube || obj.transform.root.gameObject == cube;

        return obj.name == "Cube (1)";
    }

    bool IsNeedle(GameObject obj)
    {
        return obj.name == "DemoNeedle" ||
               (obj.transform.root != null && obj.transform.root.name == "DemoNeedle");
    }
}

/// <summary>
/// Trigger listener for the box interior zone.
/// Forwards both OnTriggerEnter (Step 1) and OnTriggerExit (Step 2) to DemoObjectives.
/// </summary>
public class BoxTriggerListener : MonoBehaviour
{
    public enum ZoneType { Box, Table, Flesh }

    [HideInInspector] public DemoObjectives objectives;
    [HideInInspector] public ZoneType zoneType;

    void OnTriggerEnter(Collider other)
    {
        if (objectives == null) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        objectives.OnObjectEnteredZone(zoneType, rb.gameObject);
    }

    void OnTriggerExit(Collider other)
    {
        if (objectives == null) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        objectives.OnObjectExitedZone(zoneType, rb.gameObject);
    }
}

/// <summary>
/// Trigger listener on the flesh target block.
/// Fires when another rigidbody (the needle) enters the flesh surface.
/// </summary>
public class FleshTriggerListener : MonoBehaviour
{
    [HideInInspector] public DemoObjectives objectives;

    void OnTriggerEnter(Collider other)
    {
        if (objectives == null) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        objectives.OnNeedleTouchedFlesh(rb.gameObject);
    }

    void OnTriggerExit(Collider other)
    {
        if (objectives == null) return;
        var rb = other.attachedRigidbody;
        if (rb == null) return;
        objectives.OnNeedleExitedFlesh(rb.gameObject);
    }
}
