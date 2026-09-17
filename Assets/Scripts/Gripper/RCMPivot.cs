using UnityEngine;
using UnityEngine.InputSystem; // Required for the new Input System

public class RCMPivot : MonoBehaviour
{
    [Header("References")]
    public Transform Pivot;
    public Transform toolShaft;

    [Header("Speeds")]
    public float rotationSpeed = 100f;
    public float insertionSpeed = 0.5f;

    [Header("Current Input Amounts")]
    [Range(-1f, 1f)] public float insertionAmount = 0f;
    public Vector2 rotationInput = Vector2.zero;

    void Update()
    {
        // 1. GET INPUT
        HandleInput();

        // 2. APPLY MOVEMENT
        HandleInsertion();
        HandleRotation();
    }

    void HandleInput()
    {
        // Reset values each frame
        rotationInput = Vector2.zero;
        insertionAmount = 0f;

        // Safety check to prevent crashes if no keyboard is connected
        if (Keyboard.current != null)
        {
            // Yaw (Left/Right)
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) rotationInput.x += 1f;
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) rotationInput.x -= 1f;

            // Pitch (Up/Down)
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) rotationInput.y += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) rotationInput.y -= 1f;

            // Insertion / Retraction (E and Q)
            if (Keyboard.current.eKey.isPressed) insertionAmount = 1f;
            else if (Keyboard.current.qKey.isPressed) insertionAmount = -1f;
        }
    }

    void HandleRotation()
    {
        float yaw = rotationInput.x * rotationSpeed * Time.deltaTime;
        float pitch = rotationInput.y * rotationSpeed * Time.deltaTime;

        // Apply rotation to the Pivot
        Pivot.Rotate(Vector3.up, yaw, Space.World);
        Pivot.Rotate(Vector3.right, pitch, Space.Self);
    }

    void HandleInsertion()
    {
        // Slide the tool shaft forward/backward
        float insertion = insertionAmount * insertionSpeed * Time.deltaTime;
        toolShaft.localPosition += Vector3.forward * insertion;
    }
}