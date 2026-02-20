using UnityEngine;

/// <summary>
/// A collectible key. When the player body walks through the trigger, the key is
/// picked up and all linked <see cref="KeyGate"/>s are permanently unlocked.
///
/// Setup: this GameObject needs a Collider set to Is Trigger.
/// Wire one or more KeyGate references in the Inspector.
/// </summary>
[RequireComponent(typeof(Collider))]
public class KeyPickup : MonoBehaviour
{
    [Header("Connections")]
    [Tooltip("All gates that this key will permanently unlock on pickup.")]
    [SerializeField] KeyGate[] gates;

    [Header("Presentation")]
    [Tooltip("Child Transform holding the key mesh. Rotates continuously on the Y-axis.")]
    [SerializeField] Transform meshPivot;
    [Tooltip("Degrees per second for the Y-axis spin.")]
    [SerializeField] float rotationSpeed = 90f;
    [Tooltip("Looping particle effect that signals the key is interactable (e.g. Sparks flashing yellow).")]
    [SerializeField] ParticleSystem idleVFX;

    [Header("Pickup Feedback")]
    [Tooltip("One-shot particle burst played at the key's position when collected.")]
    [SerializeField] ParticleSystem pickupVFX;

    private bool collected;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        if (idleVFX != null) idleVFX.Play();
    }

    private void Update()
    {
        if (collected || meshPivot == null) return;
        meshPivot.RotateAround(transform.position, Vector3.up, rotationSpeed * Time.deltaTime);
    }

    // -------------------------------------------------------------------------
    // Pickup — body only
    // -------------------------------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        collected = true;

        foreach (var gate in gates)
            gate?.Unlock();

        if (idleVFX != null) idleVFX.Stop();

        if (pickupVFX != null)
        {
            // Detach so the burst outlives the key GameObject.
            pickupVFX.transform.SetParent(null);
            pickupVFX.Play();
        }

        gameObject.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Restores the key to its uncollected state.
    /// Called by <see cref="RespawnManager"/> when the player dies.
    /// </summary>
    public void ResetPickup()
    {
        collected = false;
        gameObject.SetActive(true);
        if (idleVFX != null) idleVFX.Play();
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        if (gates == null) return;

        foreach (var gate in gates)
        {
            if (gate == null) continue;
            Gizmos.color = new Color(1f, 0.85f, 0f, 0.7f);
            Gizmos.DrawLine(transform.position, gate.transform.position);
            Gizmos.DrawSphere(gate.transform.position + Vector3.up * 0.3f, 0.1f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Gold sphere to mark the pickup location in the scene view.
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, 0.4f);
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position, 0.4f);
    }
}
