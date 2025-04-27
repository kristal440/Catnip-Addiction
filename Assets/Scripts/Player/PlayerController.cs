using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Mathf;
using static UnityEngine.Vector2;

/// <inheritdoc />
/// <summary>
/// Controls player movement, jumping, animations and networking for multiplayer functionality
/// </summary>
public class PlayerController : MonoBehaviourPunCallbacks
{
    #region Variables
    [Header("Animation")]
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int InAir = Animator.StringToHash("InAir");
    private static readonly int IsJumpQueued = Animator.StringToHash("IsJumpQueued");
    private static readonly int IsLaying = Animator.StringToHash("IsLaying");
    [SerializeField] [Tooltip("Reference to the player's animator component")] public Animator animator;

    [Header("Movement")]
    [SerializeField] [Tooltip("How quickly player accelerates")] public AnimationCurve accelerationCurve = new(
        new Keyframe(0f, 0.3f),
        new Keyframe(0.6f, 0.7f),
        new Keyframe(1f, 1f)
    );
    [SerializeField] [Tooltip("Base acceleration value")] public float baseAcceleration = 10f;
    [SerializeField] [Tooltip("How long it takes to reach full acceleration")] public float accelerationTime = 0.8f;
    [SerializeField] [Tooltip("How quickly player slows down")] public float deceleration = 15f;
    [SerializeField] [Tooltip("Maximum movement speed")] public float maxSpeed = 5f;
    [SerializeField] [Tooltip("Higher speed reached after maintaining max speed")] public float turboSpeed = 7f;
    [SerializeField] [Tooltip("Time in seconds player needs to maintain max speed before reaching turbo speed")] public float timeToTurboSpeed = 1.5f;
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float verticalSpeed;

    /// Acceleration tracking
    private float _currentAccelTime;
    private float _timeAtMaxSpeed;
    private bool _isTurboActive;
    private int _lastMoveDirection;

    [Header("Ground Check")]
    [SerializeField] [Tooltip("Transform used to detect ground")] public Transform groundCheck;
    [SerializeField] [Tooltip("Radius of the ground check sphere")] public float groundCheckRadius = 0.2f;
    [SerializeField] [Tooltip("Layer mask for ground detection")] public LayerMask groundLayerMask;

    [Header("Wall Detection")]
    [SerializeField] [Tooltip("Transform used to detect walls in front")] public Transform frontWallCheck;
    [SerializeField] [Tooltip("Transform used to detect walls behind")] public Transform backWallCheck;
    [SerializeField] [Tooltip("Radius of the wall check sphere")] public float wallCheckRadius = 0.2f;
    [SerializeField] [Tooltip("Layer mask for wall detection")] public LayerMask wallLayerMask;

    [Header("UI")]
    [SerializeField] [Tooltip("Player name text display")] public TextMeshProUGUI playerNameTag;
    [SerializeField] [Tooltip("Container for jump charge bar")] public GameObject jumpChargeBarGameObject;
    [SerializeField] [Tooltip("Jump charge fill bar")] public Image jumpChargeBar;

    [Header("Charged Jump")]
    [SerializeField] [Tooltip("Minimum jump force when uncharged")] public float minJumpForce = 8.5f;
    [SerializeField] [Tooltip("Maximum jump force when fully charged")] public float maxJumpForce = 14.5f;
    [SerializeField] [Tooltip("Maximum time to charge jump")] public float maxChargeTime = 2f;
    [SerializeField] [Tooltip("Time before player can jump again")] public float jumpCooldown = 0.1f;

    [Header("Death")]
    [SerializeField] [Tooltip("Y-position that triggers death when fallen below")] public float deathHeight = -100f;
    [SerializeField] [Tooltip("Handler for player death events")] private PlayerDeathHandler playerDeathHandler;

    [Header("Visuals")]
    [SerializeField] [Tooltip("Sorting order for remote player sprites")] private int remotePlayerSpriteOrder = 2;
    [SerializeField] [Tooltip("Sorting order for remote player UI canvas")] private int remotePlayerCanvasOrder = 3;
    [SerializeField] [Tooltip("Alpha transparency for remote players (0-1)")] private float remotePlayerAlpha = 0.7f;

    /// Component references
    private Camera _mainCamera;
    private Rigidbody2D _rb;
    private DynamicCameraController _cameraController;
    private InputSystem_Actions _playerInputActions;
    private CatnipFx _catnipFx;

    /// State tracking
    private bool _isChargingJump;
    private float _jumpChargeStartTime;
    private bool _jumpButtonHeld;
    private bool _movementDisabledForJump;
    private bool _isTouchingFrontWall;
    private bool _isTouchingBackWall;
    private bool _isJumpQueued;
    private float _idleTimer;
    private Vector3 _previousPlayerScale;
    private float _originalGravityScale;
    private bool _wallCollisionHandled;
    private float _lastJumpTime;

    /// Jump buffer
    private bool _isBufferingJump;
    private float _bufferedJumpChargeLevel;
    private bool _bufferedJumpMaxCharged;
    private float _bufferedJumpStartTime;

    /// Catnip effects
    private float _newJumpForce;
    private float _newMaxSpeed;
    private float _newDeceleration;

    /// Properties
    internal bool IsStanding { get; set; }
    internal bool IsGrounded { get; private set; }
    internal bool IsJumpPaused { get; set; }
    internal bool IsPaused { get; set; }
    internal bool HasCatnip { get; set; }
    internal bool IsDead { get; private set; }

    #endregion

    #region Unity Lifecycle
    /// Initializes player input actions
    private void Awake()
    {
        _playerInputActions = new InputSystem_Actions();
    }

    /// Enables player input actions
    public override void OnEnable()
    {
        _playerInputActions.Player.Enable();
    }

    /// Disables player input actions
    public override void OnDisable()
    {
        _playerInputActions.Player.Disable();
    }

    /// Sets up initial references and player visuals
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

    /// Updates player state, animations, and handles movement
    private void Update()
    {
        UpdateAnimations();

        if (!photonView.IsMine)
            return;

        verticalSpeed = _rb.linearVelocity.y;
        CheckIdleState();
        HandleMovement();

        UpdateJumpCharging();

        if (transform.position.y < deathHeight && !IsDead)
            playerDeathHandler.HandleOutOfBoundsDeath();
    }
    #endregion

    #region Player Setup
    /// Configures visuals for non-local players
    private void SetupRemotePlayerVisuals(SpriteRenderer sr, Graphic nameTagText, Canvas playerCanvas)
    {
        if (sr != null)
        {
            var c = sr.color;
            c.a = remotePlayerAlpha;
            sr.color = c;
            sr.sortingOrder = remotePlayerSpriteOrder;
        }
        else
        {
            Debug.LogWarning("No SpriteRenderer found on player GameObject!");
        }

        if (nameTagText != null)
        {
            var textColor = nameTagText.color;
            textColor.a = remotePlayerAlpha;
            nameTagText.color = textColor;
        }
        else
        {
            Debug.LogWarning("No TextMeshProUGUI found on player GameObject!");
        }

        if (playerCanvas != null)
            playerCanvas.sortingOrder = remotePlayerCanvasOrder;
        else
            Debug.LogWarning("No Canvas found on player GameObject!");
    }

    /// Sets up camera for local player
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
    /// Handles player movement, wall detection, and jump input
    private void HandleMovement()
    {
        if (IsPaused)
            return;

        _isTouchingFrontWall = Physics2D.OverlapCircle(frontWallCheck.position, wallCheckRadius, wallLayerMask);
        _isTouchingBackWall = Physics2D.OverlapCircle(backWallCheck.position, wallCheckRadius, wallLayerMask);

        if (!_isTouchingFrontWall && !_isTouchingBackWall)
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

    /// Changes player direction based on input and updates visuals
    private void HandlePlayerDirection(float horizontalInput)
    {
        var facingRight = transform.localScale.x > 0;

        var movingIntoWall = (facingRight && _isTouchingFrontWall && horizontalInput > 0) ||
                             (!facingRight && _isTouchingBackWall && horizontalInput < 0);

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

    /// Updates player speed based on input, acceleration and wall detection
    private void UpdatePlayerSpeed(float horizontalInput)
    {
        _newMaxSpeed = HasCatnip ? maxSpeed * 1.1f : maxSpeed;
        _newDeceleration = HasCatnip ? deceleration * 0.9f : deceleration;
        var currentTurboSpeed = HasCatnip ? turboSpeed * 1.1f : turboSpeed;

        var facingRight = transform.localScale.x > 0;

        var movingIntoFrontWall = _isTouchingFrontWall &&
                                  ((facingRight && horizontalInput > 0) ||
                                   (!facingRight && horizontalInput < 0));

        var movingIntoBackWall = _isTouchingBackWall &&
                                 ((facingRight && horizontalInput < 0) ||
                                  (!facingRight && horizontalInput > 0));

        if ((movingIntoFrontWall || movingIntoBackWall) && !_wallCollisionHandled)
        {
            currentSpeed = 0;
            _wallCollisionHandled = true;
        }

        var moveDirection = (int)Sign(horizontalInput);
        switch (Abs(horizontalInput))
        {
            case > 0.01f when !movingIntoFrontWall && !movingIntoBackWall:
                if (moveDirection != _lastMoveDirection && _lastMoveDirection != 0)
                {
                    _currentAccelTime = 0f;
                    _timeAtMaxSpeed = 0f;
                    _isTurboActive = false;
                }

                _lastMoveDirection = moveDirection;

                _currentAccelTime = Min(_currentAccelTime + Time.deltaTime, accelerationTime);

                var accelMultiplier = accelerationCurve.Evaluate(_currentAccelTime / accelerationTime);
                var currentAccel = baseAcceleration * accelMultiplier;

                var targetSpeed = _newMaxSpeed;

                if (Abs(currentSpeed) >= _newMaxSpeed * 0.98f && Approximately(Sign(currentSpeed), moveDirection))
                {
                    _timeAtMaxSpeed += Time.deltaTime;

                    if (_timeAtMaxSpeed >= timeToTurboSpeed)
                        _isTurboActive = true;
                }
                else
                {
                    _timeAtMaxSpeed = 0f;
                }

                if (_isTurboActive)
                    targetSpeed = currentTurboSpeed;

                currentSpeed = MoveTowards(currentSpeed, horizontalInput * targetSpeed, currentAccel * Time.deltaTime);
                break;

            case <= 0.01f:
            {
                _currentAccelTime = 0f;
                _timeAtMaxSpeed = 0f;
                _isTurboActive = false;
                _lastMoveDirection = 0;

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
    /// Processes jump button input for charging and executing jumps
    private void HandleJumpInput()
    {
        var jumpOnCooldown = Time.time < _lastJumpTime + jumpCooldown;

        if (_playerInputActions.Player.Jump.WasPressedThisFrame() &&
            !_isJumpQueued &&
            IsStanding &&
            !jumpOnCooldown)
        {
            _jumpButtonHeld = true;
            _jumpChargeStartTime = Time.time;

            if (IsGrounded)
            {
                _isChargingJump = true;
                _movementDisabledForJump = true;
                animator.SetBool(IsJumpQueued, true);
            }
            else
            {
                _isBufferingJump = true;
            }

            jumpChargeBarGameObject.SetActive(true);
        }

        if (!_playerInputActions.Player.Jump.WasReleasedThisFrame() || !_jumpButtonHeld)
            return;

        _jumpButtonHeld = false;

        if (_isChargingJump)
        {
            var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
            ExecuteJump(chargeTime);
            return;
        }

        if (!_isBufferingJump) return;

        {
            var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
            _bufferedJumpChargeLevel = chargeTime;

            if (chargeTime >= maxChargeTime)
                _bufferedJumpMaxCharged = true;
        }
    }

    /// Updates jump charge progress and visual indicators
    private void UpdateJumpCharging()
    {
        if (!_jumpButtonHeld) return;

        var isCharging = _isChargingJump || _isBufferingJump;
        if (!isCharging) return;

        var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
        var chargeProgress = Clamp01(chargeTime / maxChargeTime);

        jumpChargeBar.fillAmount = chargeProgress;

        if (_cameraController)
            _cameraController.UpdateChargingJumpFOV(chargeProgress);

        if (!(chargeTime >= maxChargeTime)) return;

        if (_isBufferingJump)
        {
            _bufferedJumpMaxCharged = true;
            _bufferedJumpChargeLevel = maxChargeTime;
        }
        else if (_isChargingJump && IsGrounded)
        {
            ExecuteJump(maxChargeTime);
            _jumpButtonHeld = false;
        }
    }

    /// Checks if buffered jump should be executed when landing
    private void CheckBufferedJumpLanding(bool wasGrounded, bool isGroundedNow)
    {
        if (wasGrounded || !isGroundedNow || !_isBufferingJump) return;

        _isBufferingJump = false;

        switch (_jumpButtonHeld)
        {
            case false when _bufferedJumpChargeLevel > 0:
            {
                var chargeToUse = _bufferedJumpMaxCharged ? maxChargeTime : _bufferedJumpChargeLevel;

                animator.SetBool(IsJumpQueued, true);

                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

                ExecuteJump(chargeToUse);
                break;
            }
            case true:
                _isChargingJump = true;
                _movementDisabledForJump = true;
                animator.SetBool(IsJumpQueued, true);

                break;
            default:
                jumpChargeBarGameObject.SetActive(false);
                break;
        }
    }

    /// Executes jump with appropriate force based on charge time
    private void ExecuteJump(float chargeTime)
    {
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
            _rb.linearVelocity = new Vector2(0f, jumpMultiplier);
            _lastJumpTime = Time.time;
        }

        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;
    }

    /// Cancels current jump charge state
    private void CancelJumpCharge()
    {
        _isChargingJump = false;
        _movementDisabledForJump = false;
        _jumpButtonHeld = false;

        if (!_isBufferingJump)
            jumpChargeBarGameObject.SetActive(false);

        animator.SetBool(IsJumpQueued, false);
        _isJumpQueued = false;
    }
    #endregion

    #region Animations
    /// Tracks idle time and triggers laying animation when idle
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

    /// Updates player animations based on movement and ground state
    private void UpdateAnimations()
    {
        var wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayerMask) || IsDead;

        if (wasGrounded != IsGrounded)
            CheckBufferedJumpLanding(wasGrounded, IsGrounded);

        if (!IsGrounded && (_isChargingJump || _isJumpQueued))
            CancelJumpCharge();

        if (IsGrounded && IsJumpPaused)
        {
            animator.speed = 1f;
            IsJumpPaused = false;
        }

        if (photonView.IsMine)
        {
            animator.SetBool(InAir, !IsGrounded);
            animator.SetFloat(Speed, Abs(_rb.linearVelocity.x));
        }
        else
        {
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

    /// Resets player acceleration state variables
    internal void ResetAccelerationState()
    {
        _currentAccelTime = 0f;
        _timeAtMaxSpeed = 0f;
        _isTurboActive = false;
        _lastMoveDirection = 0;
    }

    /// Gets or sets the player's current acceleration value
    internal float Acceleration
    {
        get => baseAcceleration * accelerationCurve.Evaluate(_currentAccelTime / accelerationTime);
        set
        {
            baseAcceleration = value;
            _currentAccelTime = 0f;
        }
    }

    /// Teleports player to specified position
    internal void Teleport(Vector3 position)
    {
        if (!photonView.IsMine)
            return;

        transform.position = position;
        currentSpeed = 0f;
    }

    /// Enables or disables player movement
    internal void SetMovement(bool isEnabled)
    {
        IsPaused = !isEnabled;
    }

    /// Disables player's rigidbody simulation
    internal void DisableRigidbody()
    {
        if (_rb)
            _rb.simulated = false;
    }

    /// Enables player's rigidbody simulation
    internal void EnableRigidbody()
    {
        if (_rb)
            _rb.simulated = true;
    }

    /// Sets player to spectator mode
    internal void SetSpectatorMode(bool isEnabled)
    {
        SetMovement(false);
    }

    /// RPC call to activate or deactivate catnip effect
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

    /// Handles player death state
    internal void OnPlayerDeath()
    {
        if (IsDead)
            return;

        IsDead = true;
        _originalGravityScale = _rb.gravityScale;
        _rb.gravityScale = 0;
        _rb.linearVelocity = zero;
        _rb.constraints = RigidbodyConstraints2D.FreezeAll;

        if (_cameraController)
            _cameraController.OnPlayerDeath();
    }

    /// Respawns player at the last checkpoint
    internal void RespawnAtLastCheckpoint()
    {
        Teleport(CheckpointManager.LastCheckpointPosition);
        OnPlayerRespawn();
    }

    /// Resets player state after respawning
    private void OnPlayerRespawn()
    {
        if (!IsDead)
            return;

        IsDead = false;
        _rb.gravityScale = _originalGravityScale;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (_cameraController)
            _cameraController.OnPlayerRespawn();
    }
    #endregion
}
