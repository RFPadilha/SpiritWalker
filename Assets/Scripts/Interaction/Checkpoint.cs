using UnityEngine;

/// <summary>
/// A trigger zone that saves the player's respawn position.
/// Assign a child Transform to <see cref="spawnPoint"/> to control where the player
/// appears after dying. If left empty, the checkpoint's own position is used.
///
/// Setup: Add a Collider to this GameObject and enable Is Trigger.
/// </summary>
public class Checkpoint : MonoBehaviour
{
    [Tooltip("Where the player will spawn after dying. Leave empty to use this object's position.")]
    [SerializeField] Transform spawnPoint;

    public Vector3    SpawnPosition => spawnPoint != null ? spawnPoint.position : transform.position;
    public Quaternion SpawnRotation => spawnPoint != null ? spawnPoint.rotation : transform.rotation;

    public bool IsActive { get; private set; }

    public void Activate()   => IsActive = true;
    public void Deactivate() => IsActive = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() != null)
            RespawnManager.Instance?.RegisterCheckpoint(this);
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        // Colour: gold = active, grey = not yet reached
        Color col = (Application.isPlaying && IsActive)
            ? new Color(1f, 0.85f, 0f)
            : new Color(0.55f, 0.55f, 0.55f);

        DrawSpawnIcon(SpawnPosition, col);

        // Line from trigger centre to spawn point (only when they differ)
        if (spawnPoint != null)
        {
            Gizmos.color = col * new Color(1f, 1f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position, SpawnPosition);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Facing arrow so the designer can verify the spawn direction
        Vector3 spawnPos = SpawnPosition;
        Gizmos.color = Color.blue;
        Vector3 forward = SpawnRotation * Vector3.forward;
        Gizmos.DrawRay(spawnPos, forward * 1.5f);

        // Small arrowhead
        Vector3 tip = spawnPos + forward * 1.5f;
        Gizmos.DrawLine(tip, tip - forward * 0.4f + SpawnRotation * Vector3.right   * 0.2f);
        Gizmos.DrawLine(tip, tip - forward * 0.4f - SpawnRotation * Vector3.right   * 0.2f);
    }

    // Draws a simple person silhouette in world space at the given root position.
    private static void DrawSpawnIcon(Vector3 root, Color col)
    {
        Gizmos.color = col;

        // Head
        Gizmos.DrawWireSphere(root + Vector3.up * 1.85f, 0.15f);

        // Torso
        Gizmos.DrawLine(root + Vector3.up * 1.70f, root + Vector3.up * 1.05f);

        // Arms
        Gizmos.DrawLine(root + Vector3.up * 1.55f, root + Vector3.up * 1.25f + Vector3.right * 0.30f);
        Gizmos.DrawLine(root + Vector3.up * 1.55f, root + Vector3.up * 1.25f - Vector3.right * 0.30f);

        // Legs
        Gizmos.DrawLine(root + Vector3.up * 1.05f, root + Vector3.up * 0.50f + Vector3.right * 0.18f);
        Gizmos.DrawLine(root + Vector3.up * 1.05f, root + Vector3.up * 0.50f - Vector3.right * 0.18f);

        // Small platform base
        Gizmos.DrawLine(root + Vector3.right * 0.35f, root - Vector3.right * 0.35f);
    }
}
