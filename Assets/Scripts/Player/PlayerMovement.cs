using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] PlayerInput playerInput;
    private PlayerInputActions playerInputActions;
    Rigidbody rb;
    Animator animator;
    public Vector2 movementInputVector { get; private set; }

    [SerializeField] float speedMultiplier = 10f;
    [SerializeField] float maxSpeed = 10f;


    private void Awake()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Game.Enable();

        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }
    void FixedUpdate()
    {
        rb.AddForce(new Vector3(movementInputVector.x, 0f, movementInputVector.y) * speedMultiplier);
        rb.maxLinearVelocity = 10f;
        animator.SetFloat("Velocity", rb.linearVelocity.z);
        
    }
    private void OnMovement(InputValue inputValue)
    {
        movementInputVector = inputValue.Get<Vector2>();
    }
}
