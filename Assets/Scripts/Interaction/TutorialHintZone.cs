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
                TutorialHintDisplay.Instance?.Hide();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered && showOnce) return;
        if (other.GetComponentInParent<PlayerMovement>() == null) return;

        TutorialHintDisplay.Instance?.Show(hintMessage);
        triggered = true;

        if (autoHideSec > 0f)
            hideTimer = autoHideSec;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        if (autoHideSec <= 0f)
            TutorialHintDisplay.Instance?.Hide();
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
