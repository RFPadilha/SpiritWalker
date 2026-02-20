using UnityEngine;

/// <summary>
/// A gate that slides open permanently when <see cref="Unlock"/> is called.
/// Unlike <see cref="SpiritDoor"/>, it requires no pressure plates and never closes again.
/// Wire a <see cref="KeyPickup"/> to unlock this gate when its key is collected.
/// </summary>
public class KeyGate : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("World-space offset from the closed position to the fully open position.")]
    [SerializeField] Vector3 openOffset = new Vector3(0f, 4f, 0f);
    [Tooltip("Speed at which the gate slides open (units/sec).")]
    [SerializeField] float openSpeed = 3f;

    private bool    unlocked;
    private Vector3 closedPosition;
    private Vector3 openPosition;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Start()
    {
        closedPosition = transform.position;
        openPosition   = closedPosition + openOffset;
    }

    private void Update()
    {
        if (!unlocked) return;

        transform.position = Vector3.MoveTowards(
            transform.position, openPosition, openSpeed * Time.deltaTime);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Permanently opens this gate. Called by <see cref="KeyPickup"/> when collected.
    /// </summary>
    public void Unlock()
    {
        unlocked = true;
    }

    /// <summary>
    /// Snaps the gate back to its closed position and locks it again.
    /// Called by <see cref="RespawnManager"/> when the player dies.
    /// </summary>
    public void ResetGate()
    {
        unlocked = false;
        transform.position = closedPosition;
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        // Ghost of where the gate ends up when fully open.
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.18f);
        Gizmos.DrawCube(transform.position + openOffset, transform.lossyScale);
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.65f);
        Gizmos.DrawWireCube(transform.position + openOffset, transform.lossyScale);
    }

    private void OnDrawGizmosSelected()
    {
        // Arrow from closed to open position.
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + openOffset);

        // Solid ghost preview when selected.
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.35f);
        Gizmos.DrawCube(transform.position + openOffset, transform.lossyScale);
    }
}
