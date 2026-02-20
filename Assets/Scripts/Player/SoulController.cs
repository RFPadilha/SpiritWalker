using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class SoulController : MonoBehaviour
{
    [Header("Movement")]
    // Higher than body to compensate for time-scale: soul moves at maxSpeed in real-world
    // units/sec even when timeScale < 1. Tune alongside SoulSplitManager.timeSlowScale.
    [SerializeField] float maxSpeed = 10f;
    [SerializeField] float backwardsMaxSpeed = 5f;
    [SerializeField] float acceleration = 50f;
    [SerializeField] float deceleration = 80f;
    [SerializeField] float rotationSpeed = 720f;

    [Header("Physics")]
    [Tooltip("0 = no gravity, 1 = full gravity. Soul is intentionally lighter than the body.")]
    [SerializeField] float gravityMultiplier = 0.35f;

    [Header("Jump")]
    [Tooltip("Vertical impulse on jump. With 0.35× gravity the soul reaches ~9 units — enough for the Spirit Pillar.")]
    [SerializeField] float jumpForce = 8f;

    [Header("Ground Check")]
    [SerializeField] float groundCheckRadius = 0.25f;
    [SerializeField] float landingAnticipationDistance = 0.6f;
    [SerializeField] LayerMask groundLayer = -1;

    [Header("Animation")]
    [SerializeField] float animatorVelocityScale = 8f;
    [SerializeField] float animatorDampTime = 0.1f;

    public bool IsGrounded => isGrounded;

    private PlayerInputActions playerInputActions;
    private Rigidbody rb;
    private Animator animator;
    private PlayerCameraController cameraController;
    private bool isGrounded;
    private bool landingAnticipated;
    private bool jumpRequested;

    public void Initialize(PlayerCameraController cam)
    {
        cameraController = cam;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.linearDamping = 0f;
        rb.useGravity = false; // we apply reduced gravity manually below

        animator = GetComponentInChildren<Animator>();

        playerInputActions = new PlayerInputActions();
        playerInputActions.Game.Enable();
        playerInputActions.Game.Jump.performed += _ => jumpRequested = true;
    }

    private void OnDestroy()
    {
        playerInputActions.Dispose();
    }

    private void OnEnable()
    {
        isGrounded = true;
        landingAnticipated = false;
        jumpRequested = false;

        if (rb != null)
            rb.linearVelocity = Vector3.zero;

        if (animator != null)
        {
            // Set the parameter immediately so the Animator doesn't evaluate
            // IsGrounded=false between SetActive and the first FixedUpdate
            animator.SetBool("IsGrounded", true);

            // Clear any stale triggers left over from a previous activation
            animator.ResetTrigger("StartFalling");
            animator.ResetTrigger("PreLand");
            animator.ResetTrigger("LandComplete");

            // Force back to the locomotion blend tree regardless of what state
            // the animator was in when it was last deactivated
            animator.Play("2D Blend Tree", 0, 0f);
        }
    }

    private void FixedUpdate()
    {
        ApplyGravity();
        CheckGround();
        CheckLandingAnticipation();
        ApplyJump();
        ApplyMovement();
        UpdateAnimator();
    }

    private void ApplyJump()
    {
        if (jumpRequested && isGrounded)
            animator.SetTrigger("Jump"); // SoulAnimationEvents.OnJumpLaunch applies the force

        jumpRequested = false;
    }

    // Called by SoulAnimationEvents.OnJumpLaunch at the exact launch frame of the jump animation.
    public void ApplyJumpForce()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
    }

    private void ApplyGravity()
    {
        // Manual gravity so we can tune it independently of timeScale
        rb.AddForce(Physics.gravity * gravityMultiplier, ForceMode.Acceleration);
    }

    private void CheckGround()
    {
        bool wasGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(
            transform.position + Vector3.down * 0.05f, groundCheckRadius, groundLayer);

        if (wasGrounded && !isGrounded)
            landingAnticipated = false;
    }

    private void CheckLandingAnticipation()
    {
        if (isGrounded || landingAnticipated || rb.linearVelocity.y >= 0f) return;

        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
                landingAnticipationDistance + 1f, groundLayer))
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
        Vector2 input = playerInputActions.Game.Movement.ReadValue<Vector2>();
        Vector3 moveDir = GetCameraRelativeDirection(input);

        // Compensate velocity and time step so the soul always moves at maxSpeed
        // in real-world units/sec regardless of Time.timeScale
        float tsf = Time.timeScale > 0.01f ? 1f / Time.timeScale : 1f;
        float compensatedDt = Time.fixedUnscaledDeltaTime * tsf;

        bool movingBackwardLocally = transform.InverseTransformDirection(rb.linearVelocity).z < 0f;
        float speedCap = (movingBackwardLocally ? backwardsMaxSpeed : maxSpeed) * tsf;

        Vector3 targetVelocityXZ = moveDir.magnitude > 0.01f ? moveDir * speedCap : Vector3.zero;
        Vector3 currentVelocityXZ = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        float rate = moveDir.magnitude < 0.01f ? deceleration : acceleration;
        Vector3 newVelocityXZ = Vector3.MoveTowards(currentVelocityXZ, targetVelocityXZ, rate * compensatedDt);

        rb.linearVelocity = new Vector3(newVelocityXZ.x, rb.linearVelocity.y, newVelocityXZ.z);

        if (moveDir.magnitude > 0.1f && cameraController != null)
        {
            Quaternion targetRotation = Quaternion.Euler(0f, cameraController.Yaw, 0f);
            rb.MoveRotation(Quaternion.RotateTowards(
                rb.rotation, targetRotation, rotationSpeed * compensatedDt));
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null) return;

        // Normalize against the compensated speed so the blend tree always sees 0..8
        float tsf = Time.timeScale > 0.01f ? 1f / Time.timeScale : 1f;
        float effectiveMaxSpeed = maxSpeed * tsf;

        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float velZ = (localVelocity.z / effectiveMaxSpeed) * animatorVelocityScale;
        float velX = (localVelocity.x / effectiveMaxSpeed) * animatorVelocityScale;

        float backwardExtreme = -(backwardsMaxSpeed / maxSpeed) * animatorVelocityScale;
        velZ = Mathf.Clamp(velZ, backwardExtreme, animatorVelocityScale);

        float dt = Time.fixedUnscaledDeltaTime;
        animator.SetFloat("VelocityZ", velZ, animatorDampTime, dt);
        animator.SetFloat("VelocityX", velX, animatorDampTime, dt);
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("VerticalVelocity", rb.linearVelocity.y * tsf, animatorDampTime, dt);
    }

    private Vector3 GetCameraRelativeDirection(Vector2 input)
    {
        if (cameraController == null || input.magnitude < 0.01f)
            return new Vector3(input.x, 0f, input.y).normalized;

        Vector3 camForward = Vector3.ProjectOnPlane(cameraController.transform.forward, Vector3.up).normalized;
        Vector3 camRight   = Vector3.ProjectOnPlane(cameraController.transform.right,   Vector3.up).normalized;

        return (camForward * input.y + camRight * input.x).normalized;
    }
}
