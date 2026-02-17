using UnityEngine;

// Attach this to the same GameObject as the Animator (the model child).
// Animation Events in parkour clips call these methods by name.
public class ParkourAnimationEvents : MonoBehaviour
{
    [SerializeField] ParkourController parkourController;

    private void Awake()
    {
        if (parkourController == null)
            parkourController = GetComponentInParent<ParkourController>();
    }

    // --- LedgeClimb clip ---
    // Place at the final frame of the climb animation.
    // Completes the climb and returns control to PlayerMovement.
    public void OnLedgeClimbComplete()
    {
        parkourController.CompleteLedgeClimb();
    }
}
