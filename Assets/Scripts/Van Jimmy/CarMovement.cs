using UnityEngine;

public class CarMovement : MonoBehaviour
{
    [Header("Car Settings")]
    public float motorForce = 1500f;
    public float brakeForce = 3000f;
    public float maxSteerAngle = 30f;
    
    [Header("Drift Settings")]
    public float driftFactor = 0.95f;
    public float downforce = 100f;
    
    [Header("Physics")]
    public Vector3 centerOfMass;
    public float forceApplicationHeight = -0.1f; // Slightly below center of mass
    
    [Header("Wheels")]
    public Transform[] frontWheels;
    public Transform[] rearWheels;
    public ConfigurableJoint[] wheelJoints;
    
    private Rigidbody rb;
    private float horizontalInput;
    private float verticalInput;
    private float steerAngle;
    private bool isBraking;
    public bool isMotorActive = true;
    public bool isBrakeActive = true;
    public bool isSteeringActive = true;
    public bool isDriftActive = true;
    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Set proper rigidbody settings for car stability
        rb.mass = 1000f; // Heavier cars are more stable
        rb.linearDamping = 0.3f; // Some drag to prevent excessive sliding
        rb.angularDamping = 3f; // Higher angular drag to prevent flipping
        
        // Set center of mass for better car physics (less extreme values)
        rb.centerOfMass = centerOfMass;
    }

    void Update()
    {
        GetInput();
        if (isSteeringActive)
        {
            HandleSteering();
        }
        DebugInfo();
    }

    void FixedUpdate()
    {
        if (isMotorActive)
        {
            HandleMotor();
        }
        if (isBrakeActive)
        {
            HandleBrake();
        }
        if (isDriftActive)
        {
            HandleDrift();
        }
        ApplyDownforce();
    }

    private void GetInput()
    {
        // Get input from your input manager
        horizontalInput = 0f;
        verticalInput = 0f;
        
        /*
        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Forward))
            verticalInput = 1f;
        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Backward))
            verticalInput = -1f;
        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Left))
            horizontalInput = -1f;
        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Right))
            horizontalInput = 1f;
        isBraking = InputManager.Instance.IsActionDown(InputManager.ActionType.Crouch); // Use crouch as handbrake
        */
    }

    private void HandleMotor()
    {
        // Apply motor force below center of mass for realistic physics
        Vector3 forcePosition = transform.position + transform.up * forceApplicationHeight;
        Vector3 forwardForce = transform.forward * verticalInput * motorForce;
        
        rb.AddForceAtPosition(forwardForce, forcePosition);
    }

    private void HandleSteering()
    {
        // Calculate steering angle based on speed (less steering at high speed)
        float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / 20f);
        float adjustedMaxSteer = maxSteerAngle * (1f - speedFactor * 0.5f);
        
        steerAngle = adjustedMaxSteer * horizontalInput;
        
        // Apply steering to front wheels
        foreach (Transform wheel in frontWheels)
        {
            wheel.localRotation = Quaternion.Euler(0, steerAngle, 0);
        }
    }

    private void HandleBrake()
    {
        if (isBraking)
        {
            // Apply brake force
            Vector3 brakeForceVector = -rb.linearVelocity.normalized * brakeForce;
            rb.AddForce(brakeForceVector);
        }
    }

    private void ApplyDownforce()
    {
        // Apply downforce to keep car grounded at high speeds
        float speed = rb.linearVelocity.magnitude;
        float downforceAmount = downforce * speed * speed;
        
        // Add minimum downforce for stability even when stationary
        float minDownforce = downforce * 0.1f; // 10% of max downforce as baseline
        downforceAmount = Mathf.Max(downforceAmount, minDownforce);
        
        rb.AddForce(-transform.up * downforceAmount);
    }

    private void HandleDrift()
    {
        // Get velocity components
        Vector3 forwardVelocity = Vector3.Project(rb.linearVelocity, transform.forward);
        Vector3 sidewaysVelocity = Vector3.Project(rb.linearVelocity, transform.right);
        
        // Apply drift by reducing sideways velocity
        Vector3 driftForce = -sidewaysVelocity * driftFactor * rb.mass;
        rb.AddForce(driftForce);
        
        // Add some angular drag for stability
        rb.angularVelocity *= 0.98f;
    }

    // For turning, apply torque around the Y-axis
    private void HandleTurning()
    {
        if (Mathf.Abs(horizontalInput) > 0.1f && Mathf.Abs(verticalInput) > 0.1f)
        {
            float torque = horizontalInput * motorForce * 0.1f;
            
            // Apply torque based on forward velocity
            float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            if (forwardSpeed > 0.5f)
            {
                rb.AddTorque(0, torque, 0);
            }
        }
    }

    private void DebugInfo()
    {
        DebugManager.Instance.SetDebugText($"rb.velocity: {rb.linearVelocity}" + "\n" +
                                           $"rb.angularVelocity: {rb.angularVelocity}" + "\n" +
                                           $"rb.centerOfMass: {rb.centerOfMass}" + "\n" +
                                           $"rb.mass: {rb.mass}" + "\n" +
                                           $"rb.drag: {rb.linearDamping}" + "\n" +
                                           $"rb.angularDrag: {rb.angularDamping}");
    }
}