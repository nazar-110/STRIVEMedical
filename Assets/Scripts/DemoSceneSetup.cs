using UnityEngine;

/// <summary>
/// Spawns the demo training objects at runtime in SampleScene:
///   - An open box on the table (5 thin walls, no lid)
///   - A needle (thin cylinder, grabbable)
///   - A flesh target slab (red kinematic block on the table)
///
/// SETUP:
///   1. Create an empty GameObject in SampleScene, name it "DemoSetup"
///   2. Add this component.
///   3. Press Play — objects appear on the table.
///   4. Tweak the public position fields in the Inspector if needed.
/// </summary>
public class DemoSceneSetup : MonoBehaviour
{
    [Header("Box Settings")]
    [Tooltip("Center position of the box (should be on the table surface)")]
    public Vector3 boxPosition = new Vector3(-0.25f, 2.52f, -0.583f);
    public Vector3 boxSize = new Vector3(0.25f, 0.12f, 0.25f);
    public float wallThickness = 0.015f;

    [Header("Needle Settings")]
    [Tooltip("Starting position of the needle on the table")]
    public Vector3 needlePosition = new Vector3(0.6f, 2.6f, -0.9f);
    public Vector3 needleScale = new Vector3(0.015f, 0.12f, 0.015f);

    [Header("Flesh Target Settings")]
    [Tooltip("Position of the red flesh block on the table")]
    public Vector3 fleshPosition = new Vector3(0.3f, 2.54f, -0.9f);
    public Vector3 fleshScale = new Vector3(0.22f, 0.06f, 0.15f);

    // Public references so DemoObjectives can find them
    [HideInInspector] public GameObject box;
    [HideInInspector] public GameObject boxInteriorTrigger;
    [HideInInspector] public GameObject needle;
    [HideInInspector] public GameObject fleshTarget;
    [HideInInspector] public GameObject tableSurfaceTrigger;

    void Awake()
    {
        CreateBox();
        CreateNeedle();
        CreateFleshTarget();
        CreateTableSurfaceTrigger();
    }

    // ── Box (5 walls, no lid) ─────────────────────────────────────────
    void CreateBox()
    {
        box = new GameObject("DemoBox");
        box.transform.position = boxPosition;

        Color boxColor = new Color(0.55f, 0.35f, 0.15f); // brown wood-ish

        // Floor
        CreateWall(box.transform, "BoxFloor",
            Vector3.zero,
            new Vector3(boxSize.x, wallThickness, boxSize.z),
            boxColor);

        // Front wall
        CreateWall(box.transform, "BoxFront",
            new Vector3(0, boxSize.y / 2f, -boxSize.z / 2f + wallThickness / 2f),
            new Vector3(boxSize.x, boxSize.y, wallThickness),
            boxColor);

        // Back wall
        CreateWall(box.transform, "BoxBack",
            new Vector3(0, boxSize.y / 2f, boxSize.z / 2f - wallThickness / 2f),
            new Vector3(boxSize.x, boxSize.y, wallThickness),
            boxColor);

        // Left wall
        CreateWall(box.transform, "BoxLeft",
            new Vector3(-boxSize.x / 2f + wallThickness / 2f, boxSize.y / 2f, 0),
            new Vector3(wallThickness, boxSize.y, boxSize.z),
            boxColor);

        // Right wall
        CreateWall(box.transform, "BoxRight",
            new Vector3(boxSize.x / 2f - wallThickness / 2f, boxSize.y / 2f, 0),
            new Vector3(wallThickness, boxSize.y, boxSize.z),
            boxColor);

        // Interior trigger zone (slightly smaller than the box interior)
        boxInteriorTrigger = new GameObject("BoxInteriorTrigger");
        boxInteriorTrigger.transform.SetParent(box.transform);
        boxInteriorTrigger.transform.localPosition = new Vector3(0, boxSize.y / 2f, 0);
        var triggerCollider = boxInteriorTrigger.AddComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        triggerCollider.size = new Vector3(
            boxSize.x - wallThickness * 4f,
            boxSize.y * 0.8f,
            boxSize.z - wallThickness * 4f);
    }

    void CreateWall(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.localPosition = localPos;
        wall.transform.localScale = scale;
        wall.transform.localRotation = Quaternion.identity;

        var renderer = wall.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = color;
        }
    }

    // ── Needle ────────────────────────────────────────────────────────
    void CreateNeedle()
    {
        needle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        needle.name = "DemoNeedle";
        needle.transform.position = needlePosition;
        needle.transform.localScale = needleScale;

        // Metallic gray appearance
        var renderer = needle.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = new Color(0.75f, 0.75f, 0.78f);
            renderer.material.SetFloat("_Smoothness", 0.85f);
            renderer.material.SetFloat("_Metallic", 0.9f);
        }

        // Physics — grabbable
        var rb = needle.AddComponent<Rigidbody>();
        rb.mass = 0.1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    // ── Flesh Target ──────────────────────────────────────────────────
    void CreateFleshTarget()
    {
        fleshTarget = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        fleshTarget.name = "FleshTarget";
        fleshTarget.transform.position = fleshPosition;
        fleshTarget.transform.localScale = fleshScale;

        // Dark red fleshy look
        var renderer = fleshTarget.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = new Color(0.6f, 0.15f, 0.12f);
        }

        // Remove the default SphereCollider (which doesn't scale non-uniformly)
        Destroy(fleshTarget.GetComponent<Collider>());
        
        // Add a MeshCollider to perfectly match the oval visual shape
        var meshCol = fleshTarget.AddComponent<MeshCollider>();
        meshCol.convex = true;
        meshCol.isTrigger = true; // Trigger so the needle can pierce it visually

        // Kinematic — doesn't move when poked
        var rb = fleshTarget.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        // The existing BoxCollider (from CreatePrimitive) handles collision.
        // FleshCollisionListener is added at runtime by DemoObjectives
        // to detect when the needle physically touches the surface.
    }

    // ── Table Surface Trigger ─────────────────────────────────────────
    // A flat invisible trigger zone on the table surface (not inside the box)
    // Used to detect when the cube is placed back on the table for Step 2.
    void CreateTableSurfaceTrigger()
    {
        tableSurfaceTrigger = new GameObject("TableSurfaceTrigger");
        tableSurfaceTrigger.transform.position = new Vector3(0.3f, 2.55f, -0.583f);
        var col = tableSurfaceTrigger.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.5f, 0.15f, 1.5f); // wide flat zone on the table
    }
}
