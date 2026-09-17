using UnityEngine;

public class GripMotor : MonoBehaviour
{
    public HingeJoint top;
    public HingeJoint bottom;

    public HingeJoint rotator;

    public float grip; // 0=open, 1=closed

    public float rotation;
    public float motorSpeed = 100f;

    void Update()
    {
        var mTop = top.motor;
        var mBottom = bottom.motor;
        var mRot = rotator.motor;

        // Grip 0 = open, 1 = close
        float direction = grip * motorSpeed;
        float rot = rotation * motorSpeed;

        // Top goes negative, bottom goes positive
        mTop.targetVelocity = -direction;
        mBottom.targetVelocity = direction;
        mRot.targetVelocity = rot;
        

        top.motor = mTop;
        bottom.motor = mBottom;
        rotator.motor = mRot;
    }
}

