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
    private static readonly int IsWallSlidingAnimVar = Animator.StringToHash("IsWallSlidingAnimVar");
    [SerializeField] [Tooltip("Reference to the player's animator component")] public Animator animator;

    [Header("Movement")]
    [SerializeField] [Tooltip("How quickly player accelerates")] public AnimationCurve accelerationCurve = new(
        new Keyframe(0f, 0.3f),
        new Keyframe(0.6f, 0.7f),
        new Keyframe(1f, 1f)
    );
    [SerializeField] [Tooltip("Base acceleration value")] public float baseAcceleration = 10f;
    [SerializeField] [Range(0.2f, 2.0f)] [Tooltip("How long it takes to reach full acceleration")] public float accelerationTime = 0.8f;
    [SerializeField] [Tooltip("How quickly player slows down")] public float deceleration = 15f;
    [SerializeField] [Tooltip("Maximum movement speed")] public float maxSpeed = 5f;
    [SerializeField] [Tooltip("Higher speed reached after maintaining max speed")] public float turboSpeed = 7f;
    [SerializeField] [Tooltip("Time in seconds player needs to maintain max speed before reaching turbo speed")] public float timeToTurboSpeed = 1.5f;
    [SerializeField] [Range(0.0f, 0.1f)] [Tooltip("Minimum input value to register movement")] public float movementDeadzone = 0.01f;
    [SerializeField] [Range(0.7f, 0.99f)] [Tooltip("Threshold to consider player at max speed (0-1)")] public float maxSpeedThreshold = 0.98f;
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float verticalSpeed;

    /// Acceleration tracking
    private float _currentAccelTime;
    private float _timeAtMaxSpeed;
    private bool _isTurboActive;
    private int _lastMoveDirection;

    [Header("Ground Check")]
    [SerializeField] [Tooltip("Transform used to detect ground")] public Transform groundCheck;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Radius of the ground check sphere")] public float groundCheckRadius = 0.2f;
    [SerializeField] [Tooltip("Layer mask for ground detection")] public LayerMask groundLayerMask;

    [Header("Wall Detection")]
    [SerializeField] [Tooltip("Transform used to detect walls in front")] public Transform frontWallCheck;
    [SerializeField] [Tooltip("Transform used to detect walls behind")] public Transform backWallCheck;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Radius of the wall check sphere")] public float wallCheckRadius = 0.2f;
    [SerializeField] [Tooltip("Layer mask for wall detection")] public LayerMask wallLayerMask;

    [Header("Wall Sliding")]
    [SerializeField] [Tooltip("GameObject that will be rotated during wall sliding")] public GameObject wallSlideRotationObject;
    [SerializeField] [Range(0f, 90f)] [Tooltip("Rotation angle when wall sliding")] public float wallSlideRotationAngle = 15f;
    [SerializeField] [Range(0.1f, 1f)] [Tooltip("Wall sliding gravity multiplier")] public float wallSlideGravityMultiplier = 0.5f;
    [SerializeField] [Tooltip("X position offset for the rotation object during wall sliding")] public float wallSlideXOffset = 0.3f;
    [SerializeField] [Range(0.1f, 10f)] [Tooltip("How fast the player transforms into wall slide position")] public float wallSlideTransitionSpeed = 5f;
    [SerializeField] [Tooltip("Layers that prevent wall sliding when colliding with them")] public LayerMask wallSlidePreventionLayers;
    [SerializeField] [Range(-10f, 0f)] [Tooltip("Vertical velocity threshold to activate wall sliding")] public float wallSlideVerticalThreshold = -1f;

    [Header("Wall Collision")]
    [SerializeField] [Range(1f, 10f)] [Tooltip("Speed boost when hitting a wall from behind")] public float backWallBoostMultiplier = 1.5f;

    [Header("UI")]
    [SerializeField] [Tooltip("Player name text display")] public TextMeshProUGUI playerNameTag;
    [SerializeField] [Tooltip("Container for jump charge bar")] public GameObject jumpChargeBarGameObject;
    [SerializeField] [Tooltip("Jump charge fill bar")] public Image jumpChargeBar;

    [Header("Charged Jump")]
    [SerializeField] [Tooltip("Minimum jump force when uncharged")] public float minJumpForce = 8.5f;
    [SerializeField] [Tooltip("Maximum jump force when fully charged")] public float maxJumpForce = 14.5f;
    [SerializeField] [Tooltip("Maximum time to charge jump")] public float maxChargeTime = 2f;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Time before player can jump again")] public float jumpCooldown = 0.1f;

    [Header("Death")]
    [SerializeField] [Tooltip("Y-position that triggers death when fallen below")] public float deathHeight = -50f;
    [SerializeField] [Tooltip("Handler for player death events")] private PlayerDeathHandler playerDeathHandler;

    [Header("Visuals")]
    [SerializeField] [Tooltip("Sorting order for remote player sprites")] private int remotePlayerSpriteOrder = 2;
    [SerializeField] [Tooltip("Sorting order for remote player UI canvas")] private int remotePlayerCanvasOrder = 3;
    [SerializeField] [Range(0.1f, 1f)] [Tooltip("Alpha transparency for remote players (0-1)")] private float remotePlayerAlpha = 0.7f;

    [Header("Camera")]
    [SerializeField] [Tooltip("Local position offset for camera")] public Vector3 cameraOffset = new(0, 0, -10);

    [Header("Idle Behavior")]
    [SerializeField] [Tooltip("Time in seconds before triggering laying animation")] public float idleToLayingTime = 3.0f;

    [Header("Catnip Effects")]
    [SerializeField] [Range(1.0f, 2.0f)] [Tooltip("Speed multiplier when catnip is active")] public float catnipSpeedMultiplier = 1.1f;
    [SerializeField] [Range(1.0f, 2.0f)] [Tooltip("Jump force multiplier when catnip is active")] public float catnipJumpMultiplier = 1.1f;
    [SerializeField] [Range(0.5f, 1.0f)] [Tooltip("Deceleration multiplier when catnip is active")] public float catnipDecelerationMultiplier = 0.9f;

    [Header("Physics")]
    [SerializeField] [Range(0.5f, 3.0f)] [Tooltip("Normal gravity scale for the player")] public float defaultGravityScale = 1.0f;

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
    private bool _wallCollisionHandled;
    private float _lastJumpTime;
    private bool _isWallSliding;
    private float _defaultGravityScaleCache;
    private int _jumpChargeDirection;

    /// Jump buffer
    private bool _isBufferingJump;
    private float _bufferedJumpChargeLevel;
    private bool _bufferedJumpMaxCharged;
    private bool _executeBufferedJump;

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
    /// Initializes the input system
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

    /// Sets up initial references and configures local or remote player settings
    private void Start()
    {
        _mainCamera = Camera.main;
        _rb = GetComponent<Rigidbody2D>();
        _catnipFx = GetComponent<CatnipFx>();

        var playerCanvas = GetComponentInChildren<Canvas>();
        playerCanvas.worldCamera = _mainCamera;

        var sr = GetComponentInChildren<SpriteRenderer>();
        var nameTagText = GetComponentInChildren<TextMeshProUGUI>();

        if (!photonView.IsMine)
            SetupRemotePlayerVisuals(sr, nameTagText, playerCanvas);
        else
            SetupLocalPlayerCamera();
    }

    /// Handles per-frame animations and movement updates
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

    /// Handles physics-based calculations for wall sliding
    private void FixedUpdate()
    {
        if (photonView.IsMine)
            CheckWallSliding();
    }
    #endregion

    #region Player Setup
    /// Configures visuals for remote players with adjusted transparency and sorting order
    private void SetupRemotePlayerVisuals(SpriteRenderer sr, Graphic nameTagText, Canvas playerCanvas)
    {
        var c = sr.color;
        c.a = remotePlayerAlpha;
        sr.color = c;
        sr.sortingOrder = remotePlayerSpriteOrder;

        var textColor = nameTagText.color;
        textColor.a = remotePlayerAlpha;
        nameTagText.color = textColor;

        playerCanvas.sortingOrder = remotePlayerCanvasOrder;
    }

    /// Configures camera settings for the local player
    private void SetupLocalPlayerCamera()
    {
        Transform transform1;
        (transform1 = _mainCamera.transform).SetParent(transform);
        transform1.localPosition = cameraOffset;
        transform1.localRotation = Quaternion.identity;
        _cameraController = GetComponentInChildren<DynamicCameraController>();
    }
    #endregion

    #region Movement
    /// Processes player input for movement and wall collision detection
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

    /// Handles wall slide behavior and applies appropriate physics adjustments
    private void CheckWallSliding()
    {
        if (_defaultGravityScaleCache == 0)
            _defaultGravityScaleCache = _rb.gravityScale;

        var wasWallSliding = _isWallSliding;

        var isCollidingWithPreventionLayer = false;
        if (wallSlidePreventionLayers != 0)
            isCollidingWithPreventionLayer =
                Physics2D.OverlapCircle(frontWallCheck.position, wallCheckRadius, wallSlidePreventionLayers) ||
                Physics2D.OverlapCircle(backWallCheck.position, wallCheckRadius, wallSlidePreventionLayers);

        _isWallSliding = !IsGrounded &&
                         (_isTouchingFrontWall || _isTouchingBackWall) &&
                         _rb.linearVelocity.y < wallSlideVerticalThreshold &&
                         !isCollidingWithPreventionLayer;

        if (_isWallSliding)
        {
            _rb.gravityScale = _defaultGravityScaleCache * wallSlideGravityMultiplier;

            var facingRight = transform.localScale.x > 0;
            float targetRotationZ = 0;
            float directionMultiplier = facingRight ? 1 : -1;

            switch (facingRight)
            {
                case true when _isTouchingFrontWall:
                case false when _isTouchingBackWall:
                    targetRotationZ = wallSlideRotationAngle * directionMultiplier;
                    break;
                case true when _isTouchingBackWall:
                case false when _isTouchingFrontWall:
                    targetRotationZ = -wallSlideRotationAngle * directionMultiplier;
                    break;
            }

            if (wallSlideRotationObject)
            {
                wallSlideRotationObject.transform.localRotation = Quaternion.Euler(0, 0, targetRotationZ);

                var currentPosition = wallSlideRotationObject.transform.localPosition;
                var targetPosition = new Vector3(wallSlideXOffset, currentPosition.y, currentPosition.z);
                wallSlideRotationObject.transform.localPosition = Vector3.Lerp(
                    currentPosition,
                    targetPosition,
                    Time.fixedDeltaTime * wallSlideTransitionSpeed);
            }
        }
        else
        {
            _rb.gravityScale = _defaultGravityScaleCache;

            if (wallSlideRotationObject)
            {
                wallSlideRotationObject.transform.localRotation = Quaternion.Euler(0, 0, 0);

                var currentPosition = wallSlideRotationObject.transform.localPosition;
                var targetPosition = new Vector3(0f, currentPosition.y, currentPosition.z);
                wallSlideRotationObject.transform.localPosition = Vector3.Lerp(
                    currentPosition,
                    targetPosition,
                    Time.fixedDeltaTime * wallSlideTransitionSpeed);
            }
        }

        if (wasWallSliding != _isWallSliding)
            animator.SetBool(IsWallSlidingAnimVar, _isWallSliding);
    }

    /// Updates player direction based on input and adjusts related visuals
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

    /// Calculates player speed based on input, acceleration, and collisions
    private void UpdatePlayerSpeed(float horizontalInput)
    {
        if (_isChargingJump || _isBufferingJump)
        {
            _rb.linearVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
            return;
        }

        _newMaxSpeed = HasCatnip ? maxSpeed * catnipSpeedMultiplier : maxSpeed;
        _newDeceleration = HasCatnip ? deceleration * catnipDecelerationMultiplier : deceleration;
        var currentTurboSpeed = HasCatnip ? turboSpeed * catnipSpeedMultiplier : turboSpeed;

        var facingRight = transform.localScale.x > 0;

        var movingIntoFrontWall = _isTouchingFrontWall &&
                                  ((facingRight && horizontalInput > 0) ||
                                   (!facingRight && horizontalInput < 0));

        var movingIntoBackWall = _isTouchingBackWall &&
                                 ((facingRight && horizontalInput < 0) ||
                                  (!facingRight && horizontalInput > 0));

        if (movingIntoFrontWall && !_wallCollisionHandled)
        {
            currentSpeed = 0;
            _wallCollisionHandled = true;
        }
        else if (movingIntoBackWall && !_wallCollisionHandled)
        {
            var boostSpeed = _newMaxSpeed * backWallBoostMultiplier;
            currentSpeed = facingRight ? boostSpeed : -boostSpeed;
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

                if (Abs(currentSpeed) >= _newMaxSpeed * maxSpeedThreshold && Approximately(Sign(currentSpeed), moveDirection))
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
                if (Abs(currentSpeed) < movementDeadzone)
                    currentSpeed = 0;
                break;
            }
        }

        _rb.linearVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
    }
    #endregion

    #region Jump
    /// Processes jump button input for charging, buffering and executing jumps
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

            _jumpChargeDirection = (int)Sign(_rb.linearVelocity.x);

            ResetAccelerationState();

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

            _executeBufferedJump = true;
        }
    }

    /// Updates jump charge bar and camera FOV during jump charging
    private void UpdateJumpCharging()
    {
        if (!_jumpButtonHeld) return;

        var isCharging = _isChargingJump || _isBufferingJump;
        if (!isCharging) return;

        var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
        var chargeProgress = Clamp01(chargeTime / maxChargeTime);

        jumpChargeBar.fillAmount = chargeProgress;

        _cameraController.UpdateChargingJumpFOV(chargeProgress);

        if (!(chargeTime >= maxChargeTime)) return;

        if (!_isBufferingJump) return;

        _bufferedJumpMaxCharged = true;
        _bufferedJumpChargeLevel = maxChargeTime;
    }

    /// Handles buffered jump execution when player lands after jump input
    private void CheckBufferedJumpLanding(bool wasGrounded, bool isGroundedNow)
    {
        if (wasGrounded || !isGroundedNow || !_isBufferingJump) return;

        _isBufferingJump = false;

        switch (_jumpButtonHeld)
        {
            case false when _executeBufferedJump:
            {
                var chargeToUse = _bufferedJumpMaxCharged ? maxChargeTime : _bufferedJumpChargeLevel;

                animator.SetBool(IsJumpQueued, true);
                ExecuteJump(chargeToUse);
                _executeBufferedJump = false;
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

    /// Applies jump force based on charge time and resets jump-related states
    private void ExecuteJump(float chargeTime)
    {
        _isChargingJump = false;
        _movementDisabledForJump = false;
        _isBufferingJump = false;
        _isJumpQueued = false;
        jumpChargeBarGameObject.SetActive(false);

        var chargeProgress = Clamp01(chargeTime / maxChargeTime);
        var jumpMultiplier = Lerp(minJumpForce, maxJumpForce, chargeProgress);

        if (HasCatnip)
            jumpMultiplier *= catnipJumpMultiplier;

        _cameraController.TriggerJumpFOV();

        ResetAccelerationState();

        var currentHorizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
        var currentInputDirection = (int)Sign(currentHorizontalInput);

        var isOppositeDirection = _jumpChargeDirection != 0 &&
                                  currentInputDirection != 0 &&
                                  currentInputDirection != _jumpChargeDirection;

        if (isOppositeDirection)
        {
            _rb.linearVelocity = new Vector2(0, jumpMultiplier);
            currentSpeed = 0;
        }
        else
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpMultiplier);
        }

        _lastJumpTime = Time.time;
        animator.SetBool(IsJumpQueued, false);
    }

    /// Cancels jump charge and resets jump-related states
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
    /// Tracks idle time and triggers laying animation when idle threshold reached
    private void CheckIdleState()
    {
        if (_isWallSliding)
        {
            _idleTimer = 0f;
            return;
        }

        if (Approximately(currentSpeed, 0f) && Approximately(verticalSpeed, 0f) && animator.GetBool(IsLaying) == false)
        {
            _idleTimer += Time.deltaTime;
            if (!(_idleTimer >= idleToLayingTime)) return;

            animator.SetBool(IsLaying, true);
            _idleTimer = idleToLayingTime;
        }
        else
        {
            _idleTimer = 0f;
        }
    }

    /// Updates animator parameters and checks ground state
    private void UpdateAnimations()
    {
        var wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayerMask) || IsDead;

        if (wasGrounded != IsGrounded)
            CheckBufferedJumpLanding(wasGrounded, IsGrounded);

        if (wasGrounded && !IsGrounded && (_isChargingJump || _isJumpQueued))
        {
            var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
            ExecuteJump(chargeTime);
        }
        else if (!IsGrounded && (_isChargingJump || _isJumpQueued))
        {
            CancelJumpCharge();
        }

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
    /// Resets acceleration-related variables to initial state
    internal void ResetAccelerationState()
    {
        _currentAccelTime = 0f;
        _timeAtMaxSpeed = 0f;
        _isTurboActive = false;
        _lastMoveDirection = 0;
    }

    /// Gets or sets player acceleration with curve evaluation
    internal float Acceleration
    {
        get => baseAcceleration * accelerationCurve.Evaluate(_currentAccelTime / accelerationTime);
        set
        {
            baseAcceleration = value;
            _currentAccelTime = 0f;
        }
    }

    /// Teleports player to specified position and resets speed
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

    /// Disables rigidbody simulation
    internal void DisableRigidbody()
    {
        _rb.simulated = false;
    }

    /// Enables rigidbody simulation
    internal void EnableRigidbody()
    {
        _rb.simulated = true;
    }

    /// Sets spectator mode state
    internal void SetSpectatorMode(bool isEnabled)
    {
        SetMovement(isEnabled);
    }

    /// Remote procedure call to toggle catnip effect across network
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
    /// Handles player death state and physics
    internal void OnPlayerDeath()
    {
        if (IsDead)
            return;

        IsDead = true;
        _rb.gravityScale = 0;
        _rb.linearVelocity = zero;
        _rb.constraints = RigidbodyConstraints2D.FreezeAll;
        _isWallSliding = false;
        animator.SetBool(IsWallSlidingAnimVar, false);

        if (_cameraController)
            _cameraController.OnPlayerDeath();
    }

    /// Respawns player at last checkpoint position
    internal void RespawnAtLastCheckpoint()
    {
        CheckpointManager.IsRespawning = true;
        Teleport(CheckpointManager.LastCheckpointPosition);
        OnPlayerRespawn();

        Invoke(nameof(ResetRespawnFlag), 0.5f);
    }

    /// Resets checkpoint respawn flag after delay
    private void ResetRespawnFlag()
    {
        CheckpointManager.IsRespawning = false;
    }

    /// Resets physics and constraints when player respawns
    private void OnPlayerRespawn()
    {
        if (!IsDead)
            return;

        IsDead = false;
        _rb.gravityScale = defaultGravityScale;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (_cameraController)
            _cameraController.OnPlayerRespawn();
    }
    #endregion
}
