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
    private static readonly int IsWallSliding = Animator.StringToHash("IsWallSliding");
    [SerializeField] [Tooltip("Reference to the player's animator component")] public Animator animator;
    [SerializeField] [Tooltip("Reference to the player's sprite renderer")] public SpriteRenderer playerSprite;

    [Header("Movement")]
    [SerializeField] [Tooltip("How quickly player accelerates")] public AnimationCurve accelerationCurve = new(new Keyframe(0f, 0.3f), new Keyframe(0.6f, 0.7f), new Keyframe(1f, 1f));
    [SerializeField] [Tooltip("Base acceleration value")] public float baseAcceleration = 10f;
    [SerializeField] [Range(0.2f, 2.0f)] [Tooltip("How long it takes to reach full acceleration")] public float accelerationTime = 0.8f;
    [SerializeField] [Tooltip("How quickly player slows down")] public float deceleration = 15f;
    [SerializeField] [Tooltip("Maximum movement speed")] public float maxSpeed = 5f;
    [SerializeField] [Tooltip("Higher speed reached after maintaining max speed")] public float turboSpeed = 7f;
    [SerializeField] [Tooltip("Time in seconds player needs to maintain max speed before reaching turbo speed")] public float timeToTurboSpeed = 1.5f;
    [SerializeField] [Range(0.7f, 0.99f)] [Tooltip("Threshold to consider player at max speed (0-1)")] public float maxSpeedThreshold = 0.98f;
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float verticalSpeed;

    private float _currentAccelTime;
    private float _timeAtMaxSpeed;
    private bool _isTurboActive;
    private int _lastMoveDirection;

    [Header("Ground Check")]
    [SerializeField] [Tooltip("Transform used to detect ground")] public Transform groundCheck;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Radius of the ground check sphere")] public float groundCheckRadius = 0.2f;
    [SerializeField] [Tooltip("Layer mask for ground detection")] public LayerMask groundLayerMask;

    [Header("Wall Check")]
    [SerializeField] [Tooltip("Transform used to detect left wall")] public Transform leftWallCheck;
    [SerializeField] [Tooltip("Transform used to detect right wall")] public Transform rightWallCheck;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Radius of the wall check sphere")] public float wallCheckRadius = 0.2f;
    [SerializeField] [Tooltip("Layer mask for wall detection")] public LayerMask wallLayerMask;
    [SerializeField] [Range(0f, 1f)] [Tooltip("Distance for wall raycast detection")] public float wallRaycastDistance = 0.1f;

    [Header("Wall Sliding")]
    [SerializeField] [Range(0.1f, 5f)] [Tooltip("Reduced falling speed when sliding on walls")] public float wallSlideSpeed = 2f;
    [SerializeField] [Range(0.1f, 5f)] [Tooltip("Force to apply against the wall")] private float wallStickForce = 1.5f;
    [SerializeField] [Tooltip("Transform of the visual object to rotate during wall slide")] public Transform spriteTransform;
    [SerializeField] [Range(-0.2f, 0.05f)] [Tooltip("X offset of sprite during wall sliding")] public float wallSlideOffset = -0.06f;

    [Header("Wall Jumping")]
    [SerializeField] [Tooltip("Minimum jump force when uncharged")] public float minWallJumpForce = 10f;
    [SerializeField] [Tooltip("Maximum jump force when fully charged")] public float maxWallJumpForce = 16f;
    [SerializeField] [Range(0.1f, 2f)] [Tooltip("Maximum time to charge wall jump")] public float maxWallChargeTime = 1f;
    [SerializeField] [Tooltip("Force applied perpendicular to the wall")] public float wallDetachForce = 8f;
    [SerializeField] [Tooltip("Force pushing player away when reaching ground during wall jump charge")] public float wallGroundPushForce = 5f;
    [SerializeField] [Tooltip("Minimum time player needs to be on wall before can wall jump")] public float minWallContactTime = 0.05f;

    [Header("Wall Bounce")]
    [SerializeField] [Range(0f, 2f)] [Tooltip("Time window after a charged jump when wall bounces are enabled")] public float wallBounceWindow = 0.5f;
    [SerializeField] [Range(1f, 15f)] [Tooltip("Force applied when bouncing off a wall")] public float wallBounceForce = 8f;
    [SerializeField] [Range(0f, 2f)] [Tooltip("Vertical force multiplier for wall bounces")] public float wallBounceVerticalMultiplier = 0.5f;
    [SerializeField] [Tooltip("Whether bounce preserves some of the player's momentum")] public bool preserveMomentumOnBounce = true;
    [SerializeField] [Range(0f, 1f)] [Tooltip("How much of the player's momentum is preserved during a bounce (0-1)")] public float momentumPreservation = 0.7f;

    [Header("UI")]
    [SerializeField] [Tooltip("Player name text display")] public TextMeshProUGUI playerNameTag;

    [Header("Charged Jump")]
    [SerializeField] [Tooltip("Minimum jump force when uncharged")] public float minJumpForce = 8.5f;
    [SerializeField] [Tooltip("Maximum jump force when fully charged")] public float maxJumpForce = 14f;
    [SerializeField] [Range(0.1f, 2f)] [Tooltip("Maximum time to charge regular jump")] public float maxChargeTime = 2f;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Time before player can jump again")] public float jumpCooldown = 0.1f;

    [Header("Death")]
    [SerializeField] [Tooltip("Y-position that triggers death when fallen below")] public float deathHeight = -50f;
    [SerializeField] [Tooltip("Handler for player death events")] private PlayerDeathHandler playerDeathHandler;

    [Header("Visuals")]
    [SerializeField] [Tooltip("Sorting order for remote player sprites")] private int remotePlayerSpriteOrder = 2;
    [SerializeField] [Tooltip("Sorting order for remote player UI canvas")] private int remotePlayerCanvasOrder = 3;
    [SerializeField] [Range(0.1f, 1f)] [Tooltip("Alpha transparency for remote players (0-1)")] private float remotePlayerAlpha = 0.7f;

    [Header("Idle Behavior")]
    [SerializeField] [Tooltip("Time in seconds before triggering laying animation")] public float idleToLayingTime = 3.0f;

    [Header("Catnip Effects")]
    [SerializeField] [Range(1.0f, 2.0f)] [Tooltip("Speed multiplier when catnip is active")] public float catnipSpeedMultiplier = 1.1f;
    [SerializeField] [Range(1.0f, 2.0f)] [Tooltip("Jump force multiplier when catnip is active")] public float catnipJumpMultiplier = 1.1f;
    [SerializeField] [Range(0.5f, 1.0f)] [Tooltip("Deceleration multiplier when catnip is active")] public float catnipDecelerationMultiplier = 0.9f;

    [Header("Physics")]
    [SerializeField] [Range(0.5f, 3.0f)] [Tooltip("Normal gravity scale for the player")] public float defaultGravityScale = 1.0f;

    private Camera _mainCamera;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private DynamicCameraController _cameraController;
    private InputSystem_Actions _playerInputActions;
    private CatnipFx _catnipFx;
    private SpriteFlipManager _spriteFlipManager;
    private JumpChargeUIManager _jumpChargeUIManager;
    private JumpStateEnum _previousJumpState;

    internal enum JumpStateEnum
    {
        Idle,
        Charging,
        WallCharging,
        Buffered
    }

    private float _jumpChargeStartTime;
    private bool _jumpButtonHeld;
    private bool _movementDisabledForJump;
    private float _idleTimer;
    private bool _wallCollisionHandled;
    private float _lastJumpTime;
    private float _defaultGravityScaleCache;
    private int _jumpChargeDirection;

    private bool _startedChargingOnGround;
    private bool _releaseJumpInAir;
    private float _jumpChargeLevel;
    private bool _jumpFullyCharged;

    private float _newMaxSpeed;
    private float _newDeceleration;

    private bool _isTouchingLeftWall;
    private bool _isTouchingRightWall;
    private bool _isWallSliding;
    private int _wallSlideSide; // -1 for left, 1 for right
    private float _wallContactTime;
    private Vector3 _originalSpritePosition;
    private bool _postWallJump; // Flag to track post-wall-jump state
    private int _lastWallSlideSideRPC;

    private bool _isInBounceWindow;
    private float _bounceWindowEndTime;
    private bool _hasBounced;

    internal bool IsStanding { get; set; }
    internal bool IsGrounded { get; private set; }
    internal bool IsJumpPaused { get; set; }
    internal bool IsPaused { get; set; }
    internal bool HasCatnip { get; set; }
    internal bool IsDead { get; private set; }
    private bool IsTouchingWall => _isTouchingLeftWall || _isTouchingRightWall;
    internal JumpStateEnum JumpState = JumpStateEnum.Idle;

    #endregion

    #region Unity Lifecycle
    /// Initializes input system
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

    /// Sets up references and configurations
    private void Start()
    {
        _mainCamera = Camera.main;
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _catnipFx = GetComponent<CatnipFx>();
        _spriteFlipManager = GetComponent<SpriteFlipManager>();
        _jumpChargeUIManager = GetComponent<JumpChargeUIManager>();
        _previousJumpState = JumpState;

        if (spriteTransform == null)
            spriteTransform = playerSprite.transform;

        _originalSpritePosition = spriteTransform.localPosition;

        var playerCanvas = GetComponentInChildren<Canvas>();
        playerCanvas.worldCamera = _mainCamera;

        if (!photonView.IsMine)
            SetupRemotePlayerVisuals(playerSprite, playerNameTag, playerCanvas);
        else
            SetupLocalPlayerCamera();
    }

    /// Handles per-frame updates and checks
    private void Update()
    {
        UpdateAnimations();
        CheckWallContact();

        if (!photonView.IsMine)
            return;

        if (JumpState != _previousJumpState)
        {
            SyncJumpState();
            _previousJumpState = JumpState;
        }

        verticalSpeed = _rb.linearVelocity.y;
        HandleWallSliding();
        CheckIdleState();
        HandleMovement();
        CheckWallBounce();

        UpdateJumpCharging();

        if (transform.position.y < deathHeight && !IsDead)
            playerDeathHandler.HandleOutOfBoundsDeath();

        if (_postWallJump && IsTouchingWall)
        {
            var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
            var moveDirection = (int)Sign(horizontalInput);

            if (moveDirection != 0 && moveDirection != _wallSlideSide)
            {
                _postWallJump = false;
                spriteTransform.rotation = Quaternion.identity;
                spriteTransform.localPosition = _originalSpritePosition;
            }
        }

        if (_postWallJump && !IsTouchingWall)
            _postWallJump = false;

        if (_isInBounceWindow && Time.time > _bounceWindowEndTime)
            _isInBounceWindow = false;
    }
    #endregion

    #region Wall Bounce
    /// Checks if player should bounce off wall after a charged jump
    private void CheckWallBounce()
    {
        if (!_isInBounceWindow || _isWallSliding || _hasBounced || JumpState == JumpStateEnum.WallCharging)
            return;

        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
        var moveDirection = (int)Sign(horizontalInput);

        // Check for left wall bounce - player actively pressing left toward a left wall
        if (_isTouchingLeftWall && moveDirection < 0)
        {
            ApplyWallBounce(-1); // -1 is left wall
            _hasBounced = true;
            return;
        }

        // Check for right wall bounce - player actively pressing right toward a right wall
        if (_isTouchingRightWall && moveDirection > 0)
        {
            ApplyWallBounce(1); // 1 is right wall
            _hasBounced = true;
            return;
        }

        // Alternative detection using velocity if no input
        if (!(Abs(horizontalInput) < 0.01f)) return;

        var velocityDirection = (int)Sign(_rb.linearVelocity.x);

        // Check for bounce based on velocity direction
        if (_isTouchingLeftWall && velocityDirection < 0)
        {
            ApplyWallBounce(-1);
            _hasBounced = true;
            return;
        }

        if (!_isTouchingRightWall || velocityDirection <= 0) return;

        ApplyWallBounce(1);
        _hasBounced = true;
    }

    /// Applies bounce force when hitting a wall during bounce window
    private void ApplyWallBounce(int wallSide)
    {
        var bounceDirection = -wallSide; // Bounce away from wall
        var linearVelocity = _rb.linearVelocity;
        var currentXVelocity = linearVelocity.x;
        var currentYVelocity = linearVelocity.y;

        // Calculate bounce velocity
        var xVelocity = bounceDirection * wallBounceForce;

        // Preserve some momentum if enabled
        if (preserveMomentumOnBounce && Abs(currentXVelocity) > 0)
            // Only preserve momentum if moving toward the wall (not if already moving away)
            if (Approximately(Sign(currentXVelocity), wallSide))
            {
                var preservedMomentum = Abs(currentXVelocity) * momentumPreservation;
                xVelocity = bounceDirection * Max(wallBounceForce, preservedMomentum);
            }

        // Apply vertical boost
        var yVelocity = Max(currentYVelocity, 0) + (wallBounceForce * wallBounceVerticalMultiplier);

        // Apply the bounce force
        _rb.linearVelocity = new Vector2(xVelocity, yVelocity);
        currentSpeed = xVelocity;

        // Reset acceleration state for smooth control after bounce
        ResetAccelerationState();

        // Trigger camera effect
        if (_cameraController)
            _cameraController.TriggerJumpFOV();
    }
    #endregion

    #region Player Setup
    /// Configures visual settings for non-local players
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

    /// Sets up camera for the local player
    private void SetupLocalPlayerCamera()
    {
        Transform transform1;
        (transform1 = _mainCamera.transform).SetParent(transform);
        transform1.localRotation = Quaternion.identity;
        _cameraController = _mainCamera.GetComponent<DynamicCameraController>();
        if (_cameraController == null)
            _cameraController = _mainCamera.gameObject.AddComponent<DynamicCameraController>();
    }
    #endregion

    #region Wall Detection and Sliding
    /// Checks for wall contact on both sides using transform checks and collider contacts
    private void CheckWallContact()
    {
        var wasLeftWall = _isTouchingLeftWall;
        var wasRightWall = _isTouchingRightWall;

        var horizontalInput = photonView.IsMine ? _playerInputActions.Player.Move.ReadValue<Vector2>().x : 0;
        var moveDirection = (int)Sign(horizontalInput);
        var velocityDirection = (int)Sign(_rb.linearVelocity.x);

        var leftTransformCheck = Physics2D.OverlapCircle(leftWallCheck.position, wallCheckRadius, wallLayerMask);
        var rightTransformCheck = Physics2D.OverlapCircle(rightWallCheck.position, wallCheckRadius, wallLayerMask);

        var colliderBounds = _collider.bounds;
        var leftColliderCheck = false;
        var rightColliderCheck = false;

        if (moveDirection <= 0 || velocityDirection < 0)
        {
            var leftRayOrigin = new Vector2(colliderBounds.min.x, colliderBounds.center.y);
            leftColliderCheck = Physics2D.Raycast(leftRayOrigin, left, wallRaycastDistance, wallLayerMask);
            Debug.DrawRay(leftRayOrigin, left * wallRaycastDistance, leftColliderCheck ? Color.red : Color.green);
        }

        if (moveDirection >= 0 || velocityDirection > 0)
        {
            var rightRayOrigin = new Vector2(colliderBounds.max.x, colliderBounds.center.y);
            rightColliderCheck = Physics2D.Raycast(rightRayOrigin, right, wallRaycastDistance, wallLayerMask);
            Debug.DrawRay(rightRayOrigin, right * wallRaycastDistance, rightColliderCheck ? Color.red : Color.green);
        }

        _isTouchingLeftWall = leftTransformCheck && leftColliderCheck;
        _isTouchingRightWall = rightTransformCheck && rightColliderCheck;

        CheckWallJumpAutoExecute(wasLeftWall, wasRightWall);

        if (!wasLeftWall && _isTouchingLeftWall)
        {
            _wallSlideSide = -1;
            _wallContactTime = 0f;
        }
        else if (!wasRightWall && _isTouchingRightWall)
        {
            _wallSlideSide = 1;
            _wallContactTime = 0f;
        }

        if (IsTouchingWall)
            _wallContactTime += Time.deltaTime;

        if (IsGrounded && JumpState == JumpStateEnum.WallCharging)
            CancelWallJumpWithGroundPush();

        if ((_isTouchingLeftWall || _isTouchingRightWall) &&
            (JumpState == JumpStateEnum.Charging || JumpState == JumpStateEnum.Buffered) &&
            !_startedChargingOnGround)
            ConvertToWallJump();
    }

    /// Executes wall jump if player loses contact with wall during charge
    private void CheckWallJumpAutoExecute(bool wasTouchingLeftWall, bool wasTouchingRightWall)
    {
        var lostWallContact = (wasTouchingLeftWall && !_isTouchingLeftWall) ||
                              (wasTouchingRightWall && !_isTouchingRightWall);

        if (!lostWallContact || JumpState != JumpStateEnum.WallCharging) return;

        var horizontalInput = photonView.IsMine ? _playerInputActions.Player.Move.ReadValue<Vector2>().x : 0;
        var wallChargeTime = Min(Time.time - _jumpChargeStartTime, maxWallChargeTime);
        ExecuteWallJump(wallChargeTime, horizontalInput);
    }

    /// Handles wall sliding physics and visuals for both local and remote players
    private void HandleWallSliding()
    {
        var wasWallSliding = _isWallSliding;

        // Local player logic for detecting wall sliding
        if (photonView.IsMine)
        {
            var wallSlideCondition = IsTouchingWall && !IsGrounded && (_rb.linearVelocity.y < 0 || _postWallJump);
            _isWallSliding = wallSlideCondition;

            if ((_isTouchingLeftWall || _isTouchingRightWall) && !wasWallSliding && _isWallSliding)
                _wallSlideSide = _isTouchingLeftWall ? -1 : 1;

            if (_isWallSliding && IsTouchingWall)
            {
                if (_isTouchingLeftWall && _wallSlideSide != -1)
                    _wallSlideSide = -1;
                else if (_isTouchingRightWall && _wallSlideSide != 1)
                    _wallSlideSide = 1;
            }

            // Local player physics
            if (_isWallSliding)
            {
                if (_rb.linearVelocity.y < 0)
                {
                    _rb.linearVelocity = new Vector2(_wallSlideSide * wallStickForce, Max(_rb.linearVelocity.y, -wallSlideSpeed));

                    if (_postWallJump)
                        _postWallJump = false;
                }
            }
            else if (JumpState == JumpStateEnum.WallCharging)
            {
                _rb.linearVelocity = new Vector2(_wallSlideSide * wallStickForce, _rb.linearVelocity.y);
            }

            // Sync wall slide side to other players when it changes
            if (wasWallSliding != _isWallSliding || (_isWallSliding && _lastWallSlideSideRPC != _wallSlideSide))
            {
                photonView.RPC(nameof(RPC_SyncWallSlideVisuals), RpcTarget.Others, _isWallSliding, _wallSlideSide);
                _lastWallSlideSideRPC = _wallSlideSide;
            }
        }

        // Visual updates for both local and remote players
        UpdateWallSlideVisuals(_isWallSliding, _wallSlideSide);

        animator.SetBool(IsWallSliding, _isWallSliding);
    }

    /// Updates wall sliding visuals for both local and remote players
    private void UpdateWallSlideVisuals(bool isSliding, int slideSide)
    {
        if (isSliding || JumpState == JumpStateEnum.WallCharging)
        {
            // Handle sprite orientation for all players
            if (slideSide > 0 != _spriteFlipManager.IsFacingRight())
                if (photonView.IsMine)
                    FlipPlayerSprite();
            // Remote players will receive this through RPC in SpriteFlipManager
            // Apply rotation and offset to the sprite
            spriteTransform.rotation = Quaternion.Euler(0, 0, slideSide * 90f);
            var offsetPosition = _originalSpritePosition;
            offsetPosition.x += slideSide * wallSlideOffset;
            spriteTransform.localPosition = offsetPosition;
        }
        else if (spriteTransform.rotation != Quaternion.identity || spriteTransform.localPosition != _originalSpritePosition)
        {
            // Reset sprite transformation when not wall sliding
            spriteTransform.rotation = Quaternion.identity;
            spriteTransform.localPosition = _originalSpritePosition;
        }
    }

    /// RPC to sync wall sliding visuals to remote players
    [PunRPC]
    private void RPC_SyncWallSlideVisuals(bool isSliding, int slideSide)
    {
        if (photonView.IsMine) return;

        _isWallSliding = isSliding;
        _wallSlideSide = slideSide;

        UpdateWallSlideVisuals(isSliding, slideSide);
    }

    /// Converts normal jump to wall jump when hitting a wall
    private void ConvertToWallJump()
    {
        var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
        var chargeProgress = Clamp01(chargeTime / maxChargeTime);

        JumpState = JumpStateEnum.WallCharging;

        _jumpChargeStartTime = Time.time - (chargeProgress * maxWallChargeTime);
        _wallSlideSide = _isTouchingLeftWall ? -1 : 1;

        animator.SetBool(IsJumpQueued, true);
    }

    /// Pushes player away from wall when they reach ground during wall jump charge
    private void CancelWallJumpWithGroundPush()
    {
        JumpState = JumpStateEnum.Idle;

        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        animator.SetBool(IsJumpQueued, false);

        _rb.linearVelocity = new Vector2(-_wallSlideSide * wallGroundPushForce, _rb.linearVelocity.y);

        spriteTransform.rotation = Quaternion.identity;
        spriteTransform.localPosition = _originalSpritePosition;
    }
    #endregion

    #region Movement
    /// Processes player movement input
    private void HandleMovement()
    {
        if (IsPaused)
            return;

        HandleJumpInput();

        if (_movementDisabledForJump)
            return;

        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;

        if (!_isWallSliding && JumpState != JumpStateEnum.WallCharging)
            HandlePlayerDirection(horizontalInput);

        if (!IsStanding)
            return;

        UpdatePlayerSpeed(horizontalInput);
    }

    /// Flips the player sprite
    private void FlipPlayerSprite()
    {
        if (!photonView.IsMine) return;

        _spriteFlipManager.FlipAll();
    }

    /// Manages player direction based on input
    private void HandlePlayerDirection(float horizontalInput)
    {
        switch (horizontalInput)
        {
            case > 0 when !_spriteFlipManager.IsFacingRight():
            case < 0 when _spriteFlipManager.IsFacingRight():
                FlipPlayerSprite();
                animator.SetBool(IsLaying, false);
                _idleTimer = 0f;
                break;
        }
    }

    /// Updates player speed based on input and prevents movement into walls
    private void UpdatePlayerSpeed(float horizontalInput)
    {
        _newMaxSpeed = HasCatnip ? maxSpeed * catnipSpeedMultiplier : maxSpeed;
        _newDeceleration = HasCatnip ? deceleration * catnipDecelerationMultiplier : deceleration;
        var currentTurboSpeed = HasCatnip ? turboSpeed * catnipSpeedMultiplier : turboSpeed;

        if (_isWallSliding || JumpState == JumpStateEnum.WallCharging)
        {
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            return;
        }

        if (JumpState == JumpStateEnum.Charging && _startedChargingOnGround)
        {
            _rb.linearVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
            return;
        }

        if ((horizontalInput < 0 && _isTouchingLeftWall) || (horizontalInput > 0 && _isTouchingRightWall))
        {
            currentSpeed = MoveTowards(currentSpeed, 0, _newDeceleration * Time.deltaTime);
            _rb.linearVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
            return;
        }

        var moveDirection = (int)Sign(horizontalInput);
        switch (Abs(horizontalInput))
        {
            case > 0.01f:
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
                break;
            }
        }

        _rb.linearVelocity = new Vector2(currentSpeed, _rb.linearVelocity.y);
    }
    #endregion

    #region Jump
    /// Processes jump input and initiates jump
    private void HandleJumpInput()
    {
        var jumpOnCooldown = Time.time < _lastJumpTime + jumpCooldown;
        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;

        if (_playerInputActions.Player.Jump.WasPressedThisFrame() && JumpState == JumpStateEnum.Idle && IsStanding && !jumpOnCooldown)
            switch (_isWallSliding)
            {
                case true when _wallContactTime >= minWallContactTime:
                    _jumpButtonHeld = true;
                    _jumpChargeStartTime = Time.time;
                    JumpState = JumpStateEnum.WallCharging;
                    _jumpFullyCharged = false;
                    _jumpChargeUIManager.SetChargingState(true, 0f, false);
                    animator.SetBool(IsJumpQueued, true);
                    break;
                case false:
                {
                    _jumpButtonHeld = true;
                    _jumpChargeStartTime = Time.time;
                    _jumpChargeDirection = (int)Sign(_rb.linearVelocity.x);
                    _startedChargingOnGround = IsGrounded;
                    _releaseJumpInAir = false;

                    JumpState = _startedChargingOnGround ? JumpStateEnum.Charging : JumpStateEnum.Buffered;
                    _jumpFullyCharged = false;

                    if (_startedChargingOnGround)
                    {
                        _movementDisabledForJump = true;
                        animator.SetBool(IsJumpQueued, true);
                    }

                    _jumpChargeUIManager.SetChargingState(true, 0f, false);
                    break;
                }
            }

        if (!_playerInputActions.Player.Jump.WasReleasedThisFrame() || !_jumpButtonHeld) return;

        _jumpButtonHeld = false;

        switch (JumpState)
        {
            case JumpStateEnum.Charging:
            case JumpStateEnum.Buffered:
                var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
                _jumpChargeLevel = chargeTime;

                if (IsGrounded)
                {
                    ExecuteJump(chargeTime);
                }
                else
                {
                    _releaseJumpInAir = true;
                    _jumpChargeUIManager.SetChargingState(false, 0f, false);
                }
                break;

            case JumpStateEnum.WallCharging:
                var wallChargeTime = Min(Time.time - _jumpChargeStartTime, maxWallChargeTime);
                ExecuteWallJump(wallChargeTime, horizontalInput);
                break;
        }
    }

    /// Checks if a jump should execute on landing
    private void CheckJumpLanding(bool wasGrounded, bool isGroundedNow)
    {
        if (!wasGrounded && IsGrounded)
        {
            spriteTransform.rotation = Quaternion.identity;
            spriteTransform.localPosition = _originalSpritePosition;
            _postWallJump = false;
            _isInBounceWindow = false;
        }

        if (wasGrounded || !isGroundedNow) return;

        if (JumpState != JumpStateEnum.Charging && JumpState != JumpStateEnum.Buffered) return;

        if (_startedChargingOnGround)
        {
            var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
            ExecuteJump(chargeTime);
            return;
        }

        if (JumpState == JumpStateEnum.Buffered)
        {
            animator.SetBool(IsJumpQueued, true);
            if (!_jumpButtonHeld)
            {
                ExecuteJump(_jumpFullyCharged ? maxChargeTime : _jumpChargeLevel);
            }
            else
            {
                JumpState = JumpStateEnum.Charging;
                _startedChargingOnGround = true;
                _movementDisabledForJump = true;
            }
            return;
        }

        if (_jumpButtonHeld)
        {
            _startedChargingOnGround = true;
            _movementDisabledForJump = true;
            animator.SetBool(IsJumpQueued, true);
        }
        else if (_releaseJumpInAir)
        {
            animator.SetBool(IsJumpQueued, true);
            ExecuteJump(_jumpFullyCharged ? maxChargeTime : _jumpChargeLevel);
            _releaseJumpInAir = false;
        }
        else
        {
            JumpState = JumpStateEnum.Idle;
            _jumpChargeUIManager.SetChargingState(false, 0f, false);
        }
    }

    /// Performs the actual jump with calculated force
    private void ExecuteJump(float chargeTime)
    {
        JumpState = JumpStateEnum.Idle;
        _movementDisabledForJump = false;
        _releaseJumpInAir = false;
        _startedChargingOnGround = false;

        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        _jumpChargeUIManager.ForceUIStateSync();

        var chargeProgress = Clamp01(chargeTime / maxChargeTime);
        var jumpMultiplier = Lerp(minJumpForce, maxJumpForce, chargeProgress);

        if (HasCatnip)
            jumpMultiplier *= catnipJumpMultiplier;

        _cameraController.TriggerJumpFOV();

        ResetAccelerationState();

        var currentHorizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
        var currentInputDirection = (int)Sign(currentHorizontalInput);

        var isOppositeDirection = _jumpChargeDirection != 0 && currentInputDirection != 0 && currentInputDirection != _jumpChargeDirection;

        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0);
        if (isOppositeDirection)
        {
            _rb.linearVelocity = new Vector2(0, jumpMultiplier);
            currentSpeed = 0;
        }
        else
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpMultiplier);
        }

        _isInBounceWindow = true;
        _bounceWindowEndTime = Time.time + wallBounceWindow;
        _hasBounced = false;

        _lastJumpTime = Time.time;
        animator.SetBool(IsJumpQueued, false);
    }

    /// Performs wall jump with calculated force
    private void ExecuteWallJump(float chargeTime, float currentHorizontalInput)
    {
        JumpState = JumpStateEnum.Idle;
        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        animator.SetBool(IsJumpQueued, false);

        _postWallJump = true;

        var chargeProgress = Clamp01(chargeTime / maxWallChargeTime);
        var jumpMultiplier = Lerp(minWallJumpForce, maxWallJumpForce, chargeProgress);

        if (HasCatnip)
            jumpMultiplier *= catnipJumpMultiplier;

        _cameraController.TriggerJumpFOV();

        ResetAccelerationState();

        var currentInputDirection = (int)Sign(currentHorizontalInput);
        var applyDetachForce = currentInputDirection != 0 && currentInputDirection != _wallSlideSide;

        if (applyDetachForce)
        {
            _rb.linearVelocity = new Vector2(-_wallSlideSide * wallDetachForce, jumpMultiplier);
            _postWallJump = false;

            spriteTransform.rotation = Quaternion.identity;
            spriteTransform.localPosition = _originalSpritePosition;
        }
        else
        {
            _rb.linearVelocity = new Vector2(0, jumpMultiplier);
        }

        currentSpeed = _rb.linearVelocity.x;
        _lastJumpTime = Time.time;
    }

    /// Cancels the jump charge process
    private void CancelJumpCharge()
    {
        if (JumpState == JumpStateEnum.Idle)
            return;

        JumpState = JumpStateEnum.Idle;

        _movementDisabledForJump = false;
        _jumpButtonHeld = false;
        _releaseJumpInAir = false;
        _startedChargingOnGround = false;

        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        _jumpChargeUIManager.ForceUIStateSync();
        animator.SetBool(IsJumpQueued, false);

        if (!_isWallSliding && spriteTransform.localPosition == _originalSpritePosition) return;

        spriteTransform.rotation = Quaternion.identity;
        spriteTransform.localPosition = _originalSpritePosition;
    }
    #endregion

    #region Jump Charge Visualization
    /// Updates jump charge progress and visuals with network synchronization
    private void UpdateJumpCharging()
    {
        if (!_jumpButtonHeld) return;

        var chargeProgress = 0f;
        var fullCharged = false;

        switch (JumpState)
        {
            case JumpStateEnum.Charging:
            case JumpStateEnum.Buffered:
                var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
                chargeProgress = Clamp01(chargeTime / maxChargeTime);

                if (_cameraController)
                    _cameraController.UpdateChargingJumpFOV(chargeProgress);

                if (chargeTime >= maxChargeTime)
                {
                    _jumpFullyCharged = true;
                    _jumpChargeLevel = maxChargeTime;
                    fullCharged = true;
                }
                break;

            case JumpStateEnum.WallCharging:
                var wallChargeTime = Min(Time.time - _jumpChargeStartTime, maxWallChargeTime);
                chargeProgress = Clamp01(wallChargeTime / maxWallChargeTime);

                if (_cameraController)
                    _cameraController.UpdateChargingJumpFOV(chargeProgress);

                if (wallChargeTime >= maxWallChargeTime)
                {
                    _jumpFullyCharged = true;
                    _jumpChargeLevel = maxWallChargeTime;
                    fullCharged = true;
                }
                break;
        }

        // Update the UI with current charge state
        _jumpChargeUIManager.SetChargingState(true, chargeProgress, fullCharged);
    }

    /// Syncs jump state to other players
    private void SyncJumpState()
    {
        if (!photonView.IsMine) return;

        if (_previousJumpState == JumpState) return;

        if (JumpState == JumpStateEnum.Idle)
        {
            photonView.RPC(nameof(_jumpChargeUIManager.RPC_EndJumpCharge), RpcTarget.Others);
        }
        else if (_previousJumpState == JumpStateEnum.Idle)
        {
            var maxChargeTimeToUse = JumpState == JumpStateEnum.WallCharging ? maxWallChargeTime : maxChargeTime;
            photonView.RPC(nameof(_jumpChargeUIManager.RPC_StartJumpCharge), RpcTarget.Others, (int)JumpState, maxChargeTimeToUse);
        }

        _previousJumpState = JumpState;
    }

    /// Sets this player as being spectated or not
    internal void SetSpectatedState()
    {
        if (_jumpChargeUIManager)
            _jumpChargeUIManager.SetSpectatedState();
    }

    /// Called when this player starts being spectated
    internal void OnStartSpectating()
    {
        if (photonView.IsMine && JumpState != JumpStateEnum.Idle)
            SyncJumpState();
    }
    #endregion

    #region Animations
    /// Checks if player is idle for animations
    private void CheckIdleState()
    {
        if (_isWallSliding || JumpState == JumpStateEnum.WallCharging)
        {
            _idleTimer = 0f;
            animator.SetBool(IsLaying, false);
            return;
        }

        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
        var anyMovementKeyPressed = Abs(horizontalInput) > 0.01f;
        var jumpKeyPressed = _playerInputActions.Player.Jump.IsPressed();

        if (anyMovementKeyPressed || jumpKeyPressed)
        {
            _idleTimer = 0f;
            animator.SetBool(IsLaying, false);
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

    /// Updates animation states based on player status
    private void UpdateAnimations()
    {
        var wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayerMask) || IsDead;

        if (wasGrounded != IsGrounded)
            CheckJumpLanding(wasGrounded, IsGrounded);

        if (wasGrounded && !IsGrounded)
            switch (JumpState)
            {
                case JumpStateEnum.Charging when _startedChargingOnGround:
                {
                    var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
                    ExecuteJump(chargeTime);
                    break;
                }
                case JumpStateEnum.Charging when !_jumpButtonHeld && !_releaseJumpInAir:
                    JumpState = JumpStateEnum.Idle;
                    _jumpChargeUIManager.SetChargingState(false, 0f, false);
                    _jumpChargeUIManager.ForceUIStateSync();
                    animator.SetBool(IsJumpQueued, false);
                    break;
            }

        if (wasGrounded && !IsGrounded && (JumpState == JumpStateEnum.Charging && _startedChargingOnGround))
        {
            var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
            ExecuteJump(chargeTime);
        }
        else if (!IsGrounded && (JumpState == JumpStateEnum.Charging && _startedChargingOnGround))
        {
            CancelJumpCharge();
        }

        if (IsGrounded && IsJumpPaused)
        {
            animator.speed = 1f;
            IsJumpPaused = false;
        }

        if (!photonView.IsMine) return;

        animator.SetBool(InAir, !IsGrounded);
        animator.SetFloat(Speed, Abs(_rb.linearVelocity.x));
    }
    #endregion

    #region Utility
    /// Resets acceleration-related variables
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

    /// Moves player to specified position
    internal void Teleport(Vector3 position)
    {
        if (!photonView.IsMine)
            return;

        transform.position = position;
        spriteTransform.rotation = Quaternion.identity;
        spriteTransform.localPosition = _originalSpritePosition;
        ResetAccelerationState();
        currentSpeed = 0f;

        if (_cameraController)
            _cameraController.OnPlayerRespawn();
    }

    /// Teleports player to specified position without resetting camera
    internal void TeleportWithoutCameraReset(Vector3 position)
    {
        if (!photonView.IsMine)
            return;

        transform.position = position;
        spriteTransform.rotation = Quaternion.identity;
        spriteTransform.localPosition = _originalSpritePosition;
        ResetAccelerationState();
        currentSpeed = 0f;
    }

    /// Enables or disables player movement
    internal void SetMovement(bool isEnabled)
    {
        IsPaused = !isEnabled;
    }

    /// Disables physics simulation
    internal void DisableRigidbody()
    {
        _rb.simulated = false;
    }

    /// Enables physics simulation
    internal void EnableRigidbody()
    {
        _rb.simulated = true;
    }

    /// Sets player to spectator mode
    internal void SetSpectatorMode(bool isEnabled)
    {
        SetMovement(!isEnabled);
    }

    /// Handles catnip effect RPC call
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
    /// Handles player death effects
    internal void OnPlayerDeath()
    {
        if (IsDead)
            return;

        IsDead = true;
        _rb.gravityScale = 0;
        _rb.linearVelocity = zero;
        _rb.constraints = RigidbodyConstraints2D.FreezeAll;

        spriteTransform.rotation = Quaternion.identity;
        spriteTransform.localPosition = _originalSpritePosition;

        if (_cameraController)
            _cameraController.OnPlayerDeath();
    }

    /// Respawns player at last checkpoint
    internal void RespawnAtLastCheckpoint()
    {
        CheckpointManager.IsRespawning = true;
        Teleport(CheckpointManager.LastCheckpointPosition);
        OnPlayerRespawn();

        Invoke(nameof(ResetRespawnFlag), 0.5f);
    }

    /// Resets respawn flag after delay
    private void ResetRespawnFlag()
    {
        CheckpointManager.IsRespawning = false;
    }

    /// Handles player respawn effects
    private void OnPlayerRespawn()
    {
        if (!IsDead)
            return;

        IsDead = false;
        _rb.gravityScale = defaultGravityScale;
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        if (_cameraController)
            _cameraController.OnPlayerRespawn();

        CancelJumpCharge();
    }
    #endregion
}
