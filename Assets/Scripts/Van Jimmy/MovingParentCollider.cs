using UnityEngine;

public class MovingParentCollider : MonoBehaviour
{
    public MovingParent movingParent;

    void OnTriggerEnter(Collider other)
    {
        Rigidbody otherRb = other.GetComponent<Rigidbody>();
        movingParent.AddOccupant(otherRb);
    }

    void OnTriggerExit(Collider other)
    {
        Rigidbody otherRb = other.GetComponent<Rigidbody>();
        movingParent.RemoveOccupant(otherRb);
    }
}