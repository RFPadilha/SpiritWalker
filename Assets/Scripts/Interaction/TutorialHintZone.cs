using UnityEngine;

/// <summary>
/// Attach to any GameObject with a Trigger Collider.
/// Shows a hint when the player body enters the zone, hides it on exit.
///
/// Inspector settings:
///   hintMessage   — The text shown in TutorialHintDisplay.
///   autoHideSec   — If > 0, hint auto-hides after this many seconds even if
///                   the player is still inside (useful for one-liners that
///                   don't need to stay visible forever).
///   showOnce      — If true, the zone deactivates after its first trigger.
/// </summary>
[RequireComponent(typeof(Collider))]
public class TutorialHintZone : MonoBehaviour
{
    [SerializeField] [TextArea] string hintMessage = "Press [Q] to Soul Walk";
    [Tooltip("Auto-hide delay in seconds. 0 = stay visible until the player leaves the zone.")]
    [SerializeField] float autoHideSec = 0f;
    [Tooltip("Deactivate this zone after the player has seen it once.")]
    [SerializeField] bool showOnce = true;
    [Tooltip("When true, the soul entering the zone also triggers the hint.")]
    [SerializeField] bool detectSoul = false;

    private bool triggered;
    private float hideTimer;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Update()
    {
        if (hideTimer > 0f)
        {
            hideTimer -= Time.unscaledDeltaTime;
            if (hideTimer <= 0f)
                TutorialHintDisplay.Instance.Hide(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered && showOnce) return;
        if (!IsValidActivator(other)) return;

        TutorialHintDisplay.Instance.Show(hintMessage, this);
        triggered = true;

        if (autoHideSec > 0f)
            hideTimer = autoHideSec;
    }

    private void OnTriggerStay(Collider other)
    {
        // Fallback for the case where the player spawns inside the zone:
        // OnTriggerEnter never fires for pre-existing overlaps, so we catch
        // it here on the first physics tick that OnTriggerStay runs.
        if (!triggered)
            OnTriggerEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsValidActivator(other)) return;
        if (autoHideSec <= 0f)
            TutorialHintDisplay.Instance.Hide(this);
    }

    private bool IsValidActivator(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() != null) return true;
        if (detectSoul && other.GetComponentInParent<SoulController>() != null) return true;
        return false;
    }

    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);
        Gizmos.color = new Color(1f, 0.92f, 0.016f, 0.8f);
        if (col is BoxCollider box2)
            Gizmos.DrawWireCube(box2.center, box2.size);
    }
}
