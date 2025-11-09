using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// This class is based off the movement of the game Rust, it uses a state machine to determine the tpye of movement.
// It calculates where an invsibile physics body is and then lerps the visual player body to that position.
// It does not do player rotation or fps looking around.
public class PlayerMovement : MonoBehaviour
{
    // Eventually move out of this class
    private bool isAdmin;
    private bool adminNoClip;

    // Movement Configuration
    private float drag = 0;
    private float maxAngleWalking = 50f;

    private float maxAngleClimbing = 60f;

    private float maxAngleSliding = 90f;
    private float maxVelocity = 50f;

    // Movement Flags
    private bool wasFlying;
    private bool isFlying;
    private bool grounded;
    private bool climbing;
    private bool sliding;

    private bool wasGrounded;
    private bool wasClimbing;
    private bool wasSliding;
    private bool wasJumping;
    private bool wasFalling;    
    private bool jumping;
    private bool falling;

    // Components
    private CapsuleCollider capsule;
    public Rigidbody rb;
    public Transform playerVisualBody;
    // Movement Variables
    private Vector3 groundNormalNew;
    private Vector3 groundNormal;
    private float groundAngleNew;
    private float groundAngle;
    private float groundTime;
    private float landTime;
    private Vector3 previousPosition;
    private Vector3 previousVelocity;
    private float jumpTime;

    [Header("Private Movement Variables")]
    [SerializeField] private float noClipSpeed = 10f;
    [SerializeField] private float noClipCrouchSpeed = 2f;
    [SerializeField] private float noClipSprintSpeed = 50f;
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private PhysicsMaterial highFrictionMaterial;
    [SerializeField] private PhysicsMaterial zeroFrictionMaterial;

    [Header("Capsule Collider Variables")]
    [SerializeField] private float capsuleHeight;
    [SerializeField] private float capsuleHeightCrouched;
    [SerializeField] private float capsuleCenter;
    [SerializeField] private float capsuleCenterCrouched;
    [SerializeField] private float gravityTestRadius = 0.2f;

    [Header("Public Movement Variables")]
    public Vector3 TargetMovement;
    public float Running;
    public float Crouching;
    public bool hasMovingParent;
    
    void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();
        rb = GetComponent<Rigidbody>();
        
        // Initialize capsule dimensions if not set
        if (capsuleHeight == 0)
            capsuleHeight = capsule.height;
        if (capsuleHeightCrouched == 0)
            capsuleHeightCrouched = capsule.height * 0.5f;
        if (capsuleCenter == 0)
            capsuleCenter = capsule.center.y;
        if (capsuleCenterCrouched == 0)
            capsuleCenterCrouched = capsule.center.y * 0.5f;
    }

    void Start()
    {
        previousPosition = transform.localPosition;
    }

    void FixedUpdate()
    {
        PhysicsUpdate();
    }
    public void PhysicsUpdate()
    { 
        DetermineInitialFlags();

        // Apply Movement Based off Flags
        this.UpdateVelocity();
        this.UpdateGravity();

        // Finish Player State
        DetermineFinalFlags();
    }

    void Update()
    {
        // Determine desired movement every frame
        FrameUpdate();
        
        // Update debug information
        UpdateDebugInfo();
    }

    public void FrameUpdate()
    {
        // First determine movement type and then apply movement and lerp the visual player body
        DetermineMovementType();

        // Reset rotation to prevent unwanted rotation, since this is just physics 
        transform.rotation = Quaternion.identity;
        
        // Set capsule as trigger when flying
        capsule.isTrigger = isFlying;
        
        // Check if player can move (you can add your own movement restrictions here)
        bool canPlayerMove = true; // Replace with your movement restriction logic
        if (!canPlayerMove)
        {
            Vector3 playerLocalPosition = transform.localPosition;
            transform.localPosition = playerLocalPosition;
            previousPosition = playerLocalPosition;
            return;
        }
        
        // Check if player is mounted (you can add your own mounted logic here)
        bool isPlayerMounted = false; // Replace with your mounted logic
        if (isPlayerMounted)
        {
            Vector3 playerLocalPosition = transform.localPosition;
            transform.localPosition = playerLocalPosition;
            previousPosition = playerLocalPosition;
            return;
        }
        
        // If On a Moving Platform, skip visual interpolation
        if (hasMovingParent)
        {
            return;
        }

        // Calculate interpolation factor between physics frames
        float interpolationFactor = (UnityEngine.Time.time - UnityEngine.Time.fixedTime) / UnityEngine.Time.fixedDeltaTime;
        
        // Interpolate between previous and current position
        Vector3 interpolatedPosition = Vector3.Lerp(previousPosition, transform.localPosition, interpolationFactor);
        
        // Smooth Y-axis movement specifically
        interpolatedPosition.y = Mathf.Lerp(transform.localPosition.y, interpolatedPosition.y, UnityEngine.Time.smoothDeltaTime * 15f);
        
        playerVisualBody.localPosition = interpolatedPosition;
    }

    // Ideal execution order: ClientInput -> FixedUpdate -> FrameUpdate
    public void DetermineMovementType()
    {
        wasFlying = isFlying;

        if (isAdmin == true)
        {
            isFlying = adminNoClip;
        }

        if (isFlying == true)
        {
            NoClipMovement();
        }
        else
        {
            WalkingMovement();
        }
    }


    private void NoClipMovement()
    {
        TargetMovement = Vector3.zero;
        float speed = noClipSpeed;

        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Forward))
        {
            TargetMovement += transform.forward;
        }

        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Backward))
        {
            TargetMovement -= transform.forward;
        }

        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Left))
        {
            TargetMovement -= transform.right;
        }

        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Right))  
        {
            TargetMovement += transform.right;
        }

        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Jump))
        {
            TargetMovement += transform.up;
        }

        // Determine Speed
        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Run))
        {
            // If target movement purely in the negative y direction, set it to normal speed
            if (TargetMovement.x == 0 && TargetMovement.z == 0 && TargetMovement.y <= 0)
            {
                TargetMovement -= transform.up;
                speed = noClipSpeed;
                TargetMovement = TargetMovement.normalized * speed;
                return;
            }

            speed = noClipSprintSpeed;
        }

        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Crouch))
        {
            speed = noClipCrouchSpeed;
        }

        // Set Movement
        TargetMovement = TargetMovement.normalized * speed;
    }

    private void WalkingMovement()
    {
        TargetMovement = Vector3.zero;
        
        // Get movement direction from transform
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        
        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Forward))
        {
            TargetMovement += forward;
        }
        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Backward))
        {
            TargetMovement -= forward;
        }
        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Left))
        {
            TargetMovement -= right;
        }
        if (InputManager.Instance.IsActionDown(InputManager.ActionType.Right))
        {
            TargetMovement += right;
        }

        if (this.jumping || (this.falling && UnityEngine.Time.time - this.groundTime > 0.3f))
        {
            grounded = false;
        }
        else
        {
            grounded = true;
        }

        // Only allow sprinting in the forward direction
        bool isOnlyMovingFoward = InputManager.Instance.IsActionDown(InputManager.ActionType.Forward)
                                  && (!InputManager.Instance.IsActionDown(InputManager.ActionType.Left)
                                      && !InputManager.Instance.IsActionDown(InputManager.ActionType.Backward)
                                      && !InputManager.Instance.IsActionDown(InputManager.ActionType.Right));

        bool wantsRun = isOnlyMovingFoward && InputManager.Instance.IsActionDown(InputManager.ActionType.Run);
        bool wantsCrouch = InputManager.Instance.IsActionDown(InputManager.ActionType.Crouch);
        bool wantsJump = InputManager.Instance.IsActionJustDown(InputManager.ActionType.Jump);

        HandleRunning(wantsRun);
        HandleCrouch(wantsCrouch);

        TargetMovement = Vector3.Lerp(TargetMovement, Vector3.zero, drag);
        
        if (TargetMovement != Vector3.zero)
        {
            TargetMovement = TargetMovement.normalized * GetSpeed(Running, Crouching);
        }
        if (TargetMovement.magnitude < 0.1f)
        {
            Running = 0f;
        }
        
        HandleJump(wantsJump);
    }

    private void HandleRunning(bool wantsRun)
    {
        Running = (wantsRun ? Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(this.groundAngle / this.maxAngleWalking)) : 0f);
    }

    private void HandleCrouch(bool wantsCrouch)
    {
        Crouching = (wantsCrouch ? 1f : GetForcedCrouchAmount());
        this.capsule.height = Mathf.Lerp(this.capsuleHeight, this.capsuleHeightCrouched, Crouching);
        this.capsule.center = new Vector3(0f, Mathf.Lerp(this.capsuleCenter, this.capsuleCenterCrouched, Crouching), 0f);
    }

    private float GetForcedCrouchAmount()
    {
        Vector3 sphereStartPosition = transform.position + new Vector3(0f, capsule.radius, 0f);
        float sphereRadius = capsule.radius - 0.1f;
        float moveDistance = capsuleHeight - capsule.radius - sphereRadius + 0.2f;
        
        // Use Physics.SphereCast to check for ceiling
        RaycastHit hit;
        Vector3 castStart = sphereStartPosition;
        Vector3 castDirection = transform.up;
        
        if (Physics.SphereCast(castStart, sphereRadius, castDirection, out hit, moveDistance, collisionLayers))
        {
            float heightDifference = hit.point.y + sphereRadius - this.rb.transform.position.y - 0.2f;
            float crouchAmount = Mathf.InverseLerp(this.capsuleHeight, this.capsuleHeightCrouched, heightDifference);
            return crouchAmount;
        }
        
        return 0f;
    }

    private void HandleJump(bool wantsJump, bool jumpInDirection = false)
    {
        if (!wantsJump || !this.CanJump())
        {
            return;
        }

        this.Jump(jumpInDirection);
    }

    private bool CanJump()
    {
        return UnityEngine.Time.time - this.jumpTime >= 0.5f && (UnityEngine.Time.time - this.groundTime <= 0.1f && UnityEngine.Time.time - this.landTime >= 0.1f && (UnityEngine.Time.time - this.landTime >= 0.2f || !this.sliding));
    }

    public void BlockJump(float duration)
    {
        if (duration > 0f)
        {
            this.jumpTime = UnityEngine.Time.time + duration - 0.5f;
        }
    }

    private void Jump(bool jumpInDirection = false)
    {		
        //if (Player) // Check if player can jump

        this.sliding = false;
        this.climbing = false;
        this.grounded = false;
        this.jumping = true;
        this.jumpTime = UnityEngine.Time.time;

        if (jumpInDirection)
        {
            this.rb.linearVelocity += Vector3.Lerp(transform.forward * 9f, Vector3.zero, drag);
        }
        else
        {
            this.rb.linearVelocity += Vector3.Lerp(Vector3.up * 9f, Vector3.zero, drag);
        }
    }

    private Vector3 FallVelocity()
    {
        return new Vector3(0f, Mathf.Min(0f, this.rb.linearVelocity.y), 0f);
    }

    private void UpdateVelocity()
    {
        Vector3 currentVelocity = this.rb.linearVelocity;

        if (this.wasFlying && !this.isFlying)
        {
            currentVelocity = Vector3.zero;
        }

        if (this.isFlying)
        {
            currentVelocity += (TargetMovement - currentVelocity) * Mathf.Clamp01(10f * Time.fixedDeltaTime);
        }
        else if (this.grounded || this.climbing)
        {
            Vector3 groundNormalHorizontal = new Vector3(this.groundNormal.x, 0f, this.groundNormal.z).normalized;
            Vector3 slopeAlignedTargetMovement = TargetMovement + groundNormalHorizontal * Mathf.Max(0f, -Vector3.Dot(groundNormalHorizontal, TargetMovement));
            float blendFactor = this.groundAngle - this.maxAngleWalking + 0.5f;
            currentVelocity = Vector3.Lerp(TargetMovement, slopeAlignedTargetMovement, blendFactor) + this.FallVelocity();
        }
        else if (this.sliding)
        {
            Vector3 xzVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            currentVelocity += (TargetMovement - xzVelocity) * Mathf.Clamp01(3f * Time.fixedDeltaTime);
            currentVelocity = Vector3.Lerp(currentVelocity, (currentVelocity - xzVelocity) * 0.2f, this.drag);
        }
        else
        {
            Vector3 xzVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            currentVelocity += (TargetMovement - xzVelocity) * Mathf.Clamp01(3f * Time.fixedDeltaTime);
            currentVelocity = Vector3.Lerp(currentVelocity, (currentVelocity - xzVelocity) * 0.2f, this.drag);
        }   

        if (!this.isFlying)
        {
            float currentSpeedMagnitude = currentVelocity.magnitude;
            float maxAllowedSpeed = Mathf.Max(this.maxVelocity, TargetMovement.magnitude);
            if (currentSpeedMagnitude > maxAllowedSpeed)
            {
                currentVelocity *= maxAllowedSpeed / currentSpeedMagnitude;
            }
        }

        this.rb.linearVelocity = currentVelocity;
    }

    private void UpdateGravity()
    {
        this.capsule.material = ((TargetMovement.magnitude <= 0f) ? this.highFrictionMaterial : this.zeroFrictionMaterial);
        if (this.isFlying)
        {
            return;
        }
        Ray groundCheckRay = new Ray(this.capsule.bounds.center, Vector3.down);
        float sphereCastDistance = this.capsule.bounds.extents.y;
        bool shouldApplyGravity = (!this.grounded && !this.climbing) || !UnityEngine.Physics.SphereCast(groundCheckRay, this.gravityTestRadius, sphereCastDistance, collisionLayers);
        if (shouldApplyGravity)
        {
            this.rb.AddForce(UnityEngine.Physics.gravity * this.gravityMultiplier * this.rb.mass);
            this.capsule.material = this.zeroFrictionMaterial;
        }
    }

    private void DetermineInitialFlags()
    {
        this.transform.rotation = Quaternion.identity;

        isAdmin = false;
        adminNoClip = false;

        this.groundNormal = groundNormalNew;
        this.groundAngle = groundAngleNew;
        this.grounded = (this.groundAngle <= this.maxAngleWalking 
                                          && !this.jumping);

        this.climbing = (this.groundAngle <= this.maxAngleClimbing 
                                          && !this.jumping 
                                          && !this.grounded);

        this.sliding = (this.groundAngle <= this.maxAngleSliding 
                                         && !this.jumping 
                                         && !this.grounded
                                         && !this.climbing);

        this.jumping = (this.rb.linearVelocity.y > 0f
                                           && !this.grounded 
                                           && !this.climbing 
                                           && !this.sliding);

        this.falling = (this.rb.linearVelocity.y < 0f
                                         && !this.grounded 
                                         && !this.climbing 
                                         && !this.sliding 
                                         && !this.jumping);

        if (!this.isFlying && (this.wasJumping || this.wasFalling)
                           && !this.jumping 
                           && !this.falling
                           && Time.time - this.groundTime > 0.3f)
        {
            this.landTime = Time.time;
        }
        
        if (this.grounded || this.climbing || this.sliding)
        {
            this.groundTime = Time.time;
        }

    }

    private void DetermineFinalFlags()
    {
        this.wasGrounded = this.grounded;
        this.wasClimbing = this.climbing;
        this.wasSliding = this.sliding;
        this.wasJumping = this.jumping;
        this.wasFalling = this.falling;
        this.previousPosition = transform.localPosition; // Visual interpolation at 144hz not physics's 60hz, since Update goes at 50hz and first sets the position then does the Tick
        this.previousVelocity = rb.linearVelocity;
        this.groundAngleNew = float.MaxValue;
        this.groundNormalNew = Vector3.up;
    }

    // Gets touching ground normals only below feet
    void CollisionCheck(Collision collision)
    {
        float groundCheckHeight = capsule.bounds.min.y + capsule.radius;

        foreach (ContactPoint contact in collision.contacts)
        {
            if (contact.point.y <= groundCheckHeight)
            {
                Vector3 normal = contact.normal;
                float angle = Vector3.Angle(normal, Vector3.up);

                if (angle < groundAngleNew)
                {
                    groundAngleNew = angle;
                    groundNormalNew = normal;
                }
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        CollisionCheck(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        CollisionCheck(collision);
    }

    public float GetSpeed(float running, float ducking)
    {
        return Mathf.Lerp(Mathf.Lerp(2.8f, 5.5f, running), 1.7f, ducking);
    }

    private void UpdateDebugInfo()
    {
        string debugText = $"Grounded: {grounded}\n" +
                          $"Climbing: {climbing}\n" +
                          $"Sliding: {sliding}\n" +
                          $"Jumping: {jumping}\n" +
                          $"Falling: {falling}\n" +
                          $"Ground Time: {Time.time - groundTime:F2}s\n" +
                          $"Land Time: {Time.time - landTime:F2}s\n" +
                          $"Jump Time: {Time.time - jumpTime:F2}s\n" +
                          $"Position: {transform.position}\n" +
                          $"Velocity: {rb.linearVelocity}\n" +
                          $"Target: {TargetMovement}\n" +
                          $"Ground Angle: {groundAngle}";
        
        DebugManager.Instance?.SetDebugText(debugText);
    }
}
