using System.Collections;
using UnityEngine;
using Photon.Pun;
using UnityEngine.UI; // Add this line to use Canvas

public class PlayerController : MonoBehaviourPunCallbacks
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int InAir = Animator.StringToHash("InAir");
    private static readonly int IsJumpQueued = Animator.StringToHash("IsJumpQueued");
    private Rigidbody2D _rb;
    public float maxSpeed = 5f; // Maximum horizontal movement speed
    public float acceleration = 10f; // How fast the player reaches the target speed
    public float deceleration = 15f; // How fast the player stops
    public float jumpForce = 10f;
    public Animator animator;

    [Header("Ground Check")]
    public Transform groundCheck; // Transform to mark the ground check position
    public float groundCheckRadius = 0.2f; // Radius of the ground check circle
    public LayerMask groundLayerMask; // Layer mask for ground detection

    [Header("Dont flip")]
    public TMPro.TextMeshProUGUI playerNameTag; //

    private bool _isGrounded;
    private bool _isJumpQueued;
    private bool _isJumpPaused;

    // Reference to the generated Input Action class
    private InputSystem_Actions _playerInputActions;

    private float _currentSpeed;

    private void Awake()
    {
        _playerInputActions = new InputSystem_Actions(); // Instantiate the input actions
    }

    public override void OnEnable()
    {
        _playerInputActions.Player.Enable(); // Enable the Player action map
    }

    public override void OnDisable()
    {
        _playerInputActions.Player.Disable(); // Disable the Player action map
    }

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        var mainCamera = Camera.main; // Store Camera.main in a variable to avoid multiple lookups

        // Canvas Event Camera setup
        var playerCanvas = GetComponentInChildren<Canvas>(); // Get the Canvas component from children
        if (playerCanvas != null)
        {
            playerCanvas.worldCamera = mainCamera; // Assign the main camera as the Event Camera
        }
        else
        {
            Debug.LogWarning("Canvas not found in Player prefab. Name Tag might not work correctly.");
        }

        if (!photonView.IsMine) return;
        // Camera setup
        if (Camera.main == null) return;
        mainCamera.transform.SetParent(transform);
        mainCamera.transform.localPosition = new Vector3(0, 0, -10);
        mainCamera.transform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        UpdateAnimations();

        if (!photonView.IsMine) return;
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (_isJumpQueued) return;
            var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;

            if (horizontalInput > 0) // Moving right
            {
                transform.localScale = new Vector3((transform.localScale.y * 2) / 2, transform.localScale.y, transform.localScale.z);
                playerNameTag.transform.localScale = new Vector3((playerNameTag.transform.localScale.y * 2) / 2, playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
            }
            else if (horizontalInput < 0) // Moving left
            {
                transform.localScale = new Vector3(transform.localScale.y * (-1), transform.localScale.y, transform.localScale.z);
                playerNameTag.transform.localScale = new Vector3(playerNameTag.transform.localScale.y * (-1), playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
            }

            // Gradual acceleration
            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, horizontalInput * maxSpeed, acceleration * Time.deltaTime);
            }
            else
            {
                // Deceleration with a slight threshold to avoid sticking at 0
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0, deceleration * Time.deltaTime);
                if (Mathf.Abs(_currentSpeed) < 0.01f) _currentSpeed = 0; // Prevent floating-point issues
            }

            // Preserve y velocity and apply movement
            var targetVelocity = new Vector2(_currentSpeed, _rb.linearVelocity.y);
            _rb.linearVelocity = targetVelocity;

            // Jump logic
            if (_playerInputActions.Player.Jump.WasPressedThisFrame() && _isGrounded && !_isJumpQueued)
            {
                animator.SetBool(IsJumpQueued, true);
                _isJumpQueued = true;

                StartCoroutine(JumpAfterDelay(0.1f)); // Trigger the jump
            }
    }

    private IEnumerator JumpAfterDelay(float delay)
    {
        animator.speed = 0f; // Pause animation - Keep this for the slight pause before jump force
        yield return new WaitForSeconds(delay);

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce); // Perform the jump

        // Reset jump queue
        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;
        Debug.Log("Jump Queue Reset by Coroutine");

        animator.speed = 1f; // Resume animation
        Debug.Log("Jump performed");
    }

    private void UpdateAnimations()
    {
        // Ground check
        _isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayerMask);

        // Landing animation
        if (_isGrounded && _isJumpPaused)
        {
            animator.speed = 1f;
            _isJumpPaused = false;
            Debug.Log("Jump resumed");
        }

        if (photonView.IsMine)
        {
            // Update InAir parameter for jump animation
            animator.SetBool(InAir, !_isGrounded);
            // Update Speed parameter for run animation (using absolute horizontal velocity)
            animator.SetFloat(Speed, Mathf.Abs(_rb.linearVelocity.x));
        }
        else
        {
            // Flip the name tag if the player is moving left
            if (transform.localScale.x < 0)
                playerNameTag.transform.localScale = new Vector3(playerNameTag.transform.localScale.y * (-1), playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
            else
                playerNameTag.transform.localScale = new Vector3((playerNameTag.transform.localScale.y * 2) / 2, playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
        }
    }

    // Animation event: gets called when the player is midair
    public void PauseJump()
    {
        // Only pause if still in the air
        if (_isGrounded) return;
        animator.speed = 0f;
        _isJumpPaused = true;
        Debug.Log("Jump paused");
    }
}
