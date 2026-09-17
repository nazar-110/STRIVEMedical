using UnityEngine;

public class StringCreator : MonoBehaviour
{
    public GameObject linkPrefab;
    public Rigidbody anchor; // The "Ceiling" (A kinematic Rigidbody)
    public int segmentCount = 10;

    void Start()
    {
        Rigidbody lastBody = anchor;

        for (int i = 0; i < segmentCount; i++)
        {
            GameObject link = Instantiate(linkPrefab, transform);
            link.transform.position = transform.position + (Vector3.down * i * 0.4f);

            HingeJoint joint = link.GetComponent<HingeJoint>();
            joint.connectedBody = lastBody;

            lastBody = link.GetComponent<Rigidbody>();
        }
    }
}