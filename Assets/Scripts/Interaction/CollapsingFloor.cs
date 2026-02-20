using System.Collections;
using UnityEngine;

/// <summary>
/// A platform that begins crumbling when the player body lands on it, then falls
/// away after a short delay. Pairs naturally with Soul Anchor:
///   1. Anchor your soul on solid ground before the gap.
///   2. Run across, collect the item on the far side.
///   3. The floor collapses — body falls into the kill zone.
///   4. Kill zone triggers Anchor Return instead of death, snapping the body back safely.
///
/// Setup: this GameObject needs a non-trigger Collider (the walkable surface).
/// Optionally add child MeshRenderers for the visual. Assign a crumbleVFX for feedback.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CollapsingFloor : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("Seconds of shaking before the floor falls away.")]
    [SerializeField] float crumbleDuration = 1.2f;
    [Tooltip("Speed at which the floor visually sinks after collapsing (units/sec).")]
    [SerializeField] float fallSpeed = 6f;
    [Tooltip("Units to travel downward before disappearing. Should clear the kill zone below.")]
    [SerializeField] float fallDistance = 8f;

    [Header("Shake")]
    [Tooltip("Radius of the random positional shake during the crumble phase (units).")]
    [SerializeField] float shakeIntensity = 0.05f;

    [Header("Visual Feedback")]
    [Tooltip("Optional looping particle effect played during the crumble phase (e.g. Dust ground).")]
    [SerializeField] ParticleSystem crumbleVFX;

    private enum State { Stable, Crumbling, Fallen }

    private State   state          = State.Stable;
    private float   crumbleTimer;
    private Vector3 originLocalPos;
    private Collider[]    floorColliders;
    private MeshRenderer[] floorRenderers; // MeshRenderer excludes ParticleSystemRenderer

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        originLocalPos = transform.localPosition;
        floorColliders = GetComponents<Collider>();
        floorRenderers = GetComponentsInChildren<MeshRenderer>();
    }

    private void Update()
    {
        if (state != State.Crumbling) return;

        crumbleTimer -= Time.deltaTime;

        // Random shake around the rest position on XZ
        Vector2 shake = Random.insideUnitCircle * shakeIntensity;
        transform.localPosition = originLocalPos + new Vector3(shake.x, 0f, shake.y);

        if (crumbleTimer <= 0f)
        {
            transform.localPosition = originLocalPos; // snap before falling
            state = State.Fallen;
            StartCoroutine(Fall());
        }
    }

    // -------------------------------------------------------------------------
    // Collision — only the player body triggers the collapse
    // -------------------------------------------------------------------------
    private void OnCollisionEnter(Collision collision)
    {
        if (state != State.Stable) return;
        if (collision.gameObject.GetComponentInParent<PlayerMovement>() == null) return;

        state        = State.Crumbling;
        crumbleTimer = crumbleDuration;

        if (crumbleVFX != null) crumbleVFX.Play();
    }

    // -------------------------------------------------------------------------
    // Fall sequence
    // -------------------------------------------------------------------------
    private IEnumerator Fall()
    {
        // Disable colliders immediately so the player falls through right away.
        foreach (var col in floorColliders) col.enabled = false;

        float fallen = 0f;
        while (fallen < fallDistance)
        {
            float step = fallSpeed * Time.deltaTime;
            transform.position += Vector3.down * step;
            fallen += step;
            yield return null;
        }

        // Hide the mesh after it has sunk out of view.
        foreach (var r in floorRenderers) r.enabled = false;
        if (crumbleVFX != null) crumbleVFX.Stop();
    }

    /// <summary>
    /// Resets the floor to its original position and re-enables it.
    /// Called by <see cref="RespawnManager"/> when the player dies.
    /// </summary>
    public void ResetFloor()
    {
        transform.localPosition = originLocalPos;
        foreach (var col in floorColliders) col.enabled = true;
        foreach (var r in floorRenderers)   r.enabled   = true;
        state = State.Stable;
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        // Show where the floor will be after falling (end of fall distance)
        Gizmos.color = new Color(0.9f, 0.4f, 0.1f, 0.25f);
        Gizmos.DrawCube(transform.position + Vector3.down * fallDistance, transform.lossyScale);
        Gizmos.color = new Color(0.9f, 0.4f, 0.1f, 0.6f);
        Gizmos.DrawWireCube(transform.position + Vector3.down * fallDistance, transform.lossyScale);
    }
}
