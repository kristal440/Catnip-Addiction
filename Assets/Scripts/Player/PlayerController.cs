using System.Collections;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Mathf;
using static UnityEngine.Vector2;

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
    public float jumpCooldown = 0.1f;

    [Header("Death")]
    public float deathHeight = -100f;
    [SerializeField] private PlayerDeathHandler playerDeathHandler;

    // Component references
    private Camera _mainCamera;
    private Rigidbody2D _rb;
    private DynamicCameraController _cameraController;
    private InputSystem_Actions _playerInputActions;
    private CatnipFx _catnipFx;

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
    private float _lastJumpTime;

    // Jump buffering
    private bool _isBufferingJump;
    private float _bufferedJumpChargeLevel;
    private bool _bufferedJumpMaxCharged;
    private float _bufferedJumpStartTime;

    // Catnip effects
    private float _newJumpForce;
    private float _newMaxSpeed;
    private float _newDeceleration;

    // Properties
    internal bool IsStanding { get; set; }
    internal bool IsGrounded { get; private set; }
    internal bool IsJumpPaused { get; set; }
    internal bool IsPaused { get; set; }
    internal bool HasCatnip { get; set; }
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
        _catnipFx = GetComponent<CatnipFx>();

        var playerCanvas = GetComponentInChildren<Canvas>();
        if (_mainCamera != null)
            playerCanvas.worldCamera = _mainCamera;

        var sr = GetComponentInChildren<SpriteRenderer>();
        var nameTagText = GetComponentInChildren<TextMeshProUGUI>();

        if (!photonView.IsMine)
            SetupRemotePlayerVisuals(sr, nameTagText, playerCanvas);
        else
            SetupLocalPlayerCamera();
    }

    private void Update()
    {
        UpdateAnimations();

        if (!photonView.IsMine)
            return;

        verticalSpeed = _rb.linearVelocity.y;
        CheckIdleState();
        HandleMovement();

        // Update jump charging for both ground and air
        UpdateJumpCharging();

        if (transform.position.y < deathHeight && !_isDead)
            playerDeathHandler.HandleOutOfBoundsDeath();
    }
    #endregion

    #region Player Setup
    private static void SetupRemotePlayerVisuals(SpriteRenderer sr, Graphic nameTagText, Canvas playerCanvas)
    {
        if (sr != null)
        {
            var c = sr.color;
            c.a = 0.7f;
            sr.color = c;
            sr.sortingOrder = 2;
        }
        else
        {
            Debug.LogWarning("No SpriteRenderer found on player GameObject!");
        }

        if (nameTagText != null)
        {
            var textColor = nameTagText.color;
            textColor.a = 0.7f;
            nameTagText.color = textColor;
        }
        else
        {
            Debug.LogWarning("No TextMeshProUGUI found on player GameObject!");
        }

        if (playerCanvas != null)
            playerCanvas.sortingOrder = 3;
        else
            Debug.LogWarning("No Canvas found on player GameObject!");
    }

    private void SetupLocalPlayerCamera()
    {
        if (_mainCamera == null)
            return;

        Transform transform1;
        (transform1 = _mainCamera.transform).SetParent(transform);
        transform1.localPosition = new Vector3(0, 0, -10);
        transform1.localRotation = Quaternion.identity;
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

        if (!(horizontalInput > 0))
        {
            if (horizontalInput < 0)
            {
                var localScale = transform.localScale;
                localScale = new Vector3(-Abs(localScale.y), localScale.y,
                    localScale.z);
                transform.localScale = localScale;
                var scale = playerNameTag.transform.localScale;
                scale = new Vector3(-Abs(scale.y),
                    scale.y, scale.z);
                playerNameTag.transform.localScale = scale;
                var localScale1 = jumpChargeBarGameObject.transform.localScale;
                localScale1 = new Vector3(
                    -Abs(localScale1.y),
                    localScale1.y, localScale1.z);
                jumpChargeBarGameObject.transform.localScale = localScale1;
                animator.SetBool(IsLaying, false);
                _idleTimer = 0f;
            }
        }
        else
        {
            var localScale = transform.localScale;
            localScale =
                new Vector3(Abs(localScale.y), localScale.y, localScale.z);
            transform.localScale = localScale;
            var scale = playerNameTag.transform.localScale;
            scale = new Vector3(Abs(scale.y),
                scale.y, scale.z);
            playerNameTag.transform.localScale = scale;
            var localScale1 = jumpChargeBarGameObject.transform.localScale;
            localScale1 = new Vector3(
                Abs(localScale1.y), localScale1.y,
                localScale1.z);
            jumpChargeBarGameObject.transform.localScale = localScale1;
            animator.SetBool(IsLaying, false);
            _idleTimer = 0f;
        }

        if (transform.localScale != _previousPlayerScale)
        {
            var transform1 = _mainCamera.transform;
            var cameraPosition = transform1.localPosition;
            cameraPosition.x *= -1;
            transform1.localPosition = cameraPosition;
        }

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

        switch (Abs(horizontalInput))
        {
            case > 0.01f when !movingIntoWall:
                currentSpeed = MoveTowards(currentSpeed, horizontalInput * _newMaxSpeed, acceleration * Time.deltaTime);
                break;
            case <= 0.01f:
            {
                currentSpeed = MoveTowards(currentSpeed, 0, _newDeceleration * Time.deltaTime);
                if (Abs(currentSpeed) < 0.01f)
                    currentSpeed = 0;
                break;
            }
        }

        _rb.linearVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
    }
    #endregion

    #region Jump
    private void HandleJumpInput()
    {
        var jumpOnCooldown = Time.time < _lastJumpTime + jumpCooldown;

        // Start jump charge (either grounded or buffered)
        if (_playerInputActions.Player.Jump.WasPressedThisFrame() &&
            !_isJumpQueued &&
            IsStanding &&
            !jumpOnCooldown)
        {
            _jumpButtonHeld = true;
            _jumpChargeStartTime = Time.time;

            if (IsGrounded)
            {
                // Regular ground jump - show animation
                _isChargingJump = true;
                _movementDisabledForJump = true;
                animator.SetBool(IsJumpQueued, true);
                jumpChargeBarGameObject.SetActive(true);
            }
            else
            {
                // Buffered jump - same behavior but without animation
                _isBufferingJump = true;
                jumpChargeBarGameObject.SetActive(true);
                // Don't set animation or disable movement yet
            }
        }

        // Handle jump button release
        if (!_playerInputActions.Player.Jump.WasReleasedThisFrame() || !_jumpButtonHeld)
            return;

        _jumpButtonHeld = false;

        // Execute jump if charging on ground
        if (_isChargingJump)
        {
            var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
            ExecuteJump(chargeTime);
            return;
        }

        // For buffered jump, save charge level for landing
        if (_isBufferingJump)
        {
            var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
            _bufferedJumpChargeLevel = chargeTime;

            if (chargeTime >= maxChargeTime)
            {
                _bufferedJumpMaxCharged = true;
            }
        }
    }

    // Update jump charging in Update method for better responsiveness
    private void UpdateJumpCharging()
    {
        if (!_jumpButtonHeld) return;

        bool isCharging = _isChargingJump || _isBufferingJump;
        if (!isCharging) return;

        // Calculate charge progress identically for both charging methods
        var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
        var chargeProgress = Clamp01(chargeTime / maxChargeTime);

        // Update UI
        jumpChargeBar.fillAmount = chargeProgress;

        // Update FOV
        if (_cameraController)
            _cameraController.UpdateChargingJumpFOV(chargeProgress);

        // Check for max charge
        if (chargeTime >= maxChargeTime)
        {
            if (_isBufferingJump)
            {
                _bufferedJumpMaxCharged = true;
                _bufferedJumpChargeLevel = maxChargeTime;
            }
            else if (_isChargingJump && IsGrounded)
            {
                // Auto-jump when max charged on ground
                ExecuteJump(maxChargeTime);
                _jumpButtonHeld = false;
            }
        }
    }

    // Handle transition from buffered to normal jumping on landing
    private void CheckBufferedJumpLanding(bool wasGrounded, bool isGroundedNow)
    {
        // If just landed and have a buffered jump
        if (!wasGrounded && isGroundedNow && _isBufferingJump)
        {
            // Clear buffering state
            _isBufferingJump = false;

            if (!_jumpButtonHeld && _bufferedJumpChargeLevel > 0)
            {
                // Button already released - execute immediately
                var chargeToUse = _bufferedJumpMaxCharged ? maxChargeTime : _bufferedJumpChargeLevel;

                // Set animation state just before jumping
                animator.SetBool(IsJumpQueued, true);

                // Execute jump immediately without delay
                ExecuteJump(chargeToUse);
            }
            else if (_jumpButtonHeld)
            {
                // Button still held - transition to normal charging with animation
                _isChargingJump = true;
                _movementDisabledForJump = true;
                animator.SetBool(IsJumpQueued, true);

                // Continue charging from where buffer left off - no need to adjust time
            }
            else
            {
                // No valid jump
                jumpChargeBarGameObject.SetActive(false);
            }
        }
    }

    private void ExecuteJump(float chargeTime)
    {
        // Reset all jump states
        _isChargingJump = false;
        _movementDisabledForJump = false;
        _isBufferingJump = false;
        jumpChargeBarGameObject.SetActive(false);

        var chargeProgress = Clamp01(chargeTime / maxChargeTime);
        var jumpMultiplier = Lerp(minJumpForce, maxJumpForce, chargeProgress);

        if (HasCatnip)
            jumpMultiplier *= 1.1f;

        if (_cameraController)
            _cameraController.TriggerJumpFOV();

        if (IsGrounded)
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpMultiplier);
            _lastJumpTime = Time.time;
        }

        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;
    }

    private IEnumerator JumpAfterDelay(float delay, float chargeLevel = 0f)
    {
        animator.speed = 0f;
        yield return new WaitForSeconds(delay);

        float jumpForceToApply;

        if (chargeLevel > 0)
        {
            // Use the buffered charge level
            var chargeProgress = Clamp01(chargeLevel / maxChargeTime);
            jumpForceToApply = Lerp(minJumpForce, maxJumpForce, chargeProgress);
        }
        else
        {
            // Use default quick jump force
            jumpForceToApply = minJumpForce;
        }

        if (HasCatnip)
            jumpForceToApply *= 1.1f;

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpForceToApply);
        _lastJumpTime = Time.time;

        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;
        animator.speed = 1f;

        // Trigger jump FOV effect
        if (_cameraController)
            _cameraController.TriggerJumpFOV();
    }

    private void CancelJumpCharge()
    {
        _isChargingJump = false;
        _movementDisabledForJump = false;
        _jumpButtonHeld = false;

        // Don't cancel buffered jump UI when cancelling a ground charge
        if (!_isBufferingJump)
        {
            jumpChargeBarGameObject.SetActive(false);
        }

        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;
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
        {
            _idleTimer = 0f;
        }
    }

    private void UpdateAnimations()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayerMask) || _isDead;

        // Check if we just landed with a buffered jump
        if (wasGrounded != IsGrounded)
        {
            CheckBufferedJumpLanding(wasGrounded, IsGrounded);
        }

        // Cancel ground charging if in air
        if (!IsGrounded && (_isChargingJump || _isJumpQueued))
            CancelJumpCharge();

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
            var localScale = playerNameTag.transform.localScale;
            localScale = new Vector3(
                scaleFactor * Abs(localScale.y),
                localScale.y,
                localScale.z);
            playerNameTag.transform.localScale = localScale;

            if (!jumpChargeBarGameObject) return;

            var scale = jumpChargeBarGameObject.transform.localScale;
            scale = new Vector3(
                scaleFactor * Abs(scale.y),
                scale.y,
                scale.z);
            jumpChargeBarGameObject.transform.localScale = scale;
        }
    }
    #endregion

    #region Utility

    internal void Teleport(Vector3 position)
    {
        if (!photonView.IsMine)
            return;

        transform.position = position;
        currentSpeed = 0f;
    }

    internal void SetMovement(bool isEnabled)
    {
        IsPaused = !isEnabled;
    }

    internal void DisableRigidbody()
    {
        if (_rb)
            _rb.simulated = false;
    }

    internal void EnableRigidbody()
    {
        if (_rb)
            _rb.simulated = true;
    }

    internal void SetSpectatorMode(bool isEnabled)
    {
        SetMovement(false);
        Debug.Log(isEnabled ? "Spectator mode enabled" : "Spectator mode disabled");
    }
    [PunRPC]
    internal void RPC_SetCatnipEffectActive(bool isActive)
    {
        HasCatnip = isActive;

        if (!photonView.IsMine) return;

        if (isActive)
            _catnipFx.ActivateCatnipEffect();
        else
            _catnipFx.DeactivateCatnipEffect();
    }
    #endregion

    #region Death and Respawn

    internal void OnPlayerDeath()
    {
        if (_isDead)
            return;

        _isDead = true;
        _originalGravityScale = _rb.gravityScale;
        _rb.gravityScale = 0;
        _rb.linearVelocity = zero;
        _rb.constraints = RigidbodyConstraints2D.FreezeAll;

        if (_cameraController)
            _cameraController.OnPlayerDeath();
    }

    internal void RespawnAtLastCheckpoint()
    {
        Teleport(CheckpointManager.LastCheckpointPosition);
        OnPlayerRespawn();
    }

    private void OnPlayerRespawn()
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
