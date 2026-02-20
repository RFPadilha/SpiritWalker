using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] Transform target;
    [SerializeField] Vector3 pivotOffset = new Vector3(0f, 1.5f, 0f);

    [Header("Orbit")]
    [SerializeField] float sensitivity = 0.15f;
    [SerializeField] float pitchMin = -25f;
    [SerializeField] float pitchMax = 60f;
    [SerializeField] float distance = 5f;

    [Header("Follow")]
    [SerializeField] float followSmoothing = 15f;

    [Header("Collision")]
    [Tooltip("Radius of the sphere used to probe for obstructions. Smaller = less clipping, larger = pulls in sooner.")]
    [SerializeField] float collisionRadius = 0.15f;
    [Tooltip("Gap kept between the camera sphere and the surface it hits.")]
    [SerializeField] float collisionSkin = 0.05f;
    [Tooltip("Layers the camera collides with. Exclude the Player layer to avoid self-occlusion.")]
    [SerializeField] LayerMask collisionMask = -1;
    [Tooltip("How fast the camera snaps toward an obstruction (units of distance per second).")]
    [SerializeField] float pullInSpeed = 20f;
    [Tooltip("How fast the camera eases back to full distance once the path is clear.")]
    [SerializeField] float returnSpeed = 4f;

    // Read by PlayerMovement / SoulController to rotate the character to match camera yaw
    public float Yaw => yaw;

    // Called by SoulSplitManager when switching control between body and soul
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        // Snap follow position so the camera doesn't lerp from the old target
        if (newTarget != null)
            currentFollowPos = newTarget.position;
    }

    private PlayerInputActions playerInputActions;
    private float yaw;
    private float pitch = 15f;          // start slightly above horizon
    private Vector3 currentFollowPos;
    private float currentDistance;

    private void Awake()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Game.Enable();

        if (target != null)
        {
            currentFollowPos = target.position;
            yaw = target.eulerAngles.y; // inherit player's initial facing
        }

        currentDistance = distance;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    private void OnDestroy()
    {
        playerInputActions.Dispose();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Accumulate mouse delta
        Vector2 lookDelta = playerInputActions.Game.Look.ReadValue<Vector2>();
        yaw   += lookDelta.x * sensitivity;
        pitch -= lookDelta.y * sensitivity; // subtract: mouse up → camera up
        pitch  = Mathf.Clamp(pitch, pitchMin, pitchMax);

        // Smooth-follow the target — unscaled so camera stays responsive during time slow
        currentFollowPos = Vector3.Lerp(currentFollowPos, target.position, followSmoothing * Time.unscaledDeltaTime);

        // Orbit position: start directly behind the pivot, then rotate by yaw/pitch
        Vector3 pivot  = currentFollowPos + pivotOffset;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 dir    = rot * Vector3.back; // unit vector pointing away from pivot

        // Probe for obstructions between the pivot and the desired camera position.
        // SphereCast instead of Raycast so thin walls near the edge of the frame
        // are caught before the lens centre clips through them.
        float desiredDistance = distance;
        if (Physics.SphereCast(pivot, collisionRadius, dir, out RaycastHit hit,
                distance, collisionMask, QueryTriggerInteraction.Ignore))
        {
            desiredDistance = Mathf.Max(hit.distance - collisionSkin, collisionRadius);
        }

        // Pull in instantly when blocked; ease back smoothly when the path clears.
        float speed = desiredDistance < currentDistance ? pullInSpeed : returnSpeed;
        currentDistance = Mathf.Lerp(currentDistance, desiredDistance, speed * Time.unscaledDeltaTime);

        transform.position = pivot + dir * currentDistance;
        transform.LookAt(pivot);
    }
}
