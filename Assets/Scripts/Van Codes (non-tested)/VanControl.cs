using UnityEngine;
using System.Collections.Generic;
using System.Text;

[RequireComponent(typeof(Rigidbody))]
public class Van : MonoBehaviour
{
    private Rigidbody rb;

    [Header("Wheel Visuals")]
    public Transform meshFL, meshFR, meshBL, meshBR;

    [Header("Motor & Torque")]
    [SerializeField] private float maxMotorTorque = 3000f;
    [SerializeField] private float maxBrakeTorque = 5000f;
    [SerializeField] private float maxSteerAngle  = 28f;
    [SerializeField] private float topSpeedKmh    = 120f;

    [Header("Assists")]
    [SerializeField] private float steerReturnSpeed = 5f;
    [Range(0f,1f)][SerializeField] private float tractionControl = 0.2f;

    [Header("Gear Settings")]
    [Tooltip("Gear toggle key")]
    [SerializeField] private KeyCode gearToggleKey = KeyCode.R;
    [Tooltip("Should the vehicle stop before allowing gear change?")]
    [SerializeField] private bool requireStopToShift = true;
    [Tooltip("Speed threshold (km/h) considered as 'stopped'")]
    [SerializeField] private float shiftSpeedThresholdKmh = 2f;

    [Header("Seat / Permissions")]
    [SerializeField] private SeatManager seatManager;
    [SerializeField] private bool isPlayerOnSeat = true;

    // Input state
    float throttleInputSigned; // -1..1 (Direction based on gear)
    float steerInput;          // -1..1
    float brakeInput;          // 0..1
    bool  brakeButton;         // panic/hand brake

    // NOTE: Made public to fix CS0052 (used by public field in Wheels)
    public enum WheelType { FL, FR, BL, BR }
    private enum Gear { Drive, Reverse }
    [SerializeField] private Gear currentGear = Gear.Drive;

    [System.Serializable]
    public class Wheels
    {
        public WheelType wheelType;
        public WheelCollider wheelCollider;
        public Transform wheelTransform; // optional
    }

    [Header("Wheel Setup")]
    public Wheels[] wheels;

    private Dictionary<WheelType, WheelCollider> wc;
    private Dictionary<WheelType, Transform>     wt;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.centerOfMass = new Vector3(0f, -0.25f, 0.1f);

        wc = new Dictionary<WheelType, WheelCollider>(4);
        wt = new Dictionary<WheelType, Transform>(4);

        if (wheels == null || wheels.Length == 0)
        {
            Debug.LogWarning($"{nameof(Van)}: 'wheels' array is empty. Vehicle won't move until colliders are assigned.");
            return;
        }

        foreach (var w in wheels)
        {
            if (w == null)
            {
                Debug.LogWarning($"{nameof(Van)}: A null entry found in 'wheels' array. Skipping it.");
                continue;
            }

            if (w.wheelCollider == null)
                Debug.LogWarning($"{nameof(Van)}: Wheel '{w.wheelType}' has no WheelCollider. Skipping it.");
            else
                wc[w.wheelType] = w.wheelCollider;

            if (w.wheelTransform != null)
                wt[w.wheelType] = w.wheelTransform;
            else
            {
                switch (w.wheelType)
                {
                    case WheelType.FL: wt[w.wheelType] = meshFL; break;
                    case WheelType.FR: wt[w.wheelType] = meshFR; break;
                    case WheelType.BL: wt[w.wheelType] = meshBL; break;
                    case WheelType.BR: wt[w.wheelType] = meshBR; break;
                }
            }
        }
    }

    void Update()
    {
        // --- Legacy Input axes (Project Settings > Input Manager):
        // Horizontal (A/D, Left/Right), Vertical (W/S, Up/Down)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Smooth steering
        steerInput = Mathf.Lerp(steerInput, Mathf.Clamp(h, -1f, 1f), Time.deltaTime * steerReturnSpeed);

        // Gas & brake separation:
        // - Forward (v>0): accelerator pedal (0..1)
        // - Backward (v<0): brake pedal (0..1) — independent of gear
        float accel = Mathf.Clamp01(v);
        brakeInput  = Mathf.Clamp01(-v);

        // Gear toggle with R key
        HandleGearToggle();

        // Signed throttle according to current gear
        throttleInputSigned = (currentGear == Gear.Reverse ? -accel : accel);

        // Panic/handbrake (Space)
        brakeButton = Input.GetKey(KeyCode.Space);

        UpdateAllWheelVisuals();

        #if DEBUG
        UpdateDebugInfo();
        #endif
    }

    void FixedUpdate()
    {
        if (seatManager && !seatManager.DriverOccupied)
        {
            ZeroWheels();
            return;
        }

        float speedKmh = GetSpeedKmh();

        // Speed limit (absolute torque), then apply gear direction
        float torqueReqAbs = (speedKmh < topSpeedKmh) ? maxMotorTorque * Mathf.Abs(throttleInputSigned) : 0f;
        float limitedAbs = ApplyTractionControl(torqueReqAbs);
        float finalTorque = Mathf.Sign(throttleInputSigned) * limitedAbs;

        // Steering (front)
        if (wc.TryGetValue(WheelType.FL, out var cFL) && cFL != null)
            cFL.steerAngle = maxSteerAngle * steerInput;
        if (wc.TryGetValue(WheelType.FR, out var cFR) && cFR != null)
            cFR.steerAngle = maxSteerAngle * steerInput;

        // Drive (rear)
        wc.TryGetValue(WheelType.BL, out var cBL);
        wc.TryGetValue(WheelType.BR, out var cBR);
        if (cBL != null) cBL.motorTorque = finalTorque;
        if (cBR != null) cBR.motorTorque = finalTorque;

        // Brakes
        if (brakeButton && isPlayerOnSeat)
            ApplyBrakesAll();                      // temporary full lock
        else
            ApplyProportionalBrakes(brakeInput);   // revert to proportional brake after releasing the button
    }

    void ZeroWheels()
    {
        if (wc.TryGetValue(WheelType.BL, out var bl) && bl != null)
        {
            bl.motorTorque = 0f;
            bl.brakeTorque = 0f;
        }
        if (wc.TryGetValue(WheelType.BR, out var br) && br != null)
        {
            br.motorTorque = 0f;
            br.brakeTorque = 0f;
        }
        if (wc.TryGetValue(WheelType.FL, out var fl) && fl != null)
            fl.brakeTorque = 0f;
        if (wc.TryGetValue(WheelType.FR, out var fr) && fr != null)
            fr.brakeTorque = 0f;
    }

    private void HandleGearToggle()
    {
        if (!Input.GetKeyDown(gearToggleKey))
            return;

        // Optionally enforce stop safety
        if (requireStopToShift)
        {
            float speedKmh = GetSpeedKmh();
            if (speedKmh > shiftSpeedThresholdKmh)
            {
                Debug.Log($"[{nameof(Van)}] Too fast to shift. Speed={speedKmh:F1} km/h > {shiftSpeedThresholdKmh}.");
                return;
            }
        }

        // Switch gear
        currentGear = (currentGear == Gear.Drive) ? Gear.Reverse : Gear.Drive;
        Debug.Log($"[{nameof(Van)}] Gear: {currentGear}");
    }

    private float ApplyTractionControl(float torqueAbs)
    {
        float slipSum = 0f;
        int   groundedCount = 0;

        if (wc.TryGetValue(WheelType.BL, out var bl) && bl != null && bl.GetGroundHit(out WheelHit hitL) && hitL.collider != null)
        {
            slipSum += Mathf.Abs(hitL.forwardSlip);
            groundedCount++;
        }
        if (wc.TryGetValue(WheelType.BR, out var br) && br != null && br.GetGroundHit(out WheelHit hitR) && hitR.collider != null)
        {
            slipSum += Mathf.Abs(hitR.forwardSlip);
            groundedCount++;
        }

        if (groundedCount > 0)
        {
            float slip = slipSum / groundedCount;
            if (slip >= 0.2f)
            {
                float factor = Mathf.Lerp(1f, 1f - tractionControl, Mathf.InverseLerp(0.3f, 1f, slip));
                torqueAbs *= factor;
            }
        }

        return torqueAbs;
    }

    // Panic/handbrake: applies maximum brake torque immediately (temporary).
    private void ApplyBrakesAll()
    {
        if (wheels == null) return;
        foreach (var w in wheels)
        {
            if (w?.wheelCollider != null)
                w.wheelCollider.brakeTorque = maxBrakeTorque;
        }
    }

    // Proportional brake: used when panic brake is released or during normal driving.
    private void ApplyProportionalBrakes(float amount01)
    {
        if (wheels == null) return;
        float t = maxBrakeTorque * Mathf.Clamp01(amount01);

        foreach (var w in wheels)
        {
            if (w?.wheelCollider != null)
                w.wheelCollider.brakeTorque = t;
        }
    }

    private void UpdateAllWheelVisuals()
    {
        if (wc == null || wt == null) return;

        foreach (var kv in wc)
        {
            var type = kv.Key;
            var col  = kv.Value;
            if (col == null) continue;
            if (!wt.TryGetValue(type, out var vis) || vis == null) continue;

            col.GetWorldPose(out Vector3 pos, out Quaternion rot);
            vis.SetPositionAndRotation(pos, rot);
        }
    }

    private float GetSpeedKmh()
    {
        #if UNITY_6000_0_OR_NEWER
        // Unity 6+ has linearVelocity
        return rb.linearVelocity.magnitude * 3.6f;
        #else
        return rb.velocity.magnitude * 3.6f;
        #endif
    }

#if DEBUG
    // Debug Throttle: throttled debug update (every 0.2s) similar to PlayerMovement
    private float nextDebugTime;

    private void UpdateDebugInfo()
    {
        if (Time.unscaledTime < nextDebugTime) return;
        nextDebugTime = Time.unscaledTime + 0.2f;

        var sb = new StringBuilder(256);
        sb.Append("Gear: ").Append(currentGear)
          .Append("\nSpeed (km/h): ").Append(GetSpeedKmh().ToString("F1"))
          .Append("\nThrottle (signed): ").Append(throttleInputSigned.ToString("F2"))
          .Append("\nSteer: ").Append(steerInput.ToString("F2"))
          .Append("\nBrake: ").Append(brakeInput.ToString("F2"))
          .Append("\nHandbrake: ").Append(brakeButton)
          .Append("\nWheels configured: ").Append(wheels == null ? 0 : wheels.Length)
          .Append("\nDriverOccupied: ").Append(seatManager ? seatManager.DriverOccupied : (bool?)null);

        DebugManager.Instance?.SetDebugText(sb.ToString());
    }
#else
    private void UpdateDebugInfo() { }
#endif
}
