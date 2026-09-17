using UnityEngine;
using UnityEngine.InputSystem;

public class MechanicalClaw : MonoBehaviour
{
    public Transform fieldA;
    public Transform fieldB;
    public Rigidbody targetCube; // Keep this for specific logic if needed

    [Header("Depth & Movement")]
    public float mouseDepth = 10f;
    public float scrollSensitivity = 1.2f;
    public float lerpSpeed = 20f;

    [Header("Claw Mechanics")]
    public float openWidth = 3.5f;
    public float closedWidth = 2.1f;
    public float pinchSpeed = 10f;
    public float rotationSpeed = 100f;

    [Header("Grab Settings")]
    public float grabReach = 1.2f; // Increase this to make grabbing the string easier

    private float currentWidth;
    private float currentYRotation = 0f;
    private FixedJoint grabJoint;
    private bool isGrabbing = false;
    private Rigidbody clawRb;

    void Start()
    {
        currentWidth = openWidth;
        clawRb = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
        clawRb.isKinematic = true;
        clawRb.interpolation = RigidbodyInterpolation.Interpolate;
        currentYRotation = transform.eulerAngles.y;
    }

    void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        // 1. Depth & Rotation
        float scrollDelta = Mouse.current.scroll.ReadValue().y;
        mouseDepth = Mathf.Clamp(mouseDepth + (scrollDelta * 0.12f), 2f, 50f);

        float rotationInput = 0f;
        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) rotationInput = -1f;
        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) rotationInput = 1f;
        currentYRotation += rotationInput * rotationSpeed * Time.deltaTime;

        // 2. Position & Rotation Application
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 targetWorldPos = Camera.main.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, mouseDepth));

        clawRb.MovePosition(Vector3.Lerp(transform.position, targetWorldPos, Time.deltaTime * lerpSpeed));
        clawRb.MoveRotation(Quaternion.Euler(0, 0, currentYRotation));

        // 3. Pinching Animation
        bool isClicking = Mouse.current.leftButton.isPressed;
        float targetWidth = isClicking ? closedWidth : openWidth;
        currentWidth = Mathf.Lerp(currentWidth, targetWidth, Time.deltaTime * pinchSpeed);

        fieldA.localPosition = new Vector3(-currentWidth / 2f, 0, 0);
        fieldB.localPosition = new Vector3(currentWidth / 2f, 0, 0);

        // 4. Universal Grab Logic
        if (isClicking && !isGrabbing)
        {
            // Look for any Rigidbody within the grabReach
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, grabReach);
            foreach (var hit in hitColliders)
            {
                Rigidbody rb = hit.attachedRigidbody;
                // Don't grab the claw itself
                if (rb != null && rb != clawRb)
                {
                    AttachGrab(rb);
                    break;
                }
            }
        }
        else if (!isClicking && isGrabbing)
        {
            DetachGrab();
        }
    }

    void AttachGrab(Rigidbody objectToGrab)
    {
        isGrabbing = true;
        grabJoint = gameObject.AddComponent<FixedJoint>();
        grabJoint.connectedBody = objectToGrab;
        grabJoint.enableCollision = false;

        // If it's the cube, stop it from falling. 
        // For the string, we usually want gravity to stay on so it hangs naturally.
        if (objectToGrab == targetCube) objectToGrab.useGravity = false;

        objectToGrab.linearVelocity = Vector3.zero;
        objectToGrab.angularVelocity = Vector3.zero;
    }

    void DetachGrab()
    {
        isGrabbing = false;
        if (grabJoint != null)
        {
            // If we were holding the cube, turn gravity back on
            // if (grabJoint.connectedBody == targetCube) targetCube.useGravity = true;
            Destroy(grabJoint);
        }
    }
}