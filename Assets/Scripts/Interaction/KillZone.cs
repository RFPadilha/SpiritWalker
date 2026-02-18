using UnityEngine;

/// <summary>
/// A trigger volume that respawns the player when their body enters it.
/// Typically placed as a large flat box below the playable area.
///
/// Only responds to the player body (PlayerMovement), not the soul,
/// so players can intentionally fly the soul off an edge without dying.
///
/// Setup: Add a BoxCollider (or any Collider) and enable Is Trigger.
/// </summary>
public class KillZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() != null)
            RespawnManager.Instance?.Respawn();
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            // Draw in the collider's local space so center/size are applied correctly
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(
                transform.TransformPoint(box.center),
                transform.rotation,
                transform.lossyScale);

            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.18f);
            Gizmos.DrawCube(Vector3.zero, box.size);
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.75f);
            Gizmos.DrawWireCube(Vector3.zero, box.size);

            Gizmos.matrix = prev;
        }
        else
        {
            // Fallback for non-box colliders: draw a flat disc
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.4f);
            Gizmos.DrawSphere(transform.position, 1f);
        }
    }
}
