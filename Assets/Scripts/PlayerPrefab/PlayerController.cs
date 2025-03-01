using System.Collections;
using UnityEngine;
using Photon.Pun;
using static UnityEngine.Mathf;
using TMPro;
using UnityEngine.UI;

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
    public TextMeshProUGUI playerNameTag;

    [Header("Charged Jump")]
    public float minJumpForce = 8.5f;
    public float maxJumpForce = 14.5f;
    public float quickJumpThreshold = 0.2f;
    public float maxChargeTime = 2f;
    public GameObject jumpChargeBarGameObject;
    public Image jumpChargeBar;

    private bool _isChargingJump;
    private float _jumpChargeStartTime;
    private bool _jumpButtonHeld;
    private bool _movementDisabledForJump;

    private bool _isTouchingWall;
    private bool _isJumpQueued;
    private bool _isFalling;
    private bool _jump1;

    private InputSystem_Actions _playerInputActions;

    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float verticalSpeed;
    private Vector3 _previousPlayerScale;

    private static readonly int IsLaying = Animator.StringToHash("IsLaying");
    private float _idleTimer;

    public bool IsStanding { get; set; }
    public bool IsGrounded { get; private set; }
    public bool IsJumpPaused { get; set; }
    public bool IsPaused { get; set; }
    public bool HasCatnip { get; set; }
    private DynamicCameraController _cameraController;

    private Camera _mainCamera;

    // Catnip effects
    private float _newJumpForce;
    private float _newMaxSpeed;
    private float _newDeceleration;

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

        var sr = GetComponent<SpriteRenderer>();
        var nameTagText = GetComponentInChildren<TextMeshProUGUI>();

        if (!photonView.IsMine)
        {
            // Make remote players more transparent
            if (sr != null)
            {
                var c = sr.color;
                c.a = 0.7f; // transparency
                sr.color = c;

                sr.sortingOrder = 0;
            }
            else
                Debug.LogWarning("No SpriteRenderer found on player GameObject!");

            // Make remote player's name tag more transparent
            if (nameTagText != null)
            {
                var textColor = nameTagText.color;
                textColor.a = 0.7f;
                nameTagText.color = textColor;
            }
            else
                Debug.LogWarning("No TextMeshProUGUI found on player GameObject!");

            // Set the Canvas sorting order to render behind the local player's canvas
            if (playerCanvas != null)
                playerCanvas.sortingOrder = 0;
            else
                Debug.LogWarning("No Canvas found on player GameObject!");
        }
        else
        {
            // Camera setup for local player
            if (_mainCamera == null) return;
            _mainCamera.transform.SetParent(transform);
            _mainCamera.transform.localPosition = new Vector3(0, 0, -10);
            _mainCamera.transform.localRotation = Quaternion.identity;
            _cameraController = GetComponentInChildren<DynamicCameraController>();
        }
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

        _isTouchingWall = Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, wallLayerMask);
        // Handle jump input
        HandleJumpInput();

        // Disable movement while charging jump
        if (_movementDisabledForJump)
            return;

        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;

        #region handle input

        switch (horizontalInput)
        {
            // Moving right
            case > 0 when _isTouchingWall && transform.localScale.x > 0 && !_isJumpQueued:
                return;
            case > 0:
                transform.localScale = new Vector3((transform.localScale.y * 2) / 2, transform.localScale.y, transform.localScale.z);
                playerNameTag.transform.localScale = new Vector3((playerNameTag.transform.localScale.y * 2) / 2, playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
                jumpChargeBarGameObject.transform.localScale = new Vector3((jumpChargeBarGameObject.transform.localScale.y * 2) / 2, jumpChargeBarGameObject.transform.localScale.y, jumpChargeBarGameObject.transform.localScale.z);
                animator.SetBool(IsLaying, false);
                _idleTimer = 0f;
                break;

            // Moving left
            case < 0 when _isTouchingWall && transform.localScale.x < 0 && !_isJumpQueued:
                return;
            case < 0:
                transform.localScale = new Vector3(transform.localScale.y * -1, transform.localScale.y, transform.localScale.z);
                playerNameTag.transform.localScale = new Vector3(playerNameTag.transform.localScale.y * (-1), playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
                jumpChargeBarGameObject.transform.localScale = new Vector3(jumpChargeBarGameObject.transform.localScale.y * (-1), jumpChargeBarGameObject.transform.localScale.y, jumpChargeBarGameObject.transform.localScale.z);
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

        if (HasCatnip) {
            _newMaxSpeed = maxSpeed * 1.2f;
            _newDeceleration = deceleration * 0.8f;
        }
        else {
            _newMaxSpeed = maxSpeed;
            _newDeceleration = deceleration;
        }

        // Gradual acceleration
        if (Abs(horizontalInput) > 0.01f)
            currentSpeed = MoveTowards(currentSpeed, horizontalInput * _newMaxSpeed, acceleration * Time.deltaTime);
        else
        {
            // Deceleration with a slight threshold to avoid sticking at 0
            currentSpeed = MoveTowards(currentSpeed, 0, _newDeceleration * Time.deltaTime);
            if (Abs(currentSpeed) < 0.01f) currentSpeed = 0; // Prevent floating-point issues
        }

        // Preserve y velocity and apply movement
        var targetVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
        if (!_isTouchingWall) _rb.linearVelocity = targetVelocity;
        #endregion
    }

    #region Jump
    private void HandleJumpInput()
    {
        // Jump button pressed
        if (_playerInputActions.Player.Jump.WasPressedThisFrame() && IsGrounded && !_isJumpQueued && IsStanding)
        {
            _jumpButtonHeld = true;
            _jumpChargeStartTime = Time.time;
            StartCoroutine(ChargeJump());
        }

        // Jump button released
        if (!_playerInputActions.Player.Jump.WasReleasedThisFrame() || !_jumpButtonHeld) return;
        _jumpButtonHeld = false;
        if (!_isChargingJump) return;
        var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
        ExecuteJump(chargeTime);
    }

    private IEnumerator ChargeJump()
    {
        yield return new WaitForSeconds(quickJumpThreshold);

        // If button was released before threshold, execute normal jump in JumpAfterDelay
        if (!_jumpButtonHeld)
        {
            _isJumpQueued = true;
            animator.SetBool(IsJumpQueued, true);
            StartCoroutine(JumpAfterDelay(0f));
            yield break;
        }

        // Otherwise start charging
        _isChargingJump = true;
        _movementDisabledForJump = true;
        animator.SetBool(IsJumpQueued, true);

        // Show and charge bar
        jumpChargeBarGameObject.SetActive(true);

        // Charging loop
        while (_jumpButtonHeld && (Time.time - _jumpChargeStartTime) < maxChargeTime)
        {
            var chargeProgress = (Time.time - _jumpChargeStartTime) / maxChargeTime;

            jumpChargeBar.fillAmount = chargeProgress;

            // Update camera FOV during charging
            if (_cameraController)
                _cameraController.UpdateChargingJumpFOV(chargeProgress);

            yield return null;
        }

        // Max charge reached, auto-jump
        if (!_jumpButtonHeld) yield break;
        ExecuteJump(maxChargeTime);
        _jumpButtonHeld = false;
    }

    private void ExecuteJump(float chargeTime)
    {
        _isChargingJump = false;
        _movementDisabledForJump = false;

        // Hide charge bar
        jumpChargeBarGameObject.SetActive(false);

        // Calculate jump force based on charge time
        var chargeProgress = Clamp01(chargeTime / maxChargeTime);
        var jumpMultiplier = Lerp(minJumpForce, maxJumpForce, chargeProgress);

        // Apply catnip effect if present
        if (HasCatnip)
            jumpMultiplier *= 1.2f;

        if (_cameraController)
            _cameraController.TriggerJumpFOV();

        // Apply jump force
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpMultiplier);

        // Reset animation flags
        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;
    }

    // Jump logic
    private IEnumerator JumpAfterDelay(float delay)
    {
        animator.speed = 0f;
        yield return new WaitForSeconds(delay);

        var jumpForceToApply = minJumpForce;
        if (HasCatnip)
            jumpForceToApply *= 1.2f;

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForceToApply);

        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;

        animator.speed = 1f;
    }
    #endregion

    public void Teleport(Vector3 position)
    {
        if (photonView.IsMine)
            transform.position = position;
    }

    public void SetMovement(bool isEnabled)
    {
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
            _isFalling = false;
            _jump1 = false;
        }

        // jump - stage 1
        if (!_jump1 && _rb.linearVelocity.y is < 3f and > 2.5f)
        {
            animator.speed = 1f;
            _jump1 = true;
        }

        // falling down after jump
        if (_jump1 && !_isFalling && _rb.linearVelocity.y < -0.5f)
        {
            animator.speed = 1f;
            _isFalling = true;
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

            // Also flip the jump charge bar
            if (jumpChargeBarGameObject)
                jumpChargeBarGameObject.transform.localScale = transform.localScale.x < 0
                    ? new Vector3(jumpChargeBarGameObject.transform.localScale.y * (-1), jumpChargeBarGameObject.transform.localScale.y,
                        jumpChargeBarGameObject.transform.localScale.z)
                    : new Vector3((jumpChargeBarGameObject.transform.localScale.y * 2) / 2, jumpChargeBarGameObject.transform.localScale.y,
                        jumpChargeBarGameObject.transform.localScale.z);
        }
    }
    #endregion

    // spectator mode
    public void SetSpectatorMode(bool isEnabled)
    {
        SetMovement(false);
        // TODO: hide UI elements
        Debug.Log(isEnabled ? "Spectator mode enabled" : "Spectator mode disabled");
    }
}
