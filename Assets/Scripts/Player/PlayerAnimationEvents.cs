using UnityEngine;

// Attach this to the same GameObject as the Animator (the model child).
// Animation Events in the clips call these methods by name.
public class PlayerAnimationEvents : MonoBehaviour
{
    // Drag the parent PlayerMovement here in the Inspector
    [SerializeField] PlayerMovement playerMovement;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();
    }

    // --- Jump.anim ---
    // Place this event at the frame where the feet leave the ground (end of windup).
    // This is what actually launches the character — input only starts the animation.
    public void OnJumpLaunch()
    {
        playerMovement.ApplyJumpForce();
    }

    // --- Jump.anim ---
    // Place this event at the frame where the character visually peaks
    // (feet stop rising, body starts coming down).
    // Replaces the unreliable exit-time on Jump → Falling.
    public void OnJumpApex()
    {
        animator.SetTrigger("StartFalling");
    }

    // --- Landing.anim ---
    // Place this event at the frame of actual impact (feet hit hard, body compresses).
    // Use it to trigger feedback: sound, dust VFX, camera shake, etc.
    public void OnLandImpact()
    {
        playerMovement.NotifyLandImpact();
    }

    // --- Landing.anim ---
    // Place this event at the frame where the character fully recovers upright.
    // Replaces the exit-time on Landing → Blend Tree, so locomotion
    // resumes at exactly the right visual moment.
    public void OnLandingComplete()
    {
        animator.SetTrigger("LandComplete");
    }
}
