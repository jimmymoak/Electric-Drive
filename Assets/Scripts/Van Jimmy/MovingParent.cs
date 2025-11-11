using UnityEngine;
using System.Collections.Generic;

// This script is a collider that moves all the rigidbodies inside it.
public class MovingParent : MonoBehaviour
{
    public Rigidbody rb;
    private HashSet<Rigidbody> occupants = new HashSet<Rigidbody>();

    // Track last platform pose at its COM (world space)
    private Vector3 lastComPos;
    private Quaternion lastRot;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        lastComPos = rb != null ? rb.worldCenterOfMass : transform.position;
        lastRot    = rb != null ? rb.rotation          : transform.rotation;
    }

    void FixedUpdate()
    {
        if (rb == null) return;

        // Current platform pose (from previous physics step’s result)
        Vector3 comNow   = rb.worldCenterOfMass;
        Quaternion rotNow = rb.rotation;

        Quaternion deltaRot = rotNow * Quaternion.Inverse(lastRot);
        Vector3 comDelta    = comNow - lastComPos;

        bool hasMoved = comDelta.sqrMagnitude > 0f || Quaternion.Angle(deltaRot, Quaternion.identity) > 0.0001f;
        if (hasMoved)
        {
            foreach (var body in occupants)
            {
                if (!body) continue;

                // Re-anchor around last COM, then apply rotation+translation to get target
                Vector3 rLast     = body.position - lastComPos;
                Vector3 rRotated  = deltaRot * rLast;
                Vector3 targetPos = comNow + rRotated;

                body.transform.Translate(targetPos - body.position, Space.World);

                // Optional: also rotate rider with platform
                // body.MoveRotation(deltaRot * body.rotation);
            }
        }

        // Update trackers for next step
        lastComPos = comNow;
        lastRot    = rotNow;
    }

    // Be careful with multiple colliders under one parent, as being in two 
    // different colliders at once can add you twice, but then remove you from both.
    public void AddOccupant(Rigidbody otherRb)
    {
        if (otherRb != null && otherRb != this.rb)
        {
            DebugManager.Instance.Log($"Adding to moving parent: {otherRb.name}");
            occupants.Add(otherRb);
        }
    }
    
    public void RemoveOccupant(Rigidbody otherRb)
    {
        if (otherRb != null && otherRb != this.rb)
        {
            DebugManager.Instance.Log($"Removing from moving parent: {otherRb.name}");
            occupants.Remove(otherRb);
        }
    }
}