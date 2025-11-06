using System.Text;
using UnityEngine;

/// <summary>
/// - Visiual lerp removed; use Rigidbody interpolation instead
/// - Input snapshot
/// - Ground check: touch at first, else SphereCastNonAlloc
/// - Reduced unnecessary assignments, reduced GC pressure
/// </summary>
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    // Admin / NoClip
    [Header("Admin / NoClip")]
    [SerializeField] private bool isAdmin;
    [SerializeField] private bool adminNoClip;

    //  Movement Config
    [Header("Movement Config")]
    [SerializeField, Range(0f, 1f)] private float drag = 0f;
    [SerializeField] private float maxAngleWalking = 50f;
    [SerializeField] private float maxAngleClimbing = 60f;
    [SerializeField] private float maxAngleSliding = 90f;
    [SerializeField] private float maxVelocity = 50f;

    // Components 
    private CapsuleCollider capsule;
    public Rigidbody rb;

    // Movement Vars 
    private Vector3 groundNormalNew = Vector3.up;
    private Vector3 groundNormal = Vector3.up;
    private float groundAngleNew = float.MaxValue;
    private float groundAngle = 0f;
    private float groundTime;
    private float landTime;
    private float jumpTime;

    // Public Movement Vars 
    [Header("Public Movement Variables")]
    public Vector3 TargetMovement;
    public float Running;   // 0..1
    public float Crouching; // 0..1
    public bool hasMovingParent;

    //  NoClip Speeds & Physics 
    [Header("Private Movement Variables")]
    [SerializeField] private float noClipSpeed = 10f;
    [SerializeField] private float noClipCrouchSpeed = 2f;
    [SerializeField] private float noClipSprintSpeed = 50f;
    [SerializeField] private LayerMask collisionLayers;
    [SerializeField] private float gravityMultiplier = 2.5f;
    [SerializeField] private PhysicMaterial highFrictionMaterial;
    [SerializeField] private PhysicMaterial zeroFrictionMaterial;

    // Capsule Collider Vars 
    [Header("Capsule Collider Variables")]
    [SerializeField] private float capsuleHeight;
    [SerializeField] private float capsuleHeightCrouched;
    [SerializeField] private float capsuleCenter;
    [SerializeField] private float capsuleCenterCrouched;
    [SerializeField] private float gravityTestRadius = 0.2f;

    // State & Flags 
    private enum MoveState : byte { Grounded, Climbing, Sliding, Airborne, Flying }
    private MoveState state = MoveState.Airborne;
    private bool wasFlying, jumping, falling;
    private bool wasJumping, wasFalling;
    private bool hadContactsThisStep;

    // Cache / Constants 
    private static readonly RaycastHit[] HitsCache = new RaycastHit[4];
    private static readonly RaycastHit[] hitsCache = new RaycastHit[16];
    private PhysicMaterial currentMat;
    private float fixedDt;

    // Input Snapshot 
    private struct InputSnapshot
    {
        public bool f, b, l, r, run, crouch, jump, jumpJust;
    }
    private InputSnapshot input;

    // Debug Throttle 
#if DEBUG
    private float nextDebugTime;
#endif

    // Unity Hooks 
    private void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();
        rb      = GetComponent<Rigidbody>();

        // Rigidbody visiual smoothing
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.freezeRotation = true; // Lock rotation 

        // Capsule vars init (default to current if zero)
        if (capsuleHeight <= 0f)
            capsuleHeight = capsule.height;

        if (capsuleHeightCrouched <= 0f)
            capsuleHeightCrouched = capsule.height * 0.5f;

        if (Mathf.Approximately(capsuleCenter, 0f))
            capsuleCenter = capsule.center.y;
            
        if (Mathf.Approximately(capsuleCenterCrouched, 0f)) 
            capsuleCenterCrouched = capsule.center.y * 0.5f;

        currentMat = capsule.material;
        fixedDt = Time.fixedDeltaTime;
    }

    private void Update()
    {
        // input snapshot 
        var im = InputManager.Instance;
        input.f        = im.IsActionDown(InputManager.ActionType.Forward);
        input.b        = im.IsActionDown(InputManager.ActionType.Backward);
        input.l        = im.IsActionDown(InputManager.ActionType.Left);
        input.r        = im.IsActionDown(InputManager.ActionType.Right);
        input.run      = im.IsActionDown(InputManager.ActionType.Run);
        input.crouch   = im.IsActionDown(InputManager.ActionType.Crouch);
        input.jumpJust = im.IsActionJustDown(InputManager.ActionType.Jump);
        input.jump     = im.IsActionDown(InputManager.ActionType.Jump);
        
#if DEBUG
        UpdateDebugInfo();
#endif
    }

    private void FixedUpdate()
    {
        hadContactsThisStep = false; // reset for this step

        DetermineMovementType(); // input -> target movement
        PhysicsUpdate();         // velocity, gravity, state
    }

    private void OnCollisionEnter(Collision c)
    {
        CollisionCheck(c);
    }

    private void OnCollisionStay(Collision c)
    {
        hadContactsThisStep = true;
        CollisionCheck(c);
    }

    // High-level flow 
    private void DetermineMovementType()
    {
        wasFlying = (state == MoveState.Flying);

        bool isFlyingNow = isAdmin && adminNoClip;
        if (isFlyingNow)
        {
            state = MoveState.Flying;
            NoClipMovement();
        }
        else
        {
            WalkingMovement();
        }
    }

    private void PhysicsUpdate()
    {
        DetermineInitialFlags();

        UpdateVelocity();
        UpdateGravity();

        DetermineFinalFlags();
    }

    // Movement Modes 
    private void NoClipMovement()
    {
        // target direction
        TargetMovement = Vector3.zero;

        // Transform directions
        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;

        if (input.f) TargetMovement += fwd;
        if (input.b) TargetMovement -= fwd;
        if (input.l) TargetMovement -= right;
        if (input.r) TargetMovement += right;
        if (input.jump) TargetMovement += Vector3.up;

        float speed = noClipSpeed;

        if (input.run)
        {
            if (Mathf.Approximately(TargetMovement.x, 0f) &&
                Mathf.Approximately(TargetMovement.z, 0f) &&
                TargetMovement.y <= 0f)
            {
                TargetMovement -= Vector3.up; // down when no horizontal movement
                speed = noClipSpeed;
                TargetMovement = TargetMovement.normalized * speed;
                return;
            }
            speed = noClipSprintSpeed;
        }

        if (input.crouch)
            speed = noClipCrouchSpeed;

        TargetMovement = TargetMovement.sqrMagnitude > 0f ? TargetMovement.normalized * speed : Vector3.zero;
    }

    private void WalkingMovement()
    {
        TargetMovement = Vector3.zero;

        Vector3 fwd = transform.forward;
        Vector3 right = transform.right;

        if (input.f) TargetMovement += fwd;
        if (input.b) TargetMovement -= fwd;
        if (input.l) TargetMovement -= right;
        if (input.r) TargetMovement += right;

        // Sprint (only forward)
        bool onlyForward = input.f && !input.l && !input.b && !input.r;
        bool wantsRun = onlyForward && input.run;
        bool wantsCrouch = input.crouch;
        bool wantsJump = input.jumpJust;

        HandleRunning(wantsRun);
        HandleCrouch(wantsCrouch);

        // Drag application
        if (TargetMovement != Vector3.zero)
        {
            TargetMovement = Vector3.Lerp(TargetMovement, Vector3.zero, drag);
            TargetMovement = TargetMovement.normalized * GetSpeed(Running, Crouching);
        }
        else
        {
            TargetMovement = Vector3.zero;
            Running = 0f;
        }

        HandleJump(wantsJump);
    }

    // Helpers
    private float lastRunning = -1f, lastCrouching = -1f;

    private void HandleRunning(bool wantsRun) 
    {
        float target = wantsRun ? Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(groundAngle / maxAngleWalking)) : 0f;
        if (!Mathf.Approximately(target, lastRunning))
        {
            Running = lastRunning = target;
        }
    }

    private void HandleCrouch(bool wantsCrouch)
    {
        float target = wantsCrouch ? 1f : GetForcedCrouchAmount();
        if (!Mathf.Approximately(target, lastCrouching))
        {
            Crouching = lastCrouching = target;
            // capsule settings
            capsule.height = Mathf.Lerp(capsuleHeight, capsuleHeightCrouched, Crouching);
            var c = capsule.center;
            c.y = Mathf.Lerp(capsuleCenter, capsuleCenterCrouched, Crouching);
            capsule.center = c;
        }
    }

    private float GetForcedCrouchAmount()
    {
        // CheckCapsule (better than SphereCast) 
        float radius = capsule.radius - 0.05f;
        Vector3 start = transform.position + Vector3.up * radius;
        float headHeight = Mathf.Lerp(capsuleHeight, capsuleHeightCrouched, 0f);
        Vector3 end = start + Vector3.up * (headHeight - radius * 2f);

        bool blocked = Physics.CheckCapsule(start, end, radius, collisionLayers, QueryTriggerInteraction.Ignore);
        return blocked ? 1f : 0f;
    }

    private void HandleJump(bool wantsJump, bool jumpInDirection = false)
    {
        if (!wantsJump || !CanJump()) return;
        Jump(jumpInDirection);
    }

    private bool CanJump()
    {
        return Time.time - jumpTime >= 0.5f &&
                (Time.time - groundTime <= 0.1f &&
                Time.time - landTime >= 0.1f &&
               (Time.time - landTime >= 0.2f || state != MoveState.Sliding));
    }

    public void BlockJump(float duration)
    {
        if (duration > 0f)
            jumpTime = Time.time + duration - 0.5f;
    }

    private void Jump(bool jumpInDirection = false)
    {
        state = MoveState.Airborne;
        jumping = true;
        falling = false;
        jumpTime = Time.time;

        Vector3 add = jumpInDirection ? transform.forward * 9f : Vector3.up * 9f;
        rb.velocity += Vector3.Lerp(add, Vector3.zero, drag);
    }

    public float GetSpeed(float running, float ducking)
    {
        return Mathf.Lerp(Mathf.Lerp(2.8f, 5.5f, running), 1.7f, ducking);
    }

    // Physics core 
    private void UpdateVelocity()
    {
        Vector3 v = rb.velocity;

        if (wasFlying && state != MoveState.Flying)
            v = Vector3.zero;

        if (state == MoveState.Flying)
        {
            // NoClip damping
            float t = Mathf.Clamp01(10f * fixedDt);
            v += (TargetMovement - v) * t;
        }
        else if (state == MoveState.Grounded || state == MoveState.Climbing)
        {
            Vector3 groundNormalHorizontal = new Vector3(groundNormal.x, 0f, groundNormal.z).normalized;
            Vector3 slopeAlignedTarget = TargetMovement + groundNormalHorizontal * Mathf.Max(0f, -Vector3.Dot(groundNormalHorizontal, TargetMovement));
            float blend = Mathf.Clamp01((groundAngle - maxAngleWalking + 0.5f) / Mathf.Max(0.0001f, maxAngleSliding - maxAngleWalking));
            v = Vector3.Lerp(TargetMovement, slopeAlignedTarget, blend) + FallVelocity(v);
        }
        else
        {
            // fly damping
            Vector3 xz = new Vector3(v.x, 0f, v.z);
            float t = Mathf.Clamp01(3f * fixedDt);
            xz += (TargetMovement - xz) * t;
            v = new Vector3(xz.x, v.y, xz.z);
            v = Vector3.Lerp(v, (v - xz) * 0.2f, drag);
        }

        // Max velocity clamp if not flying
        if (state != MoveState.Flying)
        {
            float maxAllowed = Mathf.Max(maxVelocity, TargetMovement.magnitude);
            float sq = v.sqrMagnitude;
            float maxSq = maxAllowed * maxAllowed;
            if (sq > maxSq)
                v *= (maxAllowed / Mathf.Sqrt(sq));
        }

        rb.velocity = v;
    }

    private static Vector3 FallVelocity(in Vector3 v) => new Vector3(0f, Mathf.Min(0f, v.y), 0f);

    private void UpdateGravity()
    {
        if (state == MoveState.Flying) return;

        bool shouldApplyGravity;

        if (hadContactsThisStep)
        {
            shouldApplyGravity = false;
        }
        else
        {
            var center = capsule.bounds.center;
            int count = Physics.SphereCastNonAlloc(new Ray(center, Vector3.down), gravityTestRadius, HitsCache, dist, collisionLayers, QueryTriggerInteraction.Ignore);
            shouldApplyGravity = (count == 0);
            shouldApplyGravity = (count == 0);
        }

        if (shouldApplyGravity)
        {
            rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
            SetCapsuleMaterial(zeroFrictionMaterial);
        }
        else
        {
            // friction control
            if (TargetMovement.sqrMagnitude <= 0.0001f) SetCapsuleMaterial(highFrictionMaterial);
            else SetCapsuleMaterial(zeroFrictionMaterial);
        }
    }

    private void DetermineInitialFlags()
    {
        // Ground/Climb/Slide decision
        groundNormal = groundNormalNew;
        groundAngle  = groundAngleNew;

        bool groundedNow = (groundAngle <= maxAngleWalking && !jumping);
        bool climbingNow = (!groundedNow && groundAngle <= maxAngleClimbing && !jumping);
        bool slidingNow  = (!groundedNow && !climbingNow && groundAngle <= maxAngleSliding && !jumping);

        if (state != MoveState.Flying)
        {
            if (groundedNow) state = MoveState.Grounded;
            else if (climbingNow) state = MoveState.Climbing;
            else if (slidingNow) state = MoveState.Sliding;
            else state = MoveState.Airborne;
        }

        jumping = (rb.velocity.y > 0f && state == MoveState.Airborne);
        falling = (rb.velocity.y < 0f && state == MoveState.Airborne);

        if (state != MoveState.Flying && (wasJumping || wasFalling) && !jumping && !falling && Time.time - groundTime > 0.3f)
            landTime = Time.time;

        if (state == MoveState.Grounded || state == MoveState.Climbing || state == MoveState.Sliding)
            groundTime = Time.time;
    }

    private void DetermineFinalFlags()
    {
        wasJumping = jumping;
        wasFalling = falling;

        // reset for next step
        groundAngleNew  = float.MaxValue;
        groundNormalNew = Vector3.up;
    }

    // Collision / Ground Solve 
    private void CollisionCheck(Collision collision)
    {
        float groundCheckHeight = capsule.bounds.min.y + capsule.radius;

        // En küçük açı (en dik olmayan yer) zemin kabul edilecek
        foreach (var contact in collision.contacts)
        {
            if (contact.point.y <= groundCheckHeight)
            {
                Vector3 n = contact.normal;
                float angle = Vector3.Angle(n, Vector3.up);
                if (angle < groundAngleNew)
                {
                    groundAngleNew = angle;
                    groundNormalNew = n;
                }
            }
        }
    }

    //  Utils 
    private void SetCapsuleMaterial(PhysicMaterial mat)
    {
        if (currentMat == mat) return;
        capsule.material = mat;
        currentMat = mat;
    }

#if DEBUG
    private void UpdateDebugInfo()
    {
        if (Time.unscaledTime < nextDebugTime) return;
        nextDebugTime = Time.unscaledTime + 0.2f;

        var sb = new StringBuilder(256);
        sb.Append("State: ").Append(state)
          .Append("\nJumping: ").Append(jumping)
          .Append("\nFalling: ").Append(falling)
          .Append("\nGround Time: ").Append((Time.time - groundTime).ToString("F2")).Append("s")
          .Append("\nLand Time: ").Append((Time.time - landTime).ToString("F2")).Append("s")
          .Append("\nJump Time: ").Append((Time.time - jumpTime).ToString("F2")).Append("s")
          .Append("\nPos: ").Append(transform.position)
          .Append("\nVel: ").Append(rb.velocity)
          .Append("\nTarget: ").Append(TargetMovement)
          .Append("\nGround Angle: ").Append(groundAngle);

        DebugManager.Instance?.SetDebugText(sb.ToString());
    }
#else
    private void UpdateDebugInfo() { }
#endif
}
