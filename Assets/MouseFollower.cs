using UnityEngine;
using UnityEngine.InputSystem;

public class MouseFollower : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("Initial distance from the camera")]
    public float depth = 10f; 
    public float scrollSensitivity = 0.05f;
    public float minDepth = 2f;
    public float maxDepth = 50f;
    public float lerpSpeed = 20f;

    void Update()
    {
        if (Mouse.current != null && Camera.main != null)
        {
            // 1. Adjust depth with scroll wheel
            float scrollDelta = Mouse.current.scroll.ReadValue().y;
            if (scrollDelta != 0)
            {
                depth = Mathf.Clamp(depth + (scrollDelta * scrollSensitivity), minDepth, maxDepth);
            }

            // 2. Calculate world position
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector3 screenPos = new Vector3(mousePos.x, mousePos.y, depth);
            Vector3 targetWorldPos = Camera.main.ScreenToWorldPoint(screenPos);

            // 3. Move the object smoothly
            transform.position = Vector3.Lerp(transform.position, targetWorldPos, Time.deltaTime * lerpSpeed);
        }
    }
}
