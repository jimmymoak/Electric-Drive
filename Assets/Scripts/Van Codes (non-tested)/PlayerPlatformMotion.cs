using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerPlatformMotion : MonoBehaviour
{
    [Header("Settings")]
    public bool requireGrounded = true;
    public bool applyPlatformYaw = true;
    public bool yawOnly = true;

    [Header("Runtime")]
    public VanPlatformMotion currentPlatform;
    public Transform groundCheck;             
    public float groundedDistance = 0.2f;

    private CharacterController controller;
    private Transform tr;
    private Camera playerCam;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        tr = transform;                                   
        playerCam = GetComponentInChildren<Camera>();
    }

    void Update()
    {
        if (currentPlatform == null) return;


        bool grounded = controller.isGrounded || !requireGrounded || IsTouchingGround();
        if (!grounded) return;


        Vector3 delta = currentPlatform.DeltaPosition;
        if (delta.sqrMagnitude > 0f)
            controller.Move(delta);


        if (applyPlatformYaw)
        {
            Quaternion q = currentPlatform.DeltaRotation; 

            if (yawOnly)
            {
                Vector3 e = q.eulerAngles;
                q = Quaternion.Euler(0f, e.y, 0f);
            }

            if (q != Quaternion.identity)
            {
                Vector3 pivot = currentPlatform.transform.position;
                Vector3 offset = tr.position - pivot;
                offset = q * offset;
                Vector3 rotatedPos = pivot + offset;

                Vector3 rotDelta = rotatedPos - tr.position;
                if (rotDelta.sqrMagnitude > 0f)
                    controller.Move(rotDelta);

                tr.rotation = q * tr.rotation;
            }
        }
    }

    private bool IsTouchingGround()
    {
        Transform origin = groundCheck != null ? groundCheck : tr; 
        // out değişkeni tipi gerekli
        return Physics.SphereCast(origin.position, 0.1f, Vector3.down, out RaycastHit _, groundedDistance);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (currentPlatform != null) return; // null control

        var plat = other.GetComponentInParent<VanPlatformMotion>();
        if (plat != null)
            currentPlatform = plat;
    }

    private void OnTriggerExit(Collider other)
    {
        var plat = other.GetComponentInParent<VanPlatformMotion>();
        if (plat != null && plat == currentPlatform)
            currentPlatform = null;
    }
}
