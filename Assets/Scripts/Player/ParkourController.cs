using UnityEngine;
using UnityEngine.InputSystem;

public class ParkourController : MonoBehaviour
{
    public enum ParkourState
    {
        None,
        LedgeGrab,
        LedgeClimb,
        LedgeShimmy,
        WallRun,
        WallJump
    }

    [Header("References")]
    [SerializeField] PlayerMovement playerMovement;
    [SerializeField] PlayerCameraController cameraController;
    [SerializeField] SoulSplitManager soulSplitManager;

    [Header("Detection")]
    [SerializeField] LayerMask wallLayer;
    [SerializeField] float wallCheckDistance = 1.0f;
    [SerializeField] float wallCheckHeight = 1.0f;
    [SerializeField] float ledgeCheckUpOffset = 2.5f;
    [SerializeField] float ledgeCheckDownDistance = 2.5f;
    [SerializeField] float ledgeReachTolerance = 0.6f;
    [SerializeField] float capsuleHeight = 1.35f;

    [Header("Wall Run Detection")]
    [SerializeField] float wallRunMinSpeed = 4f;
    [SerializeField] float wallRunMaxAngle = 30f;

    [Header("Ledge Grab")]
    [SerializeField] float hangOffset = 0.35f;

    [Header("Ledge Climb")]
    [SerializeField] float climbDuration = 0.8f;
    [SerializeField] float climbArcHeight = 0.5f;

    [Header("Ledge Jump")]
    [SerializeField] float ledgeJumpAwayForce = 5f;
    [SerializeField] float ledgeJumpUpForce = 6f;

    [Header("Ledge Shimmy")]
    [SerializeField] float shimmySpeed = 2f;
    [SerializeField] float shimmySideCheckOffset = 0.5f;

    [Header("Wall Run")]
    [SerializeField] float wallRunSpeed = 8f;
    [SerializeField] float wallRunUpKick = 3f;
    [SerializeField] float wallRunGravity = 8f;
    [SerializeField] float wallRunMaxDuration = 1.2f;
    [SerializeField] float wallRunWallPush = 2f;
    [SerializeField] float wallRunMaxFallSpeed = 4f;

    [Header("Wall Jump")]
    [SerializeField] float wallJumpAwayForce = 6f;
    [SerializeField] float wallJumpUpForce = 7f;
    [SerializeField] float wallJumpForwardMomentum = 4f;
    [SerializeField] float wallJumpDuration = 0.3f;

    public ParkourState State => state;

    private PlayerInputActions playerInputActions;
    private Rigidbody rb;
    private Animator animator;
    private ParkourState state = ParkourState.None;

    // Detection results
    private Vector3 wallHitPoint;
    private Vector3 wallNormal;
    private Vector3 ledgePoint;
    private int wallRunSide; // -1 left, 1 right

    // Climb lerp
    private Vector3 climbStartPos;
    private Vector3 climbEndPos;
    private float climbTimer;

    // Wall run
    private Vector3 wallRunDirection;
    private float wallRunTimer;

    // Wall jump
    private float wallJumpTimer;
    private Vector3 lastWallNormal;

    // Input
    private bool jumpPressed;
    private Vector2 moveInput;

    private void Awake()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Game.Enable();
        playerInputActions.Game.Jump.performed += _ => { if (enabled) jumpPressed = true; };
    }

    private void OnDestroy()
    {
        playerInputActions.Dispose();
    }

    private void Update()
    {
        if (playerMovement == null) return;

        rb = playerMovement.Rb;
        animator = playerMovement.Anim;

        moveInput = playerInputActions.Game.Movement.ReadValue<Vector2>();

        switch (state)
        {
            case ParkourState.None:
                DetectParkourOpportunities();
                break;
            case ParkourState.LedgeGrab:
                UpdateLedgeGrab();
                break;
            case ParkourState.LedgeClimb:
                UpdateLedgeClimb();
                break;
            case ParkourState.LedgeShimmy:
                UpdateLedgeShimmy();
                break;
            case ParkourState.WallRun:
                UpdateWallRun();
                break;
            case ParkourState.WallJump:
                UpdateWallJump();
                break;
        }

        jumpPressed = false;
    }

    // -----------------------------------------------------------------
    // Detection (only when None + airborne)
    // -----------------------------------------------------------------
    private void DetectParkourOpportunities()
    {
        if (!CanActivateParkour()) return;
        if (playerMovement.IsGrounded) return;

        // Ledge grab: must be falling
        if (rb.linearVelocity.y <= 0f)
        {
            if (DetectLedge())
            {
                EnterLedgeGrab();
                return;
            }
        }

        // Wall run: must have horizontal speed
        float horizSpeed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        if (horizSpeed >= wallRunMinSpeed)
        {
            int side = DetectWallRunWall();
            if (side != 0)
            {
                wallRunSide = side;
                EnterWallRun();
                return;
            }
        }
    }

    private bool CanActivateParkour()
    {
        if (soulSplitManager != null && soulSplitManager.State != SoulSplitManager.SoulState.Unified)
            return false;
        if (!playerMovement.enabled) return false;
        return true;
    }

    // -----------------------------------------------------------------
    // Ledge Detection
    // -----------------------------------------------------------------
    private bool DetectLedge()
    {
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;

        // Forward wall check
        if (!Physics.Raycast(origin, transform.forward, out RaycastHit wallHit, wallCheckDistance, wallLayer))
            return false;

        wallHitPoint = wallHit.point;
        wallNormal = wallHit.normal;

        // Ledge top check — cast down from above wall hit
        // Offset AWAY from the wall (along wallNormal) so the origin stays outside mesh colliders
        Vector3 ledgeOrigin = wallHit.point + Vector3.up * ledgeCheckUpOffset + wallNormal * 0.1f;
        if (!Physics.Raycast(ledgeOrigin, Vector3.down, out RaycastHit ledgeHit, ledgeCheckDownDistance, wallLayer))
            return false;

        ledgePoint = ledgeHit.point;

        // Check if ledge is within reach of the player's top
        float playerTop = transform.position.y + capsuleHeight;
        float diff = ledgePoint.y - playerTop;
        return Mathf.Abs(diff) <= ledgeReachTolerance;
    }

    // -----------------------------------------------------------------
    // Wall Run Detection
    // -----------------------------------------------------------------
    private int DetectWallRunWall()
    {
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        Vector3 velocityXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).normalized;
        if (velocityXZ.sqrMagnitude < 0.01f) return 0;

        // Derive side directions from velocity, not transform.right
        // (PlayerMovement doesn't rotate while airborne, so transform.right is stale)
        Vector3 velocityRight = Vector3.Cross(Vector3.up, velocityXZ).normalized;

        // Check right side (relative to velocity)
        if (Physics.Raycast(origin, velocityRight, out RaycastHit rightHit, wallCheckDistance, wallLayer))
        {
            if (IsVelocityParallelToWall(velocityXZ, rightHit.normal))
            {
                wallHitPoint = rightHit.point;
                wallNormal = rightHit.normal;
                return 1;
            }
        }

        // Check left side (relative to velocity)
        if (Physics.Raycast(origin, -velocityRight, out RaycastHit leftHit, wallCheckDistance, wallLayer))
        {
            if (IsVelocityParallelToWall(velocityXZ, leftHit.normal))
            {
                wallHitPoint = leftHit.point;
                wallNormal = leftHit.normal;
                return -1;
            }
        }

        return 0;
    }

    private bool IsVelocityParallelToWall(Vector3 velocityDir, Vector3 normal)
    {
        Vector3 wallParallel = Vector3.Cross(Vector3.up, normal).normalized;
        float angle = Vector3.Angle(velocityDir, wallParallel);
        // Check both directions along the wall
        return angle < wallRunMaxAngle || angle > (180f - wallRunMaxAngle);
    }

    // -----------------------------------------------------------------
    // Enter / Exit Parkour
    // -----------------------------------------------------------------
    private void EnterParkour(ParkourState newState)
    {
        state = newState;
        playerMovement.enabled = false;

        if (animator != null)
        {
            animator.SetBool("IsParkour", true);
            animator.SetInteger("ParkourState", (int)state);
        }
    }

    private void ExitParkour()
    {
        state = ParkourState.None;
        playerMovement.enabled = true;

        if (animator != null)
        {
            animator.SetBool("IsParkour", false);
            animator.SetInteger("ParkourState", 0);
            animator.SetFloat("ShimmyDirection", 0f);
            animator.SetFloat("WallRunSide", 0f);
        }
    }

    public void ForceExit()
    {
        if (state == ParkourState.None) return;

        // Restore dynamic rigidbody if we set it kinematic
        if (rb != null && rb.isKinematic)
        {
            rb.isKinematic = false;
        }

        state = ParkourState.None;

        if (animator != null)
        {
            animator.SetBool("IsParkour", false);
            animator.SetInteger("ParkourState", 0);
            animator.SetFloat("ShimmyDirection", 0f);
            animator.SetFloat("WallRunSide", 0f);
        }

        // Do NOT re-enable PlayerMovement — the caller (SoulSplitManager) manages that
    }

    private void UpdateAnimatorState()
    {
        if (animator != null)
            animator.SetInteger("ParkourState", (int)state);
    }

    // -----------------------------------------------------------------
    // Ledge Grab
    // -----------------------------------------------------------------
    private void EnterLedgeGrab()
    {
        EnterParkour(ParkourState.LedgeGrab);

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;

        // Snap to hang position: below ledge, offset from wall
        Vector3 hangPos = ledgePoint - Vector3.up * capsuleHeight + wallNormal * hangOffset;
        transform.position = hangPos;
        transform.rotation = Quaternion.LookRotation(-wallNormal);

        lastWallNormal = wallNormal;
    }

    private void UpdateLedgeGrab()
    {
        if (jumpPressed)
        {
            // Forward input → climb up
            if (moveInput.y > 0.3f)
            {
                EnterLedgeClimb();
                return;
            }
            // Backward or no input → jump away
            else
            {
                LedgeJump();
                return;
            }
        }

        // Horizontal input → shimmy
        if (Mathf.Abs(moveInput.x) > 0.3f)
        {
            if (CanShimmy(Mathf.Sign(moveInput.x)))
            {
                EnterLedgeShimmy();
                return;
            }
        }
    }

    // -----------------------------------------------------------------
    // Ledge Climb
    // -----------------------------------------------------------------
    private void EnterLedgeClimb()
    {
        state = ParkourState.LedgeClimb;
        UpdateAnimatorState();

        climbStartPos = transform.position;
        // End position: on top of the ledge, slightly past the edge into the surface
        // wallNormal points away from wall (toward player), so -wallNormal goes onto the top
        climbEndPos = ledgePoint - wallNormal * 0.5f;
        climbTimer = 0f;
    }

    private void UpdateLedgeClimb()
    {
        climbTimer += Time.deltaTime;
        float t = Mathf.Clamp01(climbTimer / climbDuration);

        // Smoothstep for easing
        float smooth = t * t * (3f - 2f * t);

        // Arc: rise up then forward using a simple bezier-like arc
        Vector3 midPoint = climbStartPos + Vector3.up * (climbArcHeight + (climbEndPos.y - climbStartPos.y));
        Vector3 pos;
        if (smooth < 0.5f)
        {
            // First half: mostly vertical
            float subT = smooth * 2f;
            pos = Vector3.Lerp(climbStartPos, midPoint, subT);
        }
        else
        {
            // Second half: mostly horizontal toward top
            float subT = (smooth - 0.5f) * 2f;
            pos = Vector3.Lerp(midPoint, climbEndPos, subT);
        }

        transform.position = pos;

        if (t >= 1f)
        {
            CompleteLedgeClimb();
        }
    }

    public void CompleteLedgeClimb()
    {
        transform.position = climbEndPos;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        ExitParkour();
    }

    // -----------------------------------------------------------------
    // Ledge Jump
    // -----------------------------------------------------------------
    private void LedgeJump()
    {
        rb.isKinematic = false;
        rb.linearVelocity = wallNormal * ledgeJumpAwayForce + Vector3.up * ledgeJumpUpForce;
        ExitParkour();
    }

    // -----------------------------------------------------------------
    // Ledge Shimmy
    // -----------------------------------------------------------------
    private bool CanShimmy(float direction)
    {
        Vector3 sideDir = Vector3.Cross(Vector3.up, -wallNormal).normalized * direction;
        Vector3 checkOrigin = transform.position + Vector3.up * wallCheckHeight + sideDir * shimmySideCheckOffset;

        // Forward check: wall must continue
        if (!Physics.Raycast(checkOrigin, -wallNormal, wallCheckDistance, wallLayer))
            return false;

        // Downward check: ledge must continue
        Vector3 ledgeOrigin = wallHitPoint + sideDir * shimmySideCheckOffset + Vector3.up * ledgeCheckUpOffset + wallNormal * 0.1f;
        if (!Physics.Raycast(ledgeOrigin, Vector3.down, ledgeCheckDownDistance, wallLayer))
            return false;

        return true;
    }

    private void EnterLedgeShimmy()
    {
        state = ParkourState.LedgeShimmy;
        UpdateAnimatorState();
    }

    private void UpdateLedgeShimmy()
    {
        float inputX = moveInput.x;

        // Return to grab if no input
        if (Mathf.Abs(inputX) < 0.1f)
        {
            state = ParkourState.LedgeGrab;
            UpdateAnimatorState();
            if (animator != null)
                animator.SetFloat("ShimmyDirection", 0f);
            return;
        }

        float direction = Mathf.Sign(inputX);

        // Check if wall/ledge continues
        if (!CanShimmy(direction))
        {
            // Wall ends — drop
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            ExitParkour();
            return;
        }

        // Move along the wall
        Vector3 shimmyDir = Vector3.Cross(Vector3.up, -wallNormal).normalized * direction;
        transform.position += shimmyDir * shimmySpeed * Time.deltaTime;

        // Re-snap to wall to prevent drift
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        if (Physics.Raycast(origin, -wallNormal, out RaycastHit hit, wallCheckDistance * 1.5f, wallLayer))
        {
            wallNormal = hit.normal;
            wallHitPoint = hit.point;

            // Re-detect ledge point
            Vector3 ledgeOrigin = hit.point + Vector3.up * ledgeCheckUpOffset + hit.normal * 0.1f;
            if (Physics.Raycast(ledgeOrigin, Vector3.down, out RaycastHit ledgeHit, ledgeCheckDownDistance, wallLayer))
            {
                ledgePoint = ledgeHit.point;
                Vector3 hangPos = ledgePoint - Vector3.up * capsuleHeight + wallNormal * hangOffset;
                transform.position = hangPos;
            }

            transform.rotation = Quaternion.LookRotation(-wallNormal);
        }

        if (animator != null)
            animator.SetFloat("ShimmyDirection", direction);
    }

    // -----------------------------------------------------------------
    // Wall Run
    // -----------------------------------------------------------------
    private void EnterWallRun()
    {
        EnterParkour(ParkourState.WallRun);

        // Keep dynamic — we want gravity-like behavior
        rb.isKinematic = false;

        // Determine run direction along the wall
        Vector3 wallParallel = Vector3.Cross(Vector3.up, wallNormal).normalized;
        Vector3 velocityXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).normalized;
        if (Vector3.Dot(velocityXZ, wallParallel) < 0f)
            wallParallel = -wallParallel;
        wallRunDirection = wallParallel;

        // Set initial velocity: along wall + upward kick
        rb.linearVelocity = wallRunDirection * wallRunSpeed + Vector3.up * wallRunUpKick;

        // Face the run direction
        transform.rotation = Quaternion.LookRotation(wallRunDirection);

        wallRunTimer = 0f;
        lastWallNormal = wallNormal;

        if (animator != null)
            animator.SetFloat("WallRunSide", wallRunSide);
    }

    private void UpdateWallRun()
    {
        wallRunTimer += Time.deltaTime;

        // Apply custom gravity
        Vector3 vel = rb.linearVelocity;
        vel.y -= wallRunGravity * Time.deltaTime;

        // Maintain wall-parallel speed
        Vector3 horizVel = wallRunDirection * wallRunSpeed;
        vel.x = horizVel.x;
        vel.z = horizVel.z;

        // Push toward wall to maintain contact
        vel += -wallNormal * wallRunWallPush * Time.deltaTime;

        rb.linearVelocity = vel;

        // Continuous wall check
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;
        Vector3 sideDir = wallRunSide == 1 ? transform.right : -transform.right;
        bool wallStillThere = Physics.Raycast(origin, -wallNormal, out RaycastHit hit, wallCheckDistance * 1.5f, wallLayer);

        if (wallStillThere)
        {
            wallNormal = hit.normal;
            wallHitPoint = hit.point;
        }

        // Exit conditions
        if (wallRunTimer >= wallRunMaxDuration || !wallStillThere || vel.y < -wallRunMaxFallSpeed)
        {
            ExitWallRun();
            return;
        }

        // Jump input
        if (jumpPressed)
        {
            EnterWallJump();
            return;
        }
    }

    private void ExitWallRun()
    {
        ExitParkour();
    }

    // -----------------------------------------------------------------
    // Wall Jump
    // -----------------------------------------------------------------
    private void EnterWallJump()
    {
        state = ParkourState.WallJump;
        UpdateAnimatorState();

        rb.linearVelocity = wallNormal * wallJumpAwayForce
            + Vector3.up * wallJumpUpForce
            + wallRunDirection * wallJumpForwardMomentum;

        wallJumpTimer = 0f;
    }

    private void UpdateWallJump()
    {
        wallJumpTimer += Time.deltaTime;
        if (wallJumpTimer >= wallJumpDuration)
        {
            ExitParkour();
        }
    }

    // -----------------------------------------------------------------
    // Debug Gizmos
    // -----------------------------------------------------------------
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = transform.position + Vector3.up * wallCheckHeight;

        // Forward wall check
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, transform.forward * wallCheckDistance);

        // Side wall checks
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin, transform.right * wallCheckDistance);
        Gizmos.DrawRay(origin, -transform.right * wallCheckDistance);

        // Ledge check area
        if (state == ParkourState.LedgeGrab || state == ParkourState.LedgeShimmy)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(ledgePoint, 0.1f);
            Gizmos.DrawWireSphere(wallHitPoint, 0.1f);
        }

        // Wall run direction
        if (state == ParkourState.WallRun)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawRay(transform.position, wallRunDirection * 2f);
            Gizmos.DrawRay(transform.position, wallNormal);
        }

        // Ledge reach zone
        float playerTop = transform.position.y + capsuleHeight;
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireCube(
            new Vector3(transform.position.x, playerTop, transform.position.z),
            new Vector3(0.5f, ledgeReachTolerance * 2f, 0.5f));
    }
#endif
}
