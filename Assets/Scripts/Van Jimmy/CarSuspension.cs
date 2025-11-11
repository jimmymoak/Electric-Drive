using UnityEngine;

public class CarSuspension : MonoBehaviour
{

    [Header("Wheel Settings")]
    [SerializeField] private Transform[] rayPoints;
    private float[] springCompression;
    
    [Header("Raycast Layer")]
    public LayerMask raycastLayer;

    [Header("Suspension Settings")]
    [SerializeField] private float springStrength = 1000f;
    [SerializeField] private float springRestLength = 1f;
    [SerializeField] private float springMaxTravelLength = 0.5f;
    [SerializeField] private float springWheelRadius = 0.5f;
    [SerializeField] private float springDamperStiffness = 2000f;
    
    private Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Initialize raycastLengths array to prevent null reference errors
        if (rayPoints != null)
        {
            springCompression = new float[rayPoints.Length];
            // Initialize with rest length as default
            for (int i = 0; i < springCompression.Length; i++)
            {
                springCompression[i] = 1;
            }
        }
    }

    void Update()
    {
        for (int i = 0; i < rayPoints.Length; i++)
        {
            RaycastHit hit;
            if (Physics.Raycast(rayPoints[i].position, -rayPoints[i].up, out hit, springRestLength + springWheelRadius, raycastLayer))
            {   
                float currentSpringLength = (hit.distance - springWheelRadius);
                springCompression[i] = Mathf.Clamp((springRestLength - currentSpringLength) / springMaxTravelLength, 0, 1);
                Debug.DrawLine(rayPoints[i].position, rayPoints[i].position + -rayPoints[i].up * (springRestLength + springWheelRadius), Color.red);
            }
            else 
            {
                // If raycast doesn't hit anything, use max travel distance
                springCompression[i] = 0;
                Debug.DrawLine(rayPoints[i].position, rayPoints[i].position + -rayPoints[i].up * (springRestLength + springWheelRadius), Color.green);
            }
        }
    }

    void FixedUpdate()
    {
        // For i in raypoints take the respective length and apply the spring force
        for (int i = 0; i < rayPoints.Length; i++)
        {
            float springVelocity = Vector3.Dot(rb.GetPointVelocity(rayPoints[i].position), rayPoints[i].up);
            float dampingForce = springDamperStiffness * springVelocity;

            if (springCompression[i] > 0)
            {
                float netForce = (springStrength * springCompression[i]) - dampingForce;
                rb.AddForceAtPosition(rayPoints[i].up * netForce, rayPoints[i].position);
            }
        }
    }
}