using System.Collections;
using UnityEngine;
using Photon.Pun;
using static UnityEngine.Mathf;

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

    private bool _isTouchingWall;
    private bool _isJumpQueued;

    private InputSystem_Actions _playerInputActions;

    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float verticalSpeed;
    private Vector3 _previousPlayerScale;

    private static readonly int IsLaying = Animator.StringToHash("IsLaying");
    private float _idleTimer;

    public bool IsStanding { get; set; }
    public bool IsGrounded { get; private set; }
    public bool IsJumpPaused { get; set; }

    private Camera _mainCamera;

    public bool IsPaused { get; set; }
    public bool HasFinished { get; set; }

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
        _mainCamera = Camera.main;
        _rb = GetComponent<Rigidbody2D>();

        // Canvas Event Camera setup
        var playerCanvas = GetComponentInChildren<Canvas>();
        if (_mainCamera != null) playerCanvas.worldCamera = _mainCamera;

        if (!photonView.IsMine) return;
        // Camera setup
        if (_mainCamera == null) return;
        _mainCamera.transform.SetParent(transform);
        _mainCamera.transform.localPosition = new Vector3(0, 0, -10);
        _mainCamera.transform.localRotation = Quaternion.identity;
    }

    private void Update()
    {
        UpdateAnimations();

        if (!photonView.IsMine) return;
        verticalSpeed = _rb.linearVelocity.y;
        CheckIdleState();
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (IsPaused) return;

        // return if player's collider is touching a wall or is jump queued
        _isTouchingWall = Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, wallLayerMask);
        if (_isJumpQueued) return;
        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;

        #region handle input
        // Queue the jump
        if (_playerInputActions.Player.Jump.WasPressedThisFrame() && IsGrounded && !_isJumpQueued)
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
                animator.SetBool(IsLaying, false);
                _idleTimer = 0f;
                break;

            // Moving left
            case < 0 when _isTouchingWall && transform.localScale.x < 0 && !_isJumpQueued:
                return;
            case < 0:
                transform.localScale = new Vector3(transform.localScale.y * -1, transform.localScale.y, transform.localScale.z);
                playerNameTag.transform.localScale = new Vector3(playerNameTag.transform.localScale.y * (-1), playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
                animator.SetBool(IsLaying, false);
                _idleTimer = 0f;
                break;
        }

        // Flip the camera
        if (transform.localScale != _previousPlayerScale)
            _mainCamera.transform.localPosition = new Vector3(_mainCamera.transform.localPosition.x * -1, _mainCamera.transform.localPosition.y, _mainCamera.transform.localPosition.z);
        _previousPlayerScale = transform.localScale;
        #endregion

        // If laying down and not finished standing up, don't process movement
        if (!IsStanding) return;

        #region handle movement
        // Gradual acceleration
        if (Abs(horizontalInput) > 0.01f)
        {
            currentSpeed = MoveTowards(currentSpeed, horizontalInput * maxSpeed, acceleration * Time.deltaTime);
        }
        else
        {
            // Deceleration with a slight threshold to avoid sticking at 0
            currentSpeed = MoveTowards(currentSpeed, 0, deceleration * Time.deltaTime);
            if (Abs(currentSpeed) < 0.01f) currentSpeed = 0; // Prevent floating-point issues
        }

        // Preserve y velocity and apply movement
        var targetVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
        if (!_isTouchingWall) _rb.linearVelocity = targetVelocity;

        // Do the jump
        if (!_isJumpQueued) return;
        animator.SetBool(IsJumpQueued, true);
        StartCoroutine(JumpAfterDelay(0.2f)); // Trigger the jump
        #endregion
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

    public void Teleport(Vector3 position)
    {
        if (photonView.IsMine)
        {
            transform.position = position;
        }
    }

    public void SetMovement(bool isEnabled)
    {
        // Implement your movement enable/disable logic here
        IsPaused = !isEnabled;
    }

    #region Animations
    private void CheckIdleState()
    {
        if (Approximately(currentSpeed, 0f) && Approximately(verticalSpeed, 0f) && animator.GetBool(IsLaying) == false)
        {
            _idleTimer += Time.deltaTime;
            if (!(_idleTimer >= 3f)) return;
            animator.SetBool(IsLaying, true);
            Debug.Log("Laying down");
            _idleTimer = 3f;
        }
        else
        {
            _idleTimer = 0f;
        }
    }
    private void UpdateAnimations()
    {
        // Ground check
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayerMask);

        // Landing animation
        if (IsGrounded && IsJumpPaused)
        {
            animator.speed = 1f;
            IsJumpPaused = false;
        }

        if (photonView.IsMine)
        {
            // Update InAir anim parameter
            animator.SetBool(InAir, !IsGrounded);
            // Update Speed anim parameter (using absolute horizontal velocity)
            animator.SetFloat(Speed, Abs(_rb.linearVelocity.x));
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
    #endregion

    // spectator mode
    public void SetSpectatorMode(bool isEnabled)
    {
        if (isEnabled)
        {
            Debug.Log("Spectator mode enabled");
            // TODO: hide UI elements
        }
        else
        {
            Debug.Log("Spectator mode disabled");
        }
    }
}
