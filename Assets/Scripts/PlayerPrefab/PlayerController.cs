using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Mathf;

public class PlayerController : MonoBehaviourPunCallbacks
{
    #region Variables
    [Header("Animation")]
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int InAir = Animator.StringToHash("InAir");
    private static readonly int IsJumpQueued = Animator.StringToHash("IsJumpQueued");
    private static readonly int IsLaying = Animator.StringToHash("IsLaying");
    public Animator animator;

    [Header("Movement")]
    public float acceleration = 10f;
    public float deceleration = 15f;
    public float maxSpeed = 5f;
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float verticalSpeed;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayerMask;

    [Header("Wall Detection")]
    public Transform wallCheck;
    public float wallCheckRadius = 0.2f;
    public LayerMask wallLayerMask;

    [Header("UI")]
    public TextMeshProUGUI playerNameTag;
    public GameObject jumpChargeBarGameObject;
    public Image jumpChargeBar;

    [Header("Charged Jump")]
    public float minJumpForce = 8.5f;
    public float maxJumpForce = 14.5f;
    public float quickJumpThreshold = 0.2f;
    public float maxChargeTime = 2f;

    // Component references
    private Camera _mainCamera;
    private Rigidbody2D _rb;
    private DynamicCameraController _cameraController;
    private InputSystem_Actions _playerInputActions;

    // State tracking
    private bool _isChargingJump;
    private float _jumpChargeStartTime;
    private bool _jumpButtonHeld;
    private bool _movementDisabledForJump;
    private bool _isTouchingWall;
    private bool _isJumpQueued;
    private bool _isFalling;
    private bool _jump1;
    private float _idleTimer;
    private Vector3 _previousPlayerScale;
    private float _originalGravityScale;
    private bool _isDead;
    private bool _wallCollisionHandled;

    // Catnip effects
    private float _newJumpForce;
    private float _newMaxSpeed;
    private float _newDeceleration;

    // Properties
    public bool IsStanding { get; set; }
    public bool IsGrounded { get; private set; }
    public bool IsJumpPaused { get; set; }
    public bool IsPaused { get; set; }
    public bool HasCatnip { get; set; }
    #endregion

    #region Unity Lifecycle
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

        var playerCanvas = GetComponentInChildren<Canvas>();
        if (_mainCamera != null)
            playerCanvas.worldCamera = _mainCamera;

        var sr = GetComponent<SpriteRenderer>();
        var nameTagText = GetComponentInChildren<TextMeshProUGUI>();

        if (!photonView.IsMine)
        {
            SetupRemotePlayerVisuals(sr, nameTagText, playerCanvas);
        }
        else
        {
            SetupLocalPlayerCamera();
        }
    }

    private void Update()
    {
        UpdateAnimations();

        if (!photonView.IsMine)
            return;

        verticalSpeed = _rb.linearVelocity.y;
        CheckIdleState();
        HandleMovement();
    }
    #endregion

    #region Player Setup
    private static void SetupRemotePlayerVisuals(SpriteRenderer sr, TextMeshProUGUI nameTagText, Canvas playerCanvas)
    {
        if (sr != null)
        {
            var c = sr.color;
            c.a = 0.7f;
            sr.color = c;
            sr.sortingOrder = 0;
        }
        else
            Debug.LogWarning("No SpriteRenderer found on player GameObject!");

        if (nameTagText != null)
        {
            var textColor = nameTagText.color;
            textColor.a = 0.7f;
            nameTagText.color = textColor;
        }
        else
            Debug.LogWarning("No TextMeshProUGUI found on player GameObject!");

        if (playerCanvas != null)
            playerCanvas.sortingOrder = 0;
        else
            Debug.LogWarning("No Canvas found on player GameObject!");
    }

    private void SetupLocalPlayerCamera()
    {
        if (_mainCamera == null)
            return;

        _mainCamera.transform.SetParent(transform);
        _mainCamera.transform.localPosition = new Vector3(0, 0, -10);
        _mainCamera.transform.localRotation = Quaternion.identity;
        _cameraController = GetComponentInChildren<DynamicCameraController>();
    }
    #endregion

    #region Movement
    private void HandleMovement()
    {
        if (IsPaused)
            return;

        // Update wall contact state
        _isTouchingWall = Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, wallLayerMask);

        // Reset wall handling flag when not touching wall
        if (!_isTouchingWall)
            _wallCollisionHandled = false;

        HandleJumpInput();

        if (_movementDisabledForJump)
            return;

        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
        HandlePlayerDirection(horizontalInput);

        if (!IsStanding)
            return;

        UpdatePlayerSpeed(horizontalInput);
    }

    private void HandlePlayerDirection(float horizontalInput)
    {
        var facingRight = transform.localScale.x > 0;
        var movingIntoWall = _isTouchingWall &&
                             ((facingRight && horizontalInput > 0) ||
                              (!facingRight && horizontalInput < 0));

        // Skip direction change if pushing into wall (unless jumping)
        if (movingIntoWall && !_isJumpQueued)
            return;

        if (horizontalInput > 0)
        {
            transform.localScale = new Vector3(Abs(transform.localScale.y), transform.localScale.y, transform.localScale.z);
            playerNameTag.transform.localScale = new Vector3(Abs(playerNameTag.transform.localScale.y), playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
            jumpChargeBarGameObject.transform.localScale = new Vector3(Abs(jumpChargeBarGameObject.transform.localScale.y), jumpChargeBarGameObject.transform.localScale.y, jumpChargeBarGameObject.transform.localScale.z);
            animator.SetBool(IsLaying, false);
            _idleTimer = 0f;
        }
        else if (horizontalInput < 0)
        {
            transform.localScale = new Vector3(-Abs(transform.localScale.y), transform.localScale.y, transform.localScale.z);
            playerNameTag.transform.localScale = new Vector3(-Abs(playerNameTag.transform.localScale.y), playerNameTag.transform.localScale.y, playerNameTag.transform.localScale.z);
            jumpChargeBarGameObject.transform.localScale = new Vector3(-Abs(jumpChargeBarGameObject.transform.localScale.y), jumpChargeBarGameObject.transform.localScale.y, jumpChargeBarGameObject.transform.localScale.z);
            animator.SetBool(IsLaying, false);
            _idleTimer = 0f;
        }

        if (transform.localScale != _previousPlayerScale)
            _mainCamera.transform.localPosition = new Vector3(_mainCamera.transform.localPosition.x * -1, _mainCamera.transform.localPosition.y, _mainCamera.transform.localPosition.z);

        _previousPlayerScale = transform.localScale;
    }

    private void UpdatePlayerSpeed(float horizontalInput)
    {
        _newMaxSpeed = HasCatnip ? maxSpeed * 1.1f : maxSpeed;
        _newDeceleration = HasCatnip ? deceleration * 0.9f : deceleration;

        var facingRight = transform.localScale.x > 0;
        var movingIntoWall = _isTouchingWall &&
                             ((facingRight && horizontalInput > 0) ||
                              (!facingRight && horizontalInput < 0));

        // Stop momentum on first wall contact
        if (movingIntoWall && !_wallCollisionHandled)
        {
            currentSpeed = 0;
            _wallCollisionHandled = true;
        }

        // Normal movement when not pushing into wall
        if (Abs(horizontalInput) > 0.01f && !movingIntoWall)
        {
            currentSpeed = MoveTowards(currentSpeed, horizontalInput * _newMaxSpeed, acceleration * Time.deltaTime);
        }
        else if (Abs(horizontalInput) <= 0.01f)
        {
            currentSpeed = MoveTowards(currentSpeed, 0, _newDeceleration * Time.deltaTime);
            if (Abs(currentSpeed) < 0.01f)
                currentSpeed = 0;
        }

        // Apply velocity
        _rb.linearVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
    }
    #endregion

    #region Jump
    private void HandleJumpInput()
    {
        if (_playerInputActions.Player.Jump.WasPressedThisFrame() && IsGrounded && !_isJumpQueued && IsStanding)
        {
            _jumpButtonHeld = true;
            _jumpChargeStartTime = Time.time;
            StartCoroutine(ChargeJump());
        }

        if (!_playerInputActions.Player.Jump.WasReleasedThisFrame() || !_jumpButtonHeld)
            return;

        _jumpButtonHeld = false;
        if (!_isChargingJump)
            return;

        var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
        ExecuteJump(chargeTime);
    }

    private IEnumerator ChargeJump()
    {
        yield return new WaitForSeconds(quickJumpThreshold);

        if (!_jumpButtonHeld)
        {
            _isJumpQueued = true;
            animator.SetBool(IsJumpQueued, true);
            StartCoroutine(JumpAfterDelay(0f));
            yield break;
        }

        _isChargingJump = true;
        _movementDisabledForJump = true;
        animator.SetBool(IsJumpQueued, true);
        jumpChargeBarGameObject.SetActive(true);

        while (_jumpButtonHeld && (Time.time - _jumpChargeStartTime) < maxChargeTime)
        {
            var chargeProgress = (Time.time - _jumpChargeStartTime) / maxChargeTime;
            jumpChargeBar.fillAmount = chargeProgress;

            if (_cameraController)
                _cameraController.UpdateChargingJumpFOV(chargeProgress);

            yield return null;
        }

        if (!_jumpButtonHeld)
            yield break;

        ExecuteJump(maxChargeTime);
        _jumpButtonHeld = false;
    }

    private void ExecuteJump(float chargeTime)
    {
        _isChargingJump = false;
        _movementDisabledForJump = false;
        jumpChargeBarGameObject.SetActive(false);

        var chargeProgress = Clamp01(chargeTime / maxChargeTime);
        var jumpMultiplier = Lerp(minJumpForce, maxJumpForce, chargeProgress);

        if (HasCatnip)
            jumpMultiplier *= 1.1f;

        _cameraController.TriggerJumpFOV();

        if (IsGrounded)
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpMultiplier);
        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;
    }

    private IEnumerator JumpAfterDelay(float delay)
    {
        animator.speed = 0f;
        yield return new WaitForSeconds(delay);

        var jumpForceToApply = minJumpForce;
        if (HasCatnip)
            jumpForceToApply *= 1.1f;

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForceToApply);
        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;
        animator.speed = 1f;
    }
    #endregion

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
            _idleTimer = 0f;
    }

    private void UpdateAnimations()
    {
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayerMask);

        if (IsGrounded && IsJumpPaused)
        {
            animator.speed = 1f;
            IsJumpPaused = false;
            _isFalling = false;
            _jump1 = false;
        }

        if (!_jump1 && _rb.linearVelocity.y is < 3f and > 2.5f)
        {
            animator.speed = 1f;
            _jump1 = true;
        }

        if (_jump1 && !_isFalling && _rb.linearVelocity.y < -0.5f)
        {
            animator.speed = 1f;
            _isFalling = true;
        }

        if (photonView.IsMine)
        {
            animator.SetBool(InAir, !IsGrounded);
            animator.SetFloat(Speed, Abs(_rb.linearVelocity.x));
        }
        else
        {
            // Handle remote player's UI elements orientation
            var scaleFactor = transform.localScale.x < 0 ? -1 : 1;
            playerNameTag.transform.localScale = new Vector3(
                scaleFactor * Abs(playerNameTag.transform.localScale.y),
                playerNameTag.transform.localScale.y,
                playerNameTag.transform.localScale.z);

            if (jumpChargeBarGameObject)
                jumpChargeBarGameObject.transform.localScale = new Vector3(
                    scaleFactor * Abs(jumpChargeBarGameObject.transform.localScale.y),
                    jumpChargeBarGameObject.transform.localScale.y,
                    jumpChargeBarGameObject.transform.localScale.z);
        }
    }
    #endregion

    #region Utility
    public void Teleport(Vector3 position)
    {
        if (!photonView.IsMine)
            return;

        transform.position = position;
        currentSpeed = 0f;
    }

    public void SetMovement(bool isEnabled)
    {
        IsPaused = !isEnabled;
    }

    public void SetSpectatorMode(bool isEnabled)
    {
        SetMovement(false);
        Debug.Log(isEnabled ? "Spectator mode enabled" : "Spectator mode disabled");
    }
    #endregion

    #region Death and Respawn
    public void OnPlayerDeath()
    {
        if (_isDead)
            return;

        _isDead = true;
        _originalGravityScale = _rb.gravityScale;
        _rb.gravityScale = 0;
        _rb.linearVelocity = Vector2.zero;
        _rb.constraints = RigidbodyConstraints2D.FreezeAll;

        if (_cameraController)
            _cameraController.OnPlayerDeath();
    }

    public void OnPlayerRespawn()
    {
        if (!_isDead)
            return;

        _isDead = false;
        _rb.gravityScale = _originalGravityScale;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (_cameraController)
            _cameraController.OnPlayerRespawn();
    }
    #endregion
}