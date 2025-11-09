using UnityEngine;

public class VanPlatformMotion : MonoBehaviour
{
    private Vector3 lastPos;
    private Quaternion lastRot;

    //delta values
    public Vector3 DeltaPosition { get; private set; }
    public Quaternion DeltaRotation { get; private set; }
    

    void Awake()
    {
        lastPos = transform.position;
        lastRot = transform.rotation;
        DeltaPosition = Vector3.zero;
        DeltaRotation = Quaternion.identity;
    }

    void LateUpdate()
    {
        Vector3 curPos = transform.position;
        Quaternion curRot = transform.rotation;

        DeltaPosition = curPos - lastPos;
        DeltaRotation = curRot * Quaternion.Inverse(lastRot);

        DeltaRotation.ToAngleAxis(out float angleDeg, out Vector3 axis);
        if (float.IsNaN(axis.x)) axis = Vector3.zero;

        lastPos = curPos;
        lastRot = curRot;
    }
}