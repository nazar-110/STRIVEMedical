using UnityEngine;
using System.Collections.Generic;

/*
 * Dynamic Rope Generator (RopeGenerator)
 * ----------------------------------------
 * Procedurally generates a physical rope made of rigidbodies and CharacterJoints,
 * and visually connects them using a LineRenderer.
 * 
 * SETUP INSTRUCTIONS:
 * 1. Create an Empty GameObject and call it "Rope".
 * 2. Attach this script to it. It will automatically add a LineRenderer.
 * 3. Create an "Anchor" object (like a ceiling hook) with a Rigidbody (Is Kinematic = true).
 * 4. Drag the Anchor object into the "Anchor" slot on this script.
 * 5. Create a Material for your LineRenderer (e.g., a simple color) and assign it in the LineRenderer component settings.
 * 6. Press Play!
 */

[RequireComponent(typeof(LineRenderer))]
public class RopeGenerator : MonoBehaviour
{
    [Header("Rope Settings")]
    public float ropeLength = 5f;
    public int segmentCount = 50;
    public float ropeWidth = 0.1f;
    public float segmentMass = 0.2f;
    
    [Header("Damping Settings (Less Dramatic)")]
    public float linearDrag = 1f; // Slows down overall movement (air resistance)
    public float angularDrag = 5f; // Heavily slows down swinging and spinning
    
    [Header("Physics Settings")]
    public Rigidbody anchor; // The point the rope hangs from

    private LineRenderer lineRenderer;
    private List<GameObject> ropeSegments = new List<GameObject>();

    void Start()
    {
        // Force the object to the center of the scene to guarantee visibility
        //transform.position = new Vector3(0, 5f, 0);

        lineRenderer = GetComponent<LineRenderer>();
        GenerateRope();
    }

    void GenerateRope()
    {
        if (segmentCount < 2) return;

        float segmentLength = ropeLength / segmentCount;
        Rigidbody previousBody = anchor;

        for (int i = 0; i < segmentCount; i++)
        {
            // Create Segment
            GameObject segment = new GameObject($"RopeSegment_{i}");
            segment.transform.parent = transform;
            
            // Position it downwards
            segment.transform.position = transform.position + Vector3.down * (segmentLength * (i + 1));

            // Add Physics Components
            Rigidbody rb = segment.AddComponent<Rigidbody>();
            rb.mass = segmentMass;
            rb.linearDamping = linearDrag;
            rb.angularDamping = angularDrag;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous; // Prevents physics glitches and tunneling
            
            // Add Capsule Collider (better for ropes than spheres)
            CapsuleCollider col = segment.AddComponent<CapsuleCollider>();
            col.radius = ropeWidth / 2f;
            col.height = segmentLength;
            col.direction = 1; // Y-Axis

            // Connect using Character Joint only if there is a previous body
            if (previousBody != null)
            {
                CharacterJoint joint = segment.AddComponent<CharacterJoint>();
                joint.connectedBody = previousBody;
                
                // Limit twisting and swinging to simulate a rope rather than a loose chain
                SoftJointLimit twistLimit = new SoftJointLimit() { limit = 20f };
                SoftJointLimit swingLimit = new SoftJointLimit() { limit = 45f };

                joint.lowTwistLimit = new SoftJointLimit() { limit = -20f };
                joint.highTwistLimit = twistLimit;
                joint.swing1Limit = swingLimit;
                joint.swing2Limit = swingLimit;

                // Essential for preventing the chain from stretching infinitely under tension
                joint.enableProjection = true; 
            }

            // Store segment for visual updating
            ropeSegments.Add(segment);
            previousBody = rb;
        }

        // Setup LineRenderer Visuals
        if (lineRenderer.sharedMaterial == null)
        {
            // Fallback so it doesn't render completely invisible if no material is assigned
            lineRenderer.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.sharedMaterial.color = Color.white;
        }
        
        lineRenderer.positionCount = segmentCount;
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;
        lineRenderer.useWorldSpace = true;
    }

    void LateUpdate()
    {
        if (lineRenderer == null || ropeSegments.Count == 0) return;

        // Draw the line through all the generated physical segments
        for (int i = 0; i < ropeSegments.Count; i++)
        {
            lineRenderer.SetPosition(i, ropeSegments[i].transform.position);
        }
    }
}
