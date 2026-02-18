using UnityEngine;

/// <summary>
/// A door that slides open when its required pressure plates are active.
/// Assign plates in the Inspector; connection lines are drawn in the Scene view.
/// </summary>
public class SpiritDoor : MonoBehaviour
{
    public enum ActivationMode
    {
        /// <summary>All connected plates must be active simultaneously.</summary>
        RequireAll,
        /// <summary>Any one connected plate being active is enough.</summary>
        RequireAny
    }

    [Header("Connections")]
    [SerializeField] PressurePlate[] requiredPlates;
    [SerializeField] ActivationMode activationMode = ActivationMode.RequireAll;

    [Header("Movement")]
    [Tooltip("World-space offset from the closed position to the fully open position.")]
    [SerializeField] Vector3 openOffset = new Vector3(0f, 4f, 0f);
    [SerializeField] float openSpeed  = 3f;
    [SerializeField] float closeSpeed = 2f;

    private Vector3 closedPosition;
    private Vector3 openPosition;

    private void Start()
    {
        closedPosition = transform.position;
        openPosition   = closedPosition + openOffset;
    }

    private void Update()
    {
        bool open  = ShouldBeOpen();
        float speed = open ? openSpeed : closeSpeed;
        transform.position = Vector3.MoveTowards(
            transform.position,
            open ? openPosition : closedPosition,
            speed * Time.deltaTime);
    }

    private bool ShouldBeOpen()
    {
        if (requiredPlates == null || requiredPlates.Length == 0) return false;

        if (activationMode == ActivationMode.RequireAll)
        {
            foreach (var plate in requiredPlates)
                if (plate == null || !plate.IsActive) return false;
            return true;
        }
        else // RequireAny
        {
            foreach (var plate in requiredPlates)
                if (plate != null && plate.IsActive) return true;
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (requiredPlates == null) return;

        foreach (var plate in requiredPlates)
        {
            if (plate == null) continue;

            bool plateActive = Application.isPlaying && plate.IsActive;
            Gizmos.color = plateActive
                ? new Color(0.2f, 1f, 0.4f, 0.9f)   // green  — plate is live
                : new Color(0.4f, 0.8f, 1f,  0.7f);  // blue   — plate is waiting

            // Line from door pivot to plate
            Gizmos.DrawLine(transform.position, plate.transform.position);

            // Small sphere at the plate end so the line has a clear anchor point
            Gizmos.DrawSphere(plate.transform.position + Vector3.up * 0.3f, 0.1f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Brighten the connection lines when the door is selected
        if (requiredPlates != null)
        {
            foreach (var plate in requiredPlates)
            {
                if (plate == null) continue;
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, plate.transform.position);
            }
        }

        // Ghost preview of where the door will be when fully open.
        // Uses the door's own lossy scale so the preview scales with the object.
        Vector3 ghostCenter = transform.position + openOffset;
        Vector3 ghostSize   = transform.lossyScale;

        Gizmos.color = new Color(0f, 1f, 0f, 0.18f);
        Gizmos.DrawCube(ghostCenter, ghostSize);
        Gizmos.color = new Color(0f, 1f, 0f, 0.7f);
        Gizmos.DrawWireCube(ghostCenter, ghostSize);
    }
}
