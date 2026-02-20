using UnityEngine;

/// <summary>
/// A trigger zone that only harms the soul. When the soul enters it, the active
/// soul ability is immediately cancelled and the soul is dismissed.
///
/// This is invisible to the player body — use it to create hazards that force
/// careful soul routing, or to make Soul Anchor the required solution:
///
///   Soul Walk scenario: the soul must navigate around this zone to reach a target.
///   Soul Anchor scenario: the zone sits between body and goal; the body can cross
///   freely while the anchored soul stays safely behind.
///
/// Setup: Add a Collider set to Is Trigger.
/// Only cancels abilities during SoulWalking and SoulAnchored states.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SoulKillZone : MonoBehaviour
{
    [Header("Soul Walk Visuals")]
    [Tooltip("GameObjects enabled only while the player is Soul Walking. " +
             "Drag in particle systems, glowing meshes, lights — anything. " +
             "They are hidden at all other times.")]
    [SerializeField] GameObject[] soulWalkVFX;

    private bool vfxActive;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        // Ensure all VFX start hidden regardless of their scene state.
        SetVFX(false);
    }

    private void Update()
    {
        bool shouldBeActive = SoulSplitManager.Instance != null &&
                              SoulSplitManager.Instance.State == SoulSplitManager.SoulState.SoulWalking;

        if (shouldBeActive != vfxActive)
            SetVFX(shouldBeActive);
    }

    private void SetVFX(bool active)
    {
        vfxActive = active;
        foreach (var obj in soulWalkVFX)
            if (obj != null) obj.SetActive(active);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<SoulController>() == null) return;

        var mgr = SoulSplitManager.Instance;
        if (mgr == null) return;

        // Only interrupt active soul abilities. During Traversing the soul is
        // frozen in place — don't cancel the body's path for a passive overlap.
        if (mgr.State == SoulSplitManager.SoulState.SoulWalking ||
            mgr.State == SoulSplitManager.SoulState.SoulAnchored)
        {
            mgr.ForceReset();
        }
    }

    // -------------------------------------------------------------------------
    // Gizmos — blue/spirit palette to distinguish from the red body kill zone
    // -------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            Matrix4x4 prev = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(
                transform.TransformPoint(box.center),
                transform.rotation,
                transform.lossyScale);

            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.18f);
            Gizmos.DrawCube(Vector3.zero, box.size);
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.75f);
            Gizmos.DrawWireCube(Vector3.zero, box.size);

            Gizmos.matrix = prev;
        }
        else
        {
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.4f);
            Gizmos.DrawSphere(transform.position, 1f);
        }
    }
}
