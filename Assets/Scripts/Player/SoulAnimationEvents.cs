using UnityEngine;

// Attach to the same GameObject as the soul's Animator (the model child of the soul prefab).
// Mirrors PlayerAnimationEvents but for the soul — jump events are stubs since the soul
// doesn't jump, but the methods must exist so Unity doesn't log "missing method" warnings
// when the shared animation clips fire their events on the soul's Animator.
public class SoulAnimationEvents : MonoBehaviour
{
    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    // --- Jump.anim events (soul doesn't jump — stubs prevent Unity warnings) ---
    public void OnJumpLaunch() { }
    public void OnJumpApex()   { }

    // --- Landing.anim ---
    // Place on the impact frame — hook up sound/VFX here when ready
    public void OnLandImpact() { }

    // --- Landing.anim ---
    // Place on the recovery frame — fires LandComplete to exit the Landing state
    public void OnLandingComplete()
    {
        animator.SetTrigger("LandComplete");
    }
}
