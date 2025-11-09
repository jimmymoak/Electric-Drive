using UnityEngine;
using System.Collections.Generic;

public class SeatManager : MonoBehaviour
{
    public Seat[] seats;
    public MonoBehaviour[] enableWhenDriverSeated;

    public bool DriverOccupied
    {
        get
        {
            foreach (var s in seats)
                if (s && s.isDriver && s.IsOccupied) return true;
            return false;
        }
    }

    public void NotifySeatChanged()
    {
        bool driverOn = DriverOccupied;
        foreach (var mb in enableWhenDriverSeated)
        {
            if (!mb) continue;
            mb.enabled = driverOn;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // autofill (optional)
        if (seats == null || seats.Length == 0)
            seats = GetComponentsInChildren<Seat>(true);
    }
#endif
}