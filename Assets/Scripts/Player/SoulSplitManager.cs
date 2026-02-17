using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

public class SoulSplitManager : MonoBehaviour
{
    public enum SoulState { Unified, SoulWalking, SoulAnchored }

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

    [Header("Visuals — Soul Tether")]
    [SerializeField] SoulTether soulTether;

    [Header("Visuals — Otherworldly Filter")]
    [SerializeField] Volume soulVisionVolume;
    [Tooltip("How fast the post-processing filter fades in/out (units per real-world second)")]
    [SerializeField] float volumeFadeSpeed = 4f;

    [Header("Visuals — Ability Timer")]
    [SerializeField] AbilityTimerUI abilityTimerUI;

    public SoulState State => state;

    private PlayerInputActions playerInputActions;
    private SoulState state = SoulState.Unified;
    private float timer;
    private float timerDuration;
    private float targetVolumeWeight;
    private Rigidbody soulRb;
    private RigidbodyConstraints soulDefaultConstraints;

    private void Awake()
    {
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
        ActivateVisuals();
    }

    private void CompleteSoulWalk()
    {
        TeleportBodyToSoul();
        ReturnToUnified();
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
        ActivateVisuals();
    }

    private void CompleteSoulAnchor()
    {
        TeleportBodyToSoul();
        body.ForceGrounded(); // soul was anchored on the ground, so body always arrives grounded
        ReturnToUnified();
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
    // Visuals
    // -------------------------------------------------------------------------
    private void ActivateVisuals()
    {
        targetVolumeWeight = 1f;
        if (soulTether != null) soulTether.Activate(body.transform, soul.transform);
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

    private void TeleportBodyToSoul()
    {
        body.transform.SetPositionAndRotation(soul.transform.position, soul.transform.rotation);
    }

    private void ReturnToUnified()
    {
        DeactivateVisuals();
        UnfreezeSoul();
        soul.gameObject.SetActive(false);
        body.enabled = true;
        Time.timeScale = 1f;
        cameraController.SetTarget(body.transform);
        state = SoulState.Unified;
    }
}
