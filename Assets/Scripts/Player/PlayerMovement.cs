using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Transform cameraTransform;
    [SerializeField] PlayerCameraController cameraController;

    [Header("Movement")]
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] float backwardsMaxSpeed = 5f;
    [SerializeField] float acceleration = 50f;
    [SerializeField] float deceleration = 80f;
    [SerializeField] float rotationSpeed = 720f; // degrees/sec

    [Header("Jump")]
    [SerializeField] float jumpForce = 8f;
    [SerializeField] float groundCheckRadius = 0.25f;
    [SerializeField] LayerMask groundLayer = -1; // -1 = Everything
    // How far above the ground to start the landing animation so its impact
    // frame coincides with the actual physics touchdown
    [SerializeField] float landingAnticipationDistance = 0.6f;

    [Header("Animation")]
    // Should match the furthest position values in the 2D Blend Tree (currently ±8)
    [SerializeField] float animatorVelocityScale = 8f;
    [SerializeField] float animatorDampTime = 0.1f;

    public Vector2 MovementInput { get; private set; }
    public bool IsGrounded => isGrounded;

    private PlayerInputActions playerInputActions;
    private Rigidbody rb;
    private Animator animator;
    private bool isGrounded;
    private bool jumpRequested;
    private bool landingAnticipated; // true once we've fired the pre-landing trigger

    private void Awake()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Game.Enable();
        playerInputActions.Game.Jump.performed += _ => jumpRequested = true;

        rb = GetComponent<Rigidbody>();
        animator = GetComponentInChildren<Animator>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    private void OnDestroy()
    {
        playerInputActions.Dispose();
    }

    private void FixedUpdate()
    {
        CheckGround();
        CheckLandingAnticipation();
        ApplyMovement();
        ApplyJump();
        UpdateAnimator();
    }

    private void CheckGround()
    {
        // Small sphere just below the character origin
        bool wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(transform.position + Vector3.down * 0.05f, groundCheckRadius, groundLayer);

        // Reset anticipation flag when we leave the ground so it can fire again on the next fall
        if (wasGrounded && !isGrounded)
            landingAnticipated = false;
    }

    private void CheckLandingAnticipation()
    {
        // Only relevant while airborne and falling (negative Y velocity)
        if (isGrounded || landingAnticipated || rb.linearVelocity.y >= 0f) return;

        // Cast down to find how far the ground is
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, landingAnticipationDistance + 1f, groundLayer))
        {
            if (hit.distance <= landingAnticipationDistance)
            {
                landingAnticipated = true;
                animator.SetTrigger("PreLand");
            }
        }
    }

    private void ApplyMovement()
    {
        MovementInput = playerInputActions.Game.Movement.ReadValue<Vector2>();

        // No steering in the air — momentum from the jump carries through
        if (!isGrounded) return;

        Vector3 moveDir = GetCameraRelativeDirection(MovementInput);

        // Use local-space velocity to determine if we're moving backward (briefly during turns)
        // so the backwards speed cap kicks in at the right time
        bool movingBackwardLocally = transform.InverseTransformDirection(rb.linearVelocity).z < 0f;
        float speedCap = movingBackwardLocally ? backwardsMaxSpeed : maxSpeed;

        Vector3 targetVelocityXZ = moveDir.magnitude > 0.01f ? moveDir * speedCap : Vector3.zero;
        Vector3 currentVelocityXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        float rate = moveDir.magnitude < 0.01f ? deceleration : acceleration;
        Vector3 newVelocityXZ = Vector3.MoveTowards(currentVelocityXZ, targetVelocityXZ, rate * Time.fixedDeltaTime);

        // Preserve Y so gravity and jump are not overridden
        rb.linearVelocity = new Vector3(newVelocityXZ.x, rb.linearVelocity.y, newVelocityXZ.z);

        // Rotate to match camera yaw, but only while moving so the camera
        // can orbit freely when the player is idle
        if (moveDir.magnitude > 0.1f && cameraController != null)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, cameraController.Yaw, 0f);
            rb.MoveRotation(Quaternion.RotateTowards(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime));
        }
    }

    private void ApplyJump()
    {
        if (jumpRequested && isGrounded)
            animator.SetTrigger("Jump"); // force is applied later by OnJumpLaunch animation event

        // Always consume the request — add a time buffer here later if needed
        jumpRequested = false;
    }

    // Called by PlayerAnimationEvents.OnJumpLaunch at the exact launch frame
    public void ApplyJumpForce()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
    }

    private void UpdateAnimator()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);

        // Normalize to blend tree coordinate space:
        // maxSpeed maps to +animatorVelocityScale, backwardsMaxSpeed maps to -backwardsRatio
        float velZ = (localVelocity.z / maxSpeed) * animatorVelocityScale;
        float velX = (localVelocity.x / maxSpeed) * animatorVelocityScale;

        float backwardExtreme = -(backwardsMaxSpeed / maxSpeed) * animatorVelocityScale;
        velZ = Mathf.Clamp(velZ, backwardExtreme, animatorVelocityScale);

        // Damped SetFloat prevents snapping between animation states
        animator.SetFloat("VelocityZ", velZ, animatorDampTime, Time.fixedDeltaTime);
        animator.SetFloat("VelocityX", velX, animatorDampTime, Time.fixedDeltaTime);

        // Airborne parameters — drive the jump/fall/land state machine
        // IsGrounded still set for any transitions that might need it (e.g. debug or future states)
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("VerticalVelocity", rb.linearVelocity.y, animatorDampTime, Time.fixedDeltaTime);
        // PreLand trigger is set by CheckLandingAnticipation() directly on the animator
    }

    // Called by PlayerAnimationEvents when the landing impact frame plays
    public void NotifyLandImpact()
    {
        // Hook up camera shake, dust VFX, landing sound, etc. here later
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (cameraTransform == null || input.magnitude < 0.01f)
            return new Vector3(input.x, 0f, input.y).normalized;

        // Flatten camera axes onto the XZ plane so slopes don't tilt movement direction
        Vector3 camForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(cameraTransform.right,   Vector3.up).normalized;

        return (camForward * input.y + camRight * input.x).normalized;
    }
}
