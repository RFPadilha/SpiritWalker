using UnityEngine;

/// <summary>
/// Singleton that tracks the active checkpoint and handles the full respawn sequence:
///   1. Cancel any active soul ability (ForceReset).
///   2. Zero out the body's velocity.
///   3. Teleport the body to the checkpoint spawn position.
///
/// Place on a dedicated Manager GameObject in the scene and wire up all references.
/// </summary>
public class RespawnManager : MonoBehaviour
{
    public static RespawnManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] SoulSplitManager soulSplitManager;
    [SerializeField] PlayerMovement   playerBody;

    [Header("Starting Checkpoint")]
    [Tooltip("The checkpoint that is active at the start of the level (before the player reaches any other).")]
    [SerializeField] Checkpoint startingCheckpoint;

    [Header("Respawn")]
    [Tooltip("Seconds after a respawn during which another respawn cannot be triggered.")]
    [SerializeField] float respawnCooldown = 1.5f;

    private Checkpoint activeCheckpoint;
    private Rigidbody  bodyRb;
    private float      cooldownTimer;

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
    }

    private void Start()
    {
        bodyRb = playerBody.GetComponent<Rigidbody>();

        if (startingCheckpoint != null)
            RegisterCheckpoint(startingCheckpoint);
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

    /// <summary>
    /// Called by a Checkpoint trigger when the player body walks through it.
    /// No-ops if this checkpoint is already active.
    /// </summary>
    public void RegisterCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == activeCheckpoint) return;

        activeCheckpoint?.Deactivate();
        activeCheckpoint = checkpoint;
        activeCheckpoint.Activate();
    }

    /// <summary>
    /// Called by a KillZone to respawn the player at the active checkpoint.
    /// </summary>
    public void Respawn()
    {
        if (cooldownTimer > 0f)   return;
        if (activeCheckpoint == null) return;

        // During traversal the body is on rails — the player has no control and
        // cannot react. Ignore kill zones until the body reaches its destination.
        if (soulSplitManager.State == SoulSplitManager.SoulState.Traversing) return;

        cooldownTimer = respawnCooldown;

        // 1. Cancel any active soul ability and restore normal game state.
        soulSplitManager.ForceReset();

        // 2. Kill all body momentum before the teleport.
        bodyRb.linearVelocity  = Vector3.zero;
        bodyRb.angularVelocity = Vector3.zero;

        // 3. Teleport.
        playerBody.transform.SetPositionAndRotation(
            activeCheckpoint.SpawnPosition,
            activeCheckpoint.SpawnRotation);
    }
}
