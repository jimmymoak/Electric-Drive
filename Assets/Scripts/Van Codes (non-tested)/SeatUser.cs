using UnityEngine;

/// <summary>
    /// 
    /// 
    /// parents the player to that mount point without preserving world position, and notifies the parent <see cref="SeatManager"/>
    /// of the seat change so it can handle enabling vehicle controls if necessary.
    /// </summary>
    /// <param name="seat">The <see cref="Seat"/> to mount the player onto.</param>

public class SeatUser : MonoBehaviour
{
    [Header("Interact")]
    public KeyCode enterKey = KeyCode.E;
    public KeyCode exitKey = KeyCode.F;
    public float uDistance = 2.0f;
    public LayerMask seatLayer = ~0;
    public CharacterController controller;
    public Camera playerCamera; // optional, falls back to Camera.main if null
    Seat currentSeat;
    Transform originalParent;
    Vector3 cachedLocalPos;
    Quaternion cachedLocalRot;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        originalParent = transform.parent;
        if (playerCamera == null) playerCamera = Camera.main;
    }

    void Update()
    {
        if (!currentSeat)
        {
            // enter
            if (Input.GetKeyDown(enterKey))
                TryEnterSeatByRay();
        }
        else
        {
            // exit
            if (Input.GetKeyDown(exitKey))
                currentSeat.TryExit();
        }
    }
    
    void TryEnterSeatByRay()
    {
        var cam = playerCamera ? playerCamera : Camera.main;
        if (cam == null) return;

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        if (Physics.Raycast(ray, out RaycastHit hit, uDistance, seatLayer, QueryTriggerInteraction.Ignore))
        {
            Seat seat = hit.collider.GetComponentInParent<Seat>();
            if (seat != null) seat.TryEnter(this);
        }
    }

    // Mounts the player on the given seat.
    
    public void MountSeat(Seat seat)
    {
        currentSeat = seat;

        // stop char movement
        controller.enabled = false;

        // pin the player at mounPoint
    transform.SetPositionAndRotation(seat.mountPoint.position, seat.mountPoint.rotation);
    transform.SetParent(seat.mountPoint, worldPositionStays: false);

        var m = GetComponentInParent<SeatManager>();
        if (m) m.NotifySeatChanged();
    }

    // calls from seat
    public void UnmountSeat(Seat seat)
    {
        // eixt the player
        transform.SetParent(originalParent, worldPositionStays: true);
        if (seat.exitPoint)
            transform.SetPositionAndRotation(seat.exitPoint.position, seat.exitPoint.rotation);

        // reactivate char control
        controller.enabled = true;

        currentSeat = null;

        var m = GetComponentInParent<SeatManager>();
        if (m) m.NotifySeatChanged();
    }

    public bool IsSeated => currentSeat != null;
    public bool IsDriver => currentSeat != null && currentSeat.isDriver;

}