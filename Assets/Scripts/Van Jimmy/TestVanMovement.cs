using UnityEngine;

public class TestVanMovement : MonoBehaviour
{
    public float acceleration = 1f;
    public float maxSpeed = 5f;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Update is called once per frame
    void Update()
    {
        // Ping pong the van's position between 0 and 10 with velocity
        float zVelocity = Mathf.PingPong(Time.time * acceleration, maxSpeed) - maxSpeed / 2f;
        Vector3 velocity = new Vector3(rb.linearVelocity.x, rb.linearVelocity.y, zVelocity);
        rb.linearVelocity = velocity;
    }
}
