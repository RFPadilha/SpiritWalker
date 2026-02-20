using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class SoulSplitManager : MonoBehaviour
{
    public enum SoulState { Unified, SoulWalking, SoulAnchored, Traversing }

    [Header("References")]
    [SerializeField] PlayerMovement body;
    [SerializeField] SoulController soul;
    [SerializeField] PlayerCameraController cameraController;

    [Header("Ability 1 — Soul Walk")]
    [Tooltip("Real-world seconds the player controls the soul before the body snaps to it")]
    [SerializeField] float soulWalkDuration = 6f;
    [Tooltip("Time.timeScale while the soul is walking (0.1 = very slow, 1 = normal)")]
    [SerializeField] float timeSlowScale = 0.4f;

    [Header("Ability 2 — Soul Anchor")]
    [Tooltip("Real-world seconds before the body teleports to the anchored soul")]
    [SerializeField] float soulAnchorDuration = 3f;

    [Header("Path Traversal")]
    [Tooltip("Speed at which the body travels along the recorded path (units/sec)")]
    [SerializeField] float traversalSpeed = 15f;

    [Header("Visuals — Soul Tether")]
    [SerializeField] SoulTether soulTether;

    [Header("Visuals — Otherworldly Filter")]
    [SerializeField] Volume soulVisionVolume;
    [Tooltip("How fast the post-processing filter fades in/out (units per real-world second)")]
    [SerializeField] float volumeFadeSpeed = 4f;

    [Header("Visuals — Ability Timer")]
    [SerializeField] AbilityTimerUI abilityTimerUI;

    public static SoulSplitManager Instance { get; private set; }

    public SoulState State => state;

    private PlayerInputActions playerInputActions;
    private SoulState state = SoulState.Unified;
    private float timer;
    private float timerDuration;
    private float targetVolumeWeight;
    private Rigidbody soulRb;
    private RigidbodyConstraints soulDefaultConstraints;

    // Traversal
    private Rigidbody bodyRb;
    private Animator bodyAnimator;
    private bool bodyDefaultIsKinematic;
    private readonly List<Vector3> traversalWaypoints = new List<Vector3>();
    private int traversalIndex;
    private bool forceGroundedOnArrival;

    private void Awake()
    {
        Instance = this;
        playerInputActions = new PlayerInputActions();
        playerInputActions.Game.Enable();
        playerInputActions.Game.SoulWalk.performed      += _ => OnSoulWalkPressed();
        playerInputActions.Game.SoulAnchor.performed     += _ => OnSoulAnchorPressed();
        playerInputActions.Game.CancelAbility.performed  += _ => CancelActiveAbility();
    }

    private void Start()
    {
        soulRb = soul.GetComponent<Rigidbody>();
        soulDefaultConstraints = soulRb.constraints;

        bodyRb = body.GetComponent<Rigidbody>();
        bodyDefaultIsKinematic = bodyRb.isKinematic;
        bodyAnimator = body.GetComponentInChildren<Animator>();

        soul.Initialize(cameraController);
        soul.gameObject.SetActive(false);

        if (soulVisionVolume != null)
            soulVisionVolume.weight = 0f;

        // Ignore all collisions between body and soul so overlapping on spawn
        // doesn't produce a physics impulse that launches either character
        foreach (var bc in body.GetComponentsInChildren<Collider>(true))
            foreach (var sc in soul.GetComponentsInChildren<Collider>(true))
                Physics.IgnoreCollision(bc, sc, true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        playerInputActions.Dispose();
        Time.timeScale = 1f;
    }

    private void Update()
    {
        UpdateVolumeWeight();

        switch (state)
        {
            case SoulState.SoulWalking:
                timer -= Time.unscaledDeltaTime;
                UpdateTimerUI();
                if (timer <= 0f && soul.IsGrounded)
                    CompleteSoulWalk();
                break;

            case SoulState.SoulAnchored:
                timer -= Time.unscaledDeltaTime;
                UpdateTimerUI();
                if (timer <= 0f)
                    CompleteSoulAnchor();
                break;

            case SoulState.Traversing:
                UpdateTraversal();
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Ability 1 — Soul Walk
    // Soul separates and the player navigates it. Body is frozen. World slows.
    // When the real-time duration expires AND the soul is grounded, body warps.
    // Press Q again to complete early. Esc/Tab to cancel (body stays put).
    // -------------------------------------------------------------------------
    private void OnSoulWalkPressed()
    {
        switch (state)
        {
            case SoulState.Unified:
                ActivateSoulWalk();
                break;
            case SoulState.SoulWalking:
                CompleteSoulWalk();
                break;
        }
    }

    private void ActivateSoulWalk()
    {
        PlaceSoulAtBody();
        soul.gameObject.SetActive(true); // SoulController.OnEnable runs here

        body.enabled = false;
        Time.timeScale = timeSlowScale;
        cameraController.SetTarget(soul.transform);

        state = SoulState.SoulWalking;
        timer = soulWalkDuration;
        timerDuration = soulWalkDuration;
        ActivateVisuals(body.transform, soul.transform);
    }

    private void CompleteSoulWalk()
    {
        // Path goes anchor(body) → mover(soul): body follows it forward
        BeginTraversal(reversePath: false, forceGrounded: false);
    }

    // -------------------------------------------------------------------------
    // Ability 2 — Soul Anchor
    // Plants the soul as a fixed beacon at the body's feet (only if grounded).
    // Body stays active. After the real-time duration, body warps to the soul.
    // Press E again to complete early. Esc/Tab to cancel (body stays put).
    // -------------------------------------------------------------------------
    private void OnSoulAnchorPressed()
    {
        switch (state)
        {
            case SoulState.Unified:
                ActivateSoulAnchor();
                break;
            case SoulState.SoulAnchored:
                CompleteSoulAnchor();
                break;
        }
    }

    private void ActivateSoulAnchor()
    {
        if (!body.IsGrounded) return;
        PlaceSoulAtBody();
        soul.gameObject.SetActive(true);
        FreezeSoul();
        state = SoulState.SoulAnchored;
        timer = soulAnchorDuration;
        timerDuration = soulAnchorDuration;
        ActivateVisuals(soul.transform, body.transform);
    }

    private void CompleteSoulAnchor()
    {
        // Path goes anchor(soul) → mover(body): body retraces in reverse back to soul
        BeginTraversal(reversePath: true, forceGrounded: true);
    }

    // -------------------------------------------------------------------------
    // Cancel — Esc / Tab
    // Dismisses the soul and returns control to the body at its current position.
    // -------------------------------------------------------------------------
    private void CancelActiveAbility()
    {
        if (state == SoulState.Unified) return;
        ReturnToUnified();
    }

    // -------------------------------------------------------------------------
    // Path Traversal
    // Body moves along the recorded tether path instead of teleporting.
    // -------------------------------------------------------------------------
    private void BeginTraversal(bool reversePath, bool forceGrounded)
    {
        // Snapshot the recorded path
        traversalWaypoints.Clear();
        if (soulTether != null)
        {
            for (int i = 0; i < soulTether.Path.Count; i++)
                traversalWaypoints.Add(soulTether.Path[i]);
        }

        if (reversePath)
            traversalWaypoints.Reverse();

        traversalIndex = 0;
        forceGroundedOnArrival = forceGrounded;

        // Freeze both characters: soul locked, body moved by us
        FreezeSoul();
        body.enabled = false;
        bodyRb.linearVelocity = Vector3.zero;
        bodyRb.angularVelocity = Vector3.zero;
        bodyRb.isKinematic = true;

        // Restore world time and follow the body
        Time.timeScale = 1f;
        cameraController.SetTarget(body.transform);

        // Switch tether to traversal rendering (shrinking line)
        if (soulTether != null) soulTether.BeginTraversal(reversePath);

        // Hide the timer during traversal
        if (abilityTimerUI != null) abilityTimerUI.Hide();

        state = SoulState.Traversing;
    }

    private void UpdateTraversal()
    {
        if (traversalWaypoints.Count == 0)
        {
            FinishTraversal();
            return;
        }

        // Move the body along waypoints, consuming as much distance as the frame allows
        float step = traversalSpeed * Time.unscaledDeltaTime;

        while (step > 0f && traversalIndex < traversalWaypoints.Count)
        {
            Vector3 target = traversalWaypoints[traversalIndex];
            Vector3 toTarget = target - body.transform.position;
            float dist = toTarget.magnitude;

            if (dist <= step)
            {
                body.transform.position = target;
                step -= dist;
                traversalIndex++;
            }
            else
            {
                body.transform.position += toTarget.normalized * step;
                step = 0f;
            }
        }

        // Face movement direction
        if (traversalIndex < traversalWaypoints.Count)
        {
            Vector3 lookDir = traversalWaypoints[traversalIndex] - body.transform.position;
            lookDir.y = 0f;
            if (lookDir.sqrMagnitude > 0.001f)
                body.transform.rotation = Quaternion.LookRotation(lookDir);
        }

        // Drive the run animation on the body
        if (bodyAnimator != null)
        {
            bodyAnimator.SetFloat("VelocityZ", 8f, 0.1f, Time.unscaledDeltaTime);
            bodyAnimator.SetFloat("VelocityX", 0f, 0.1f, Time.unscaledDeltaTime);
            bodyAnimator.SetBool("IsGrounded", true);
        }

        // Update tether visual to shrink as body progresses
        if (soulTether != null)
            soulTether.UpdateTraversalHead(traversalIndex, body.transform.position);

        if (traversalIndex >= traversalWaypoints.Count)
            FinishTraversal();
    }

    private void FinishTraversal()
    {
        bodyRb.isKinematic = bodyDefaultIsKinematic;
        if (forceGroundedOnArrival)
            body.ForceGrounded();
        ReturnToUnified();
    }

    // -------------------------------------------------------------------------
    // Visuals
    // -------------------------------------------------------------------------
    private void ActivateVisuals(Transform anchor, Transform mover)
    {
        targetVolumeWeight = 1f;
        if (soulTether != null) soulTether.Activate(anchor, mover);
        if (abilityTimerUI != null) abilityTimerUI.Show();
    }

    private void DeactivateVisuals()
    {
        targetVolumeWeight = 0f;
        if (soulTether != null) soulTether.Deactivate();
        if (abilityTimerUI != null) abilityTimerUI.Hide();
    }

    private void UpdateVolumeWeight()
    {
        if (soulVisionVolume == null) return;
        soulVisionVolume.weight = Mathf.MoveTowards(
            soulVisionVolume.weight, targetVolumeWeight,
            volumeFadeSpeed * Time.unscaledDeltaTime);
    }

    private void UpdateTimerUI()
    {
        if (abilityTimerUI != null && timerDuration > 0f)
            abilityTimerUI.SetFill(Mathf.Clamp01(timer / timerDuration));
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// If Soul Anchor is currently active, immediately triggers the body's return traversal
    /// as though the anchor timer expired. Has no effect in any other state.
    /// </summary>
    public void TriggerAnchorReturn()
    {
        if (state == SoulState.SoulAnchored)
            CompleteSoulAnchor();
    }

    /// <summary>
    /// Immediately cancels any active soul ability and returns to the Unified state.
    /// Safe to call from any state, including Traversing. Used by the respawn system.
    /// </summary>
    public void ForceReset()
    {
        if (state == SoulState.Unified) return;

        // During traversal the body is kinematic — restore it before ReturnToUnified
        // so that the subsequent physics teleport lands correctly.
        if (state == SoulState.Traversing)
            bodyRb.isKinematic = bodyDefaultIsKinematic;

        ReturnToUnified();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------
    private void FreezeSoul()
    {
        soul.enabled = false;                   // stop SoulController input/movement
        soulRb.linearVelocity  = Vector3.zero;
        soulRb.angularVelocity = Vector3.zero;
        soulRb.constraints     = RigidbodyConstraints.FreezeAll;
    }

    private void UnfreezeSoul()
    {
        soulRb.constraints = soulDefaultConstraints;
        soul.enabled       = true;
    }

    private void PlaceSoulAtBody()
    {
        soul.transform.SetPositionAndRotation(body.transform.position, body.transform.rotation);
    }

    private void ReturnToUnified()
    {
        // Restore body physics in case we're coming from traversal
        bodyRb.isKinematic = bodyDefaultIsKinematic;

        DeactivateVisuals();
        UnfreezeSoul();
        soul.gameObject.SetActive(false);
        body.enabled = true;
        Time.timeScale = 1f;
        cameraController.SetTarget(body.transform);
        state = SoulState.Unified;
    }
}
