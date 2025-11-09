using UnityEngine;

public class Seat : MonoBehaviour
{
    [Header("Seat Setup")]
    public bool isDriver = false;
    public Transform mountPoint;
    public Transform exitPoint;

    [Header("readonly")]
    public SeatUser cUser; // current user mounted on this seat
    public bool IsOccupied => cUser != null;

    // Attempt to enter seat with a user
    public bool TryEnter(SeatUser user)
    {
        if (IsOccupied || user == null) return false;

        cUser = user;
        user.MountSeat(this);
        return true;
    }

    // Forcefully remove current user
    public void ForceExit()
    {
        if (!cUser) return;

        var u = cUser;
        cUser = null;
        u.UnmountSeat(this);
    }

    // Request exit from current user
    public bool TryExit()
    {
        if (!cUser) return false;

        var u = cUser;
        cUser = null;
        u.UnmountSeat(this);
        return true;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (mountPoint)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(mountPoint.position, 0.1f);
        }
        if (exitPoint)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(exitPoint.position, 0.1f);
        }
    }
#endif
}