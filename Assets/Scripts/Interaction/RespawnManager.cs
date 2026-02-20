using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that tracks the active checkpoint and handles the full respawn sequence:
///   1. Cancel any active soul ability.
///   2. Freeze the body.
///   3. Fade to black.
///   4. Teleport to checkpoint (while black).
///   5. Restore body physics and animator state.
///   6. Fade back in and reactivate player.
///
/// Creates its own full-screen fade canvas at runtime — no manual UI setup needed.
/// Place on a dedicated Manager GameObject and wire up all Inspector references.
/// </summary>
public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] SoulSplitManager soulSplitManager;
    [SerializeField] PlayerMovement   playerBody;

    [Header("Starting Checkpoint")]
    [Tooltip("The checkpoint that is active at the start of the level.")]
    [SerializeField] Checkpoint startingCheckpoint;

    [Header("Fade Timing")]
    [Tooltip("Seconds to fade from clear to black.")]
    [SerializeField] float fadeOutDuration = 0.35f;
    [Tooltip("Seconds held on black before the teleport and fade-in begin.")]
    [SerializeField] float holdDuration    = 0.1f;
    [Tooltip("Seconds to fade from black back to clear.")]
    [SerializeField] float fadeInDuration  = 0.5f;

    [Header("Respawn Cooldown")]
    [Tooltip("Seconds after a respawn during which kill zones are ignored.")]
    [SerializeField] float respawnCooldown = 1f;

    [Header("Level Start Fade")]
    [Tooltip("When true, the screen starts black and fades in when the level loads.")]
    [SerializeField] bool fadeInOnStart = true;
    [Tooltip("Duration of the opening fade-in, in seconds.")]
    [SerializeField] float startFadeInDuration = 1f;

    private Checkpoint activeCheckpoint;
    private Rigidbody  bodyRb;
    private bool       bodyDefaultIsKinematic;
    private float      cooldownTimer;
    private bool       isRespawning;
    private Image      fadeImage;

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        CreateFadeCanvas();
    }

    private void Start()
    {
        bodyRb                 = playerBody.GetComponent<Rigidbody>();
        bodyDefaultIsKinematic = bodyRb.isKinematic;

        if (startingCheckpoint != null)
            RegisterCheckpoint(startingCheckpoint);

        if (fadeInOnStart)
        {
            fadeImage.color = new Color(0f, 0f, 0f, 1f);
            StartCoroutine(Fade(1f, 0f, startFadeInDuration));
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.unscaledDeltaTime;
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Called by a Checkpoint trigger when the body walks through it.</summary>
    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == activeCheckpoint) return;

        activeCheckpoint?.Deactivate();
        activeCheckpoint = checkpoint;
        activeCheckpoint.Activate();
    }

    /// <summary>Called by a KillZone. Starts the fade-respawn sequence.</summary>
    public void Respawn()
    {
        if (isRespawning)       return;
        if (cooldownTimer > 0f) return;
        if (activeCheckpoint == null) return;

        // Soul Anchor is active: snap the body back to the anchor instead of dying.
        if (soulSplitManager.State == SoulSplitManager.SoulState.SoulAnchored)
        {
            soulSplitManager.TriggerAnchorReturn();
            return;
        }

        // Body is on rails during traversal — player cannot react. Skip.
        if (soulSplitManager.State == SoulSplitManager.SoulState.Traversing) return;

        isRespawning = true;
        StartCoroutine(RespawnSequence());
    }

    // -------------------------------------------------------------------------
    // Respawn sequence
    // -------------------------------------------------------------------------
    private IEnumerator RespawnSequence()
    {
        // 1. Cancel any active soul ability.
        //    ReturnToUnified re-enables PlayerMovement, so we freeze it right after.
        soulSplitManager.ForceReset();

        // 2. Freeze the body — player loses control for the duration of the fade.
        playerBody.enabled     = false;
        bodyRb.linearVelocity  = Vector3.zero;
        bodyRb.angularVelocity = Vector3.zero;
        bodyRb.isKinematic     = true;

        // 3. Fade to black.
        yield return StartCoroutine(Fade(0f, 1f, fadeOutDuration));
        yield return new WaitForSecondsRealtime(holdDuration);

        // 4. Teleport while the screen is black, and reset any collapsed floors.
        //    Both happen invisibly so the player never sees the snap.
        bodyRb.linearVelocity  = Vector3.zero;
        bodyRb.angularVelocity = Vector3.zero;
        playerBody.transform.SetPositionAndRotation(
            activeCheckpoint.SpawnPosition,
            activeCheckpoint.SpawnRotation);

        foreach (var floor in FindObjectsByType<CollapsingFloor>(FindObjectsSortMode.None))
            floor.ResetFloor();

        foreach (var key in FindObjectsByType<KeyPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            key.ResetPickup();

        foreach (var gate in FindObjectsByType<KeyGate>(FindObjectsSortMode.None))
            gate.ResetGate();

        // 5. Restore body physics and reset the animator to a clean idle state.
        bodyRb.isKinematic = bodyDefaultIsKinematic;
        playerBody.ForceGrounded();
        playerBody.enabled = true;

        // 6. Fade back in.
        yield return StartCoroutine(Fade(1f, 0f, fadeInDuration));

        // 7. Start the post-respawn cooldown, then allow new respawns.
        cooldownTimer = respawnCooldown;
        isRespawning  = false;
    }

    /// <summary>
    /// Fades the screen to black over <paramref name="duration"/> seconds.
    /// Intended for use by LevelExit before loading the next scene.
    /// </summary>
    public IEnumerator FadeToBlack(float duration)
    {
        yield return StartCoroutine(Fade(fadeImage.color.a, 1f, duration));
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            fadeImage.color = new Color(0f, 0f, 0f, alpha);
            yield return null;
        }
        fadeImage.color = new Color(0f, 0f, 0f, to);
    }

    // -------------------------------------------------------------------------
    // Fade canvas — created at runtime, no manual scene setup required
    // -------------------------------------------------------------------------
    private void CreateFadeCanvas()
    {
        var canvasGo = new GameObject("RespawnFadeCanvas");
        canvasGo.transform.SetParent(transform);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // render on top of all other UI

        canvasGo.AddComponent<CanvasScaler>();

        var panelGo = new GameObject("FadePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);

        fadeImage               = panelGo.AddComponent<Image>();
        fadeImage.color         = new Color(0f, 0f, 0f, 0f); // fully transparent at start
        fadeImage.raycastTarget = false;                      // don't swallow input events

        var rect       = panelGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
