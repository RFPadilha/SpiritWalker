using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A trigger zone that activates when the player body or anchored soul stands on it.
/// Requires a Collider on this GameObject set to Is Trigger.
/// </summary>
public class PressurePlate : MonoBehaviour
{
    [Header("Activation")]
    [Tooltip("When true, only the soul (not the body) can activate this plate.")]
    [SerializeField] bool requireSoulOnly = false;

    [Header("Visual Feedback")]
    [Tooltip("The child Transform that holds the plate mesh. It will depress when the plate is active.")]
    [SerializeField] Transform plateVisual;
    [Tooltip("How many units the plate sinks when pressed.")]
    [SerializeField] float pressDepth = 0.07f;
    [Tooltip("Lerp speed for the press/release animation.")]
    [SerializeField] float pressSpeed = 15f;
    [Tooltip("Looping particle effect shown while the plate is idle (unactivated).")]
    [SerializeField] ParticleSystem idleVFX;
    [Tooltip("Particle effect played when the plate is first activated.")]
    [SerializeField] ParticleSystem activatedVFX;

    [Header("Events")]
    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;

    public bool IsActive { get; private set; }

    private readonly HashSet<Collider> activators = new HashSet<Collider>();
    private Vector3 plateRestLocalPos;

    private void Start()
    {
        if (plateVisual != null)
            plateRestLocalPos = plateVisual.localPosition;

        OnActivated.AddListener(OnPlateActivated);
        OnDeactivated.AddListener(OnPlateDeactivated);

        if (idleVFX != null)
            idleVFX.Play();
    }

    private void OnPlateActivated()
    {
        if (activatedVFX != null) activatedVFX.Play();
        if (idleVFX != null)     idleVFX.Stop();
    }

    private void OnPlateDeactivated()
    {
        if (idleVFX != null)      idleVFX.Play();
        if (activatedVFX != null) activatedVFX.Stop();
    }

    private void Update()
    {
        // Remove any activators that were deactivated (e.g. soul dismissed mid-trigger).
        // OnTriggerExit does not fire when a GameObject is SetActive(false).
        activators.RemoveWhere(c => c == null || !c.gameObject.activeInHierarchy);

        bool active = activators.Count > 0;
        if (active != IsActive)
        {
            IsActive = active;
            if (IsActive) OnActivated.Invoke();
            else          OnDeactivated.Invoke();
        }

        UpdatePlateVisual();
    }

    private void UpdatePlateVisual()
    {
        if (plateVisual == null) return;

        float targetY  = IsActive ? plateRestLocalPos.y - pressDepth : plateRestLocalPos.y;
        Vector3 target = new Vector3(plateRestLocalPos.x, targetY, plateRestLocalPos.z);
        plateVisual.localPosition = Vector3.Lerp(
            plateVisual.localPosition, target, pressSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsValidActivator(other))
            activators.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        activators.Remove(other);
    }

    private bool IsValidActivator(Collider other)
    {
        if (requireSoulOnly)
            return other.GetComponentInParent<SoulController>() != null;

        return other.GetComponentInParent<PlayerMovement>() != null
            || other.GetComponentInParent<SoulController>() != null;
    }

    // -------------------------------------------------------------------------
    // Gizmos
    // -------------------------------------------------------------------------
    private void OnDrawGizmos()
    {
        bool active = Application.isPlaying && IsActive;
        Gizmos.color = active ? Color.green : new Color(1f, 0.75f, 0f);

        // Flat slab to represent the plate surface
        Vector3 center = transform.position + Vector3.up * 0.05f;
        Vector3 size   = new Vector3(1f, 0.08f, 1f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = active ? new Color(0f, 0.8f, 0f) : new Color(0.8f, 0.6f, 0f);
        Gizmos.DrawWireCube(center, size);
    }
}
