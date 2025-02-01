using System.Collections;
using UnityEngine;
using Photon.Pun;
using UnityEngine.Serialization;

public class PlayerController : MonoBehaviourPunCallbacks
{
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int InAir = Animator.StringToHash("InAir");
    private static readonly int IsJumpQueued = Animator.StringToHash("IsJumpQueued");
    private Rigidbody2D _rb;
    public float maxSpeed = 5f;
    public float acceleration = 10f;
    public float deceleration = 15f;
    public float jumpForce = 10f;
    public Animator animator;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayerMask;

    [Header("Wall Check")]
    public Transform wallCheck;
    public float wallCheckRadius = 0.2f;
    public LayerMask wallLayerMask;

    [Header("Dont flip")]
    public TMPro.TextMeshProUGUI playerNameTag;

    private bool _isGrounded;
    private bool _isTouchingWall;
    private bool _isJumpQueued;
    private bool _isJumpPaused;

    private InputSystem_Actions _playerInputActions;

    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float verticalSpeed;

    private void Awake()
    {
        _playerInputActions = new InputSystem_Actions();
    }

    public override void OnEnable()
    {
        _playerInputActions.Player.Enable();
    }

    public override void OnDisable()
    {
        _playerInputActions.Player.Disable();
    }

    private void Start()
    {
        _rb = GetComponent<Rigidbody2D>();
        var mainCamera = Camera.main;

        // Canvas Event Camera setup
        var playerCanvas = GetComponentInChildren<Canvas>();
        if (playerCanvas != null)
        {
            if (mainCamera != null) playerCanvas.worldCamera = mainCamera;
        }
        else
        {
            Debug.LogWarning("Canvas not found in Player prefab. Name Tag won't not work correctly.");
        }

        if (!photonView.IsMine) return;
        // Camera setup
        if (mainCamera == null) return;
        mainCamera.transform.SetParent(transform);
        mainCamera.transform.localPosition = new Vector3(0, 0, -10);
        mainCamera.transform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        UpdateAnimations();

        if (!photonView.IsMine) return;
        verticalSpeed = _rb.linearVelocity.y;
        HandleMovement();
    }

    private void HandleMovement()
    {
        // return if player's collider is touching a wall or is jump queued
        _isTouchingWall = Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, wallLayerMask);
        if (_isJumpQueued) return;
        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;

        // Queue the jump
        if (_playerInputActions.Player.Jump.WasPressedThisFrame() && _isGrounded && !_isJumpQueued)
        {
            _isJumpQueued = true;
        }

        switch (horizontalInput)
        {
            // Moving right
            case > 0 when _isTouchingWall && transform.localScale.x > 0 && !_isJumpQueued:
                return;
            case > 0:
                transform.localScale = new Vector3((transform.localScale.y * 2) / 2, transform.localScale.y, transform.localScale.z);
                playerNameTag.transform.localScale = new Vector3((playerNameTag.transform.localScale.y * 2) / 2, playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
                break;

            // Moving left
            case < 0 when _isTouchingWall && transform.localScale.x < 0 && !_isJumpQueued:
                return;
            case < 0:
                transform.localScale = new Vector3(transform.localScale.y * (-1), transform.localScale.y, transform.localScale.z);
                playerNameTag.transform.localScale = new Vector3(playerNameTag.transform.localScale.y * (-1), playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
                break;
        }

        // Gradual acceleration
        if (Mathf.Abs(horizontalInput) > 0.01f)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, horizontalInput * maxSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            // Deceleration with a slight threshold to avoid sticking at 0
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0, deceleration * Time.deltaTime);
            if (Mathf.Abs(currentSpeed) < 0.01f) currentSpeed = 0; // Prevent floating-point issues
        }

        // Preserve y velocity and apply movement
        var targetVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
        if (!_isTouchingWall) _rb.linearVelocity = targetVelocity;

        // Do the jump
        if (!_isJumpQueued) return;
        animator.SetBool(IsJumpQueued, true);
        StartCoroutine(JumpAfterDelay(0.1f)); // Trigger the jump
    }

    // Jump logic
    private IEnumerator JumpAfterDelay(float delay)
    {
        animator.speed = 0f;
        yield return new WaitForSeconds(delay);

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForce);

        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;

        animator.speed = 1f;
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
        }

        if (photonView.IsMine)
        {
            // Update InAir anim parameter
            animator.SetBool(InAir, !_isGrounded);
            // Update Speed anim parameter (using absolute horizontal velocity)
            animator.SetFloat(Speed, Mathf.Abs(_rb.linearVelocity.x));
        }
        else
        {
            // Flip the name tag if the player is moving left
            playerNameTag.transform.localScale = transform.localScale.x < 0
                ? new Vector3(playerNameTag.transform.localScale.y * (-1), playerNameTag.transform.localScale.y,
                    playerNameTag.transform.localScale.z)
                : new Vector3((playerNameTag.transform.localScale.y * 2) / 2, playerNameTag.transform.localScale.y,
                    playerNameTag.transform.localScale.z);
        }
    }

    // Animation event: gets called when the player is midair
    public void PauseJump()
    {
        // Only pause if still in the air
        if (_isGrounded) return;

        animator.speed = 0f;
        _isJumpPaused = true;
    }
}
