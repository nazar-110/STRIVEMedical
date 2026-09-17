using UnityEngine;
using UnityEngine.InputSystem;

/*
 * VR-Style Physics Grabber (newGrabScript)
 * ----------------------------------------
 * This script allows you to grab and move Rigidbody objects using physics-based velocity tracking.
 * This prevents objects from clipping through walls or tables while you hold them.
 * 
 * SETUP INSTRUCTIONS:
 * 1. Create an Empty GameObject to act as your "Hand" or "Grabber".
 * 2. Attach this script to the Grabber object.
 * 3. (Optional) Attach a script like MouseFollower.cs to the Grabber so it moves with your mouse.
 * 4. Ensure the objects you want to grab have a BoxCollider (or similar) and a Rigidbody (Use Gravity = true, Is Kinematic = false).
 * 5. Press Play. Move the Grabber near a Rigidbody and hold Left Click to grab it!
 * 
 * PROPERTIES:
 * - Grab Radius: How close the grabber needs to be to pick up an object.
 * - Grabbable Layers: Restrict which objects can be grabbed.
 * - Snap To Center: If true, the grabbed object snaps perfectly to the grabber's position/rotation.
 * - Stiffness (Position/Rotation): How strongly the object tries to match the grabber's movement. Increase if the object is heavy.
 * - Max Velocity: Prevents physics explosions by capping how fast the object can fly towards the grabber.
 */
public class newGrabScript : MonoBehaviour
{
    [Header("Grab Settings")]
    [Tooltip("Attach a Collider (Box, Sphere, Mesh) to this object and check 'Is Trigger'.")]
    public LayerMask grabbableLayers = ~0; // Everything by default
    public bool snapToCenter = true; // Set to true to snap the object to the middle
    public float spinSpeed = 360f; // Speed at which the object spins when R or T is pressed
    
    
    [Header("Physics Tracking Settings")]
    public float positionStiffness = 50f;
    public float rotationStiffness = 50f;
    public float maxVelocity = 20f;
    public float maxAngularVelocity = 20f;

    [Header("Jaw Animation Settings")]
    public Transform jaw1;
    public Transform jaw2;
    public Vector3 jaw1OpenRotation;
    public Vector3 jaw1ClosedRotation;
    public Vector3 jaw2OpenRotation;
    public Vector3 jaw2ClosedRotation;
    public float jawAnimationSpeed = 15f;
    public float jawMaxOpenWidth = 0.5f;
    [Tooltip("Increase this if the jaws clip through the object, decrease if they hover above it.")]
    public float jawPadding = 0f;

    [Header("Testing")]
    [Tooltip("Check this box in the inspector to test grabbing without a mouse or physical arm.")]
    public bool mockTrigger = false;

    private Rigidbody grabbedObject;
    private float currentGrabWidth = 0f;
    private Vector3 positionOffset;
    private Quaternion rotationOffset;
    private bool wasGravityEnabled;

    private Collider[] gripperColliders;
    private Collider[] grabbedColliders;
    private System.Collections.Generic.List<Rigidbody> objectsInZone = new System.Collections.Generic.List<Rigidbody>();

    void Start()
    {
        gripperColliders = GetComponentsInChildren<Collider>();
    }

    void OnTriggerEnter(Collider other)
    {
        // Check if the layer is in the grabbable mask
        if (((1 << other.gameObject.layer) & grabbableLayers) == 0) return;

        Rigidbody rb = other.attachedRigidbody;
        // Ignore if no rigidbody, if kinematic, or if it's our own arm
        if (rb != null && !rb.isKinematic && rb.transform.root != transform.root)
        {
            if (!objectsInZone.Contains(rb))
            {
                objectsInZone.Add(rb);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        Rigidbody rb = other.attachedRigidbody;
        if (rb != null && objectsInZone.Contains(rb))
        {
            objectsInZone.Remove(rb);
        }
    }

    void Update()
    {
        bool isGrabbing = false;

        // Check inspector mock trigger
        if (mockTrigger)
        {
            isGrabbing = true;
        }

        // Check physical hardware trigger
        if (PacketProcessor.PacketProcessor.Instance != null && PacketProcessor.PacketProcessor.Instance.TriggerPressed)
        {
            isGrabbing = true;
        }

        // Keep mouse input for testing in editor
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            isGrabbing = true;
        }

        if (isGrabbing)
        {
            if (grabbedObject == null)
            {
                Grab();
            }
        }
        else
        {
            if (grabbedObject != null)
            {
                Release();
            }
        }

        // Handle spinning while grabbed
        if (grabbedObject != null && Keyboard.current != null)
        {
            if (Keyboard.current.rKey.isPressed)
            {
                rotationOffset *= Quaternion.Euler(spinSpeed * Time.deltaTime, 0, 0);
            }
            if (Keyboard.current.tKey.isPressed)
            {
                rotationOffset *= Quaternion.Euler(0, spinSpeed * Time.deltaTime, 0);
            }
        }

        // Handle Jaw Animation
        if (jaw1 != null && jaw2 != null)
        {
            Vector3 targetRot1 = jaw1OpenRotation;
            Vector3 targetRot2 = jaw2OpenRotation;

            if (isGrabbing)
            {
                if (grabbedObject != null)
                {
                    // Stop the jaws from closing all the way if holding an object
                    float grabRatio = Mathf.Clamp01((currentGrabWidth + jawPadding) / jawMaxOpenWidth);
                    targetRot1 = Vector3.Lerp(jaw1ClosedRotation, jaw1OpenRotation, grabRatio);
                    targetRot2 = Vector3.Lerp(jaw2ClosedRotation, jaw2OpenRotation, grabRatio);
                }
                else
                {
                    // Close fully if grabbing thin air
                    targetRot1 = jaw1ClosedRotation;
                    targetRot2 = jaw2ClosedRotation;
                }
            }

            jaw1.localRotation = Quaternion.Lerp(jaw1.localRotation, Quaternion.Euler(targetRot1), Time.deltaTime * jawAnimationSpeed);
            jaw2.localRotation = Quaternion.Lerp(jaw2.localRotation, Quaternion.Euler(targetRot2), Time.deltaTime * jawAnimationSpeed);
        }
    }

    public void Grab()
    {
        if (grabbedObject != null) return; // Already holding something

        // Clean up list in case an object was destroyed while inside the trigger
        objectsInZone.RemoveAll(r => r == null);
        
        Rigidbody closestRb = null;
        float closestDistance = float.MaxValue;

        // Find the closest valid Rigidbody currently inside our trigger collider
        foreach (var rb in objectsInZone)
        {
            if (!rb.isKinematic)
            {
                float dist = Vector3.Distance(transform.position, rb.position);
                if (dist < closestDistance)
                {
                    closestDistance = dist;
                    closestRb = rb;
                }
            }
        }

        if (closestRb != null)
        {
            grabbedObject = closestRb;

            // Calculate how wide the object is to stop the jaws from clipping through it
            Collider col = grabbedObject.GetComponentInChildren<Collider>();
            if (col != null)
            {
                // Take the smallest dimension of the bounding box
                Vector3 size = col.bounds.size;
                currentGrabWidth = Mathf.Min(size.x, size.y, size.z);
            }
            else
            {
                currentGrabWidth = 0f;
            }

            if (snapToCenter)
            {
                positionOffset = Vector3.zero;
                rotationOffset = Quaternion.identity;
            }
            else
            {
                // Store offsets so the object is held exactly where it was grabbed
                positionOffset = transform.InverseTransformPoint(grabbedObject.position);
                rotationOffset = Quaternion.Inverse(transform.rotation) * grabbedObject.rotation;
            }

            wasGravityEnabled = grabbedObject.useGravity;
            grabbedObject.useGravity = false; // Turn off gravity while holding to improve tracking
            grabbedObject.maxAngularVelocity = maxAngularVelocity; // Override Unity's default max angular velocity of 7

            // Ignore collisions between grabber and grabbed object
            grabbedColliders = grabbedObject.GetComponentsInChildren<Collider>();
            if (gripperColliders != null && grabbedColliders != null)
            {
                foreach (var gripperCol in gripperColliders)
                {
                    foreach (var grabbedCol in grabbedColliders)
                    {
                        if (gripperCol != null && grabbedCol != null)
                        {
                            Physics.IgnoreCollision(gripperCol, grabbedCol, true);
                        }
                    }
                }
            }
        }
    }

    public void Release()
    {
        if (grabbedObject == null) return;

        // Restore collisions
        if (gripperColliders != null && grabbedColliders != null)
        {
            foreach (var gripperCol in gripperColliders)
            {
                foreach (var grabbedCol in grabbedColliders)
                {
                    if (gripperCol != null && grabbedCol != null)
                    {
                        Physics.IgnoreCollision(gripperCol, grabbedCol, false);
                    }
                }
            }
        }

        grabbedObject.useGravity = wasGravityEnabled;
        grabbedObject = null;
        grabbedColliders = null;
    }

    void FixedUpdate()
    {
        if (grabbedObject != null)
        {
            // Calculate target position and rotation based on offsets
            Vector3 targetPosition = transform.TransformPoint(positionOffset);
            Quaternion targetRotation = transform.rotation * rotationOffset;

            // --- Position Tracking ---
            Vector3 positionDifference = targetPosition - grabbedObject.position;
            Vector3 targetVelocity = positionDifference * positionStiffness;
            
            // Clamp velocity to prevent physics explosions
            if (targetVelocity.magnitude > maxVelocity)
            {
                targetVelocity = targetVelocity.normalized * maxVelocity;
            }
            grabbedObject.linearVelocity = targetVelocity;

            // --- Rotation Tracking ---
            Quaternion rotationDifference = targetRotation * Quaternion.Inverse(grabbedObject.rotation);
            
            // Ensure the shortest rotation path is taken
            if (rotationDifference.w < 0f)
            {
                rotationDifference.x = -rotationDifference.x;
                rotationDifference.y = -rotationDifference.y;
                rotationDifference.z = -rotationDifference.z;
                rotationDifference.w = -rotationDifference.w;
            }

            // Avoid ToAngleAxis instability near 0 by using the quaternion's vector part directly.
            // This is mathematically stable and completely eliminates the "shaking when picked up" issue.
            Vector3 angleAxis = new Vector3(rotationDifference.x, rotationDifference.y, rotationDifference.z);
            float angleMagnitude = angleAxis.magnitude;

            if (angleMagnitude > 0.001f)
            {
                // Multiply by a factor to convert to an effective angular velocity
                Vector3 targetAngularVelocity = angleAxis * (rotationStiffness * 2f);
                
                if (targetAngularVelocity.magnitude > maxAngularVelocity)
                {
                    targetAngularVelocity = targetAngularVelocity.normalized * maxAngularVelocity;
                }
                grabbedObject.angularVelocity = targetAngularVelocity;
            }
            else
            {
                // Completely stop micro-jitters when rotation is perfectly aligned
                grabbedObject.angularVelocity = Vector3.zero;
            }
        }
    }

    // Removed manual OnDrawGizmosSelected since Unity draws colliders automatically
}
