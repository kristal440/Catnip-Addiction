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
    [SerializeField] [Tooltip("Reference to the player's animator component")] internal Animator animator;
    [SerializeField] [Tooltip("Reference to the player's sprite renderer")] internal SpriteRenderer playerSprite;

    [Header("Movement")]
    [SerializeField] [Tooltip("How quickly player accelerates")] internal AnimationCurve accelerationCurve = new(new Keyframe(0f, 0.3f), new Keyframe(0.6f, 0.7f), new Keyframe(1f, 1f));
    [SerializeField] [Tooltip("Base acceleration value")] internal float baseAcceleration = 10f;
    [SerializeField] [Range(0.2f, 2.0f)] [Tooltip("How long it takes to reach full acceleration")] internal float accelerationTime = 0.8f;
    [SerializeField] [Tooltip("How quickly player slows down")] internal float deceleration = 15f;
    [SerializeField] [Tooltip("Maximum movement speed")] internal float maxSpeed = 5f;
    [SerializeField] [Tooltip("Higher speed reached after maintaining max speed")] internal float turboSpeed = 7f;
    [SerializeField] [Tooltip("Time in seconds player needs to maintain max speed before reaching turbo speed")] internal float timeToTurboSpeed = 1.5f;
    [SerializeField] [Range(0.7f, 0.99f)] [Tooltip("Threshold to consider player at max speed (0-1)")] internal float maxSpeedThreshold = 0.98f;

    [Header("Ground Check")]
    [SerializeField] [Tooltip("Transform used to detect ground")] internal Transform groundCheck;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Radius of the ground check sphere")] internal float groundCheckRadius = 0.2f;
    [SerializeField] [Tooltip("Layer mask for ground detection")] internal LayerMask groundLayerMask;

    [Header("Wall Check")]
    [SerializeField] [Tooltip("Transform used to detect left wall")] internal Transform leftWallCheck;
    [SerializeField] [Tooltip("Transform used to detect right wall")] internal Transform rightWallCheck;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Radius of the wall check sphere")] internal float wallCheckRadius = 0.2f;
    [SerializeField] [Tooltip("Layer mask for wall detection")] internal LayerMask wallLayerMask;
    [SerializeField] [Range(0f, 1f)] [Tooltip("Distance for wall raycast detection")] internal float wallRaycastDistance = 0.1f;

    [Header("Wall Sliding")]
    [SerializeField] [Range(0.1f, 5f)] [Tooltip("Reduced falling speed when sliding on walls")] internal float wallSlideSpeed = 2f;
    [SerializeField] [Range(0.1f, 5f)] [Tooltip("Force to apply against the wall")] private float wallStickForce = 1.5f;
    [SerializeField] [Tooltip("Transform of the visual object to rotate during wall slide")] internal Transform spriteTransform;
    [SerializeField] [Range(-0.2f, 0.05f)] [Tooltip("X offset of sprite during wall sliding")] internal float wallSlideOffset = -0.06f;
    [SerializeField] [Range(0.1f, 1.0f)] [Tooltip("Time threshold to detect being stuck against a wall")] internal float wallStuckThreshold = 0.3f;
    [SerializeField] [Range(0.01f, 0.5f)] [Tooltip("Maximum velocity to consider player as stuck")] internal float stuckVelocityThreshold = 0.1f;

    [Header("UI")]
    [SerializeField] [Tooltip("Player name text display")] internal TextMeshProUGUI playerNameTag;

    [Header("Death")]
    [SerializeField] [Tooltip("Y-position that triggers death when fallen below")] internal float deathHeight = -50f;
    [SerializeField] [Tooltip("Handler for player death events")] private PlayerDeathHandler playerDeathHandler;

    [Header("Visuals")]
    [SerializeField] [Tooltip("Sorting order for remote player sprites")] private int remotePlayerSpriteOrder = 2;
    [SerializeField] [Tooltip("Sorting order for remote player UI canvas")] private int remotePlayerCanvasOrder = 3;
    [SerializeField] [Range(0.1f, 1f)] [Tooltip("Alpha transparency for remote players (0-1)")] private float remotePlayerAlpha = 0.7f;

    [Header("Idle Behavior")]
    [SerializeField] [Tooltip("Time in seconds before triggering laying animation")] internal float idleToLayingTime = 3.0f;

    [Header("Catnip Effects")]
    [SerializeField] [Range(1.0f, 2.0f)] [Tooltip("Speed multiplier when catnip is active")] internal float catnipSpeedMultiplier = 1.1f;
    [SerializeField] [Range(1.0f, 2.0f)] [Tooltip("Jump force multiplier when catnip is active")] internal float catnipJumpMultiplier = 1.1f;
    [SerializeField] [Range(0.5f, 1.0f)] [Tooltip("Deceleration multiplier when catnip is active")] internal float catnipDecelerationMultiplier = 0.9f;

    [Header("Physics")]
    [SerializeField] [Range(0.5f, 3.0f)] [Tooltip("Normal gravity scale for the player")] internal float defaultGravityScale = 1.0f;

    // Animator variables
    private static readonly int Speed = Animator.StringToHash("Speed");
    private static readonly int InAir = Animator.StringToHash("InAir");
    private static readonly int IsLaying = Animator.StringToHash("IsLaying");
    private static readonly int IsWallSlidingAnimVar = Animator.StringToHash("IsWallSliding");

    // Component references
    private Camera _mainCamera;
    private Rigidbody2D _rb;
    private Collider2D _collider;
    private InputSystem_Actions _playerInputActions;
    private SpriteFlipManager _spriteFlipManager;

    // helper scripts
    internal JumpSystem JumpSystem;
    internal CatnipFx CatnipFx;
    internal DynamicCameraController CameraController;

    // Internal state variables
    private float _idleTimer;
    private bool _wallCollisionHandled;
    private float _defaultGravityScaleCache;
    private float _newMaxSpeed;
    private float _newDeceleration;
    private float _currentAccelTime;
    private float _timeAtMaxSpeed;
    private bool _isTurboActive;
    private int _lastMoveDirection;
    private float _wallStuckTimer;
    private bool _isPotentiallyStuck;

    // Properties
    internal float CurrentSpeed;
    internal float VerticalSpeed;
    internal bool IsTouchingLeftWall;
    internal bool IsTouchingRightWall;
    internal bool IsWallSliding;
    internal int WallSlideSide; // -1 for left, 1 for right
    internal float WallContactTime;
    internal Vector3 OriginalSpritePosition;
    internal bool PostWallJump; // Flag to track post-wall-jump state
    private int _lastWallSlideSideRPC;

    internal bool IsStanding { get; set; }
    internal bool IsGrounded { get; private set; }
    internal bool IsJumpPaused { get; set; }
    internal bool IsPaused { get; set; }
    internal bool HasCatnip { get; set; }
    internal bool IsDead { get; private set; }
    private bool IsTouchingWall => IsTouchingLeftWall || IsTouchingRightWall;
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
        base.OnEnable();
        _playerInputActions.Player.Enable();
    }

    /// Disables player input actions
    public override void OnDisable()
    {
        base.OnDisable();
        _playerInputActions.Player.Disable();
    }

    /// Sets up references and configurations
    private void Start()
    {
        _mainCamera = Camera.main;
        _rb = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        CatnipFx = GetComponent<CatnipFx>();
        _spriteFlipManager = GetComponent<SpriteFlipManager>();
        JumpSystem = GetComponent<JumpSystem>();

        if (spriteTransform == null)
            spriteTransform = playerSprite.transform;

        OriginalSpritePosition = spriteTransform.localPosition;

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

        VerticalSpeed = _rb.linearVelocity.y;
        HandleWallSliding();
        CheckIdleState();
        HandleMovement();

        if (transform.position.y < deathHeight && !IsDead)
            playerDeathHandler.HandleOutOfBoundsDeath();

        if (PostWallJump && IsTouchingWall)
        {
            var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
            var moveDirection = (int)Sign(horizontalInput);

            if (moveDirection != 0 && moveDirection != WallSlideSide)
            {
                PostWallJump = false;
                spriteTransform.rotation = Quaternion.identity;
                spriteTransform.localPosition = OriginalSpritePosition;
            }
        }

        if (PostWallJump && !IsTouchingWall)
            PostWallJump = false;

        // Update the jump system
        JumpSystem.UpdateJumpSystem();
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
        CameraController = _mainCamera.GetComponent<DynamicCameraController>();
        if (CameraController == null)
            CameraController = _mainCamera.gameObject.AddComponent<DynamicCameraController>();
    }
    #endregion

    #region Wall Detection and Sliding
    /// Checks for wall contact on both sides using transform checks and collider contacts
    private void CheckWallContact()
    {
        var wasLeftWall = IsTouchingLeftWall;
        var wasRightWall = IsTouchingRightWall;

        var leftTransformCheck = Physics2D.OverlapCircle(leftWallCheck.position, wallCheckRadius, wallLayerMask);
        var rightTransformCheck = Physics2D.OverlapCircle(rightWallCheck.position, wallCheckRadius, wallLayerMask);

        var colliderBounds = _collider.bounds;

        var leftRayOrigin = new Vector2(colliderBounds.min.x, colliderBounds.center.y);
        var leftColliderCheck = Physics2D.Raycast(leftRayOrigin, left, wallRaycastDistance, wallLayerMask);
        Debug.DrawRay(leftRayOrigin, left * wallRaycastDistance, leftColliderCheck ? Color.red : Color.green);

        var rightRayOrigin = new Vector2(colliderBounds.max.x, colliderBounds.center.y);
        var rightColliderCheck = Physics2D.Raycast(rightRayOrigin, right, wallRaycastDistance, wallLayerMask);
        Debug.DrawRay(rightRayOrigin, right * wallRaycastDistance, rightColliderCheck ? Color.red : Color.green);

        IsTouchingLeftWall = leftTransformCheck && leftColliderCheck;
        IsTouchingRightWall = rightTransformCheck && rightColliderCheck;

        if (!wasLeftWall && IsTouchingLeftWall)
        {
            WallSlideSide = -1;
            WallContactTime = 0f;
        }
        else if (!wasRightWall && IsTouchingRightWall)
        {
            WallSlideSide = 1;
            WallContactTime = 0f;
        }

        if (IsTouchingWall)
        {
            WallContactTime += Time.deltaTime;
            JumpSystem.OnWallContactStart();
        }

        if (IsGrounded && JumpSystem.JumpState == JumpSystem.JumpStateEnum.WallCharging)
            JumpSystem.CancelWallJumpWithGroundPush();

        if ((IsTouchingLeftWall || IsTouchingRightWall) &&
            JumpSystem.JumpState is JumpSystem.JumpStateEnum.Charging or JumpSystem.JumpStateEnum.Buffered &&
            !JumpSystem.GetStartedChargingOnGround())
            JumpSystem.ConvertToWallJump();
    }

    /// Handles wall sliding physics and visuals for both local and remote players
    private void HandleWallSliding()
    {
        var wasWallSliding = IsWallSliding;

        if (photonView.IsMine)
        {
            var wallSlideCondition = IsTouchingWall && !IsGrounded && (
                _rb.linearVelocity.y < 0 ||
                PostWallJump ||
                _isPotentiallyStuck);

            IsWallSliding = wallSlideCondition;

            if (IsWallSliding)
            {
                if (IsTouchingLeftWall)
                    WallSlideSide = -1;
                else if (IsTouchingRightWall)
                    WallSlideSide = 1;
            }

            if (IsWallSliding)
            {
                if (_rb.linearVelocity.y < 0 || _isPotentiallyStuck)
                {
                    _rb.linearVelocity = new Vector2(WallSlideSide * wallStickForce, Max(_rb.linearVelocity.y, -wallSlideSpeed));

                    if (PostWallJump)
                        PostWallJump = false;

                    // Reset stuck status
                    _isPotentiallyStuck = false;
                    _wallStuckTimer = 0f;
                }
            }
            else if (JumpSystem.JumpState == JumpSystem.JumpStateEnum.WallCharging)
            {
                _rb.linearVelocity = new Vector2(WallSlideSide * wallStickForce, _rb.linearVelocity.y);
            }

            if (wasWallSliding != IsWallSliding || (IsWallSliding && _lastWallSlideSideRPC != WallSlideSide))
            {
                photonView.RPC(nameof(RPC_SyncWallSlideVisuals), RpcTarget.Others, IsWallSliding, WallSlideSide);
                _lastWallSlideSideRPC = WallSlideSide;
            }
        }

        UpdateWallSlideVisuals(IsWallSliding, WallSlideSide);

        animator.SetBool(IsWallSlidingAnimVar, IsWallSliding);
    }

    /// Updates wall sliding visuals for both local and remote players
    private void UpdateWallSlideVisuals(bool isSliding, int slideSide)
    {
        if (isSliding || JumpSystem.JumpState == JumpSystem.JumpStateEnum.WallCharging)
        {
            var shouldFaceRight = slideSide > 0;
            if (shouldFaceRight != _spriteFlipManager.IsFacingRight() && photonView.IsMine)
                FlipPlayerSprite();

            spriteTransform.rotation = Quaternion.Euler(0, 0, slideSide * 90f);
            var offsetPosition = OriginalSpritePosition;
            offsetPosition.x += slideSide * wallSlideOffset;
            spriteTransform.localPosition = offsetPosition;
        }
        else if (spriteTransform.rotation != Quaternion.identity || spriteTransform.localPosition != OriginalSpritePosition)
        {
            spriteTransform.rotation = Quaternion.identity;
            spriteTransform.localPosition = OriginalSpritePosition;
        }
    }

    /// RPC to sync wall sliding visuals to remote players
    [PunRPC]
    private void RPC_SyncWallSlideVisuals(bool isSliding, int slideSide)
    {
        if (photonView.IsMine) return;

        IsWallSliding = isSliding;
        WallSlideSide = slideSide;

        UpdateWallSlideVisuals(isSliding, slideSide);
    }
    #endregion

    #region Movement
    /// Processes player movement input
    private void HandleMovement()
    {
        if (IsPaused)
            return;

        JumpSystem.HandleJumpInput();

        if (JumpSystem.IsMovementDisabledForJump())
            return;

        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;

        if (!IsWallSliding && JumpSystem.JumpState != JumpSystem.JumpStateEnum.WallCharging)
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

        if (IsWallSliding || JumpSystem.JumpState == JumpSystem.JumpStateEnum.WallCharging)
        {
            _rb.linearVelocity = new Vector2(0, _rb.linearVelocity.y);
            return;
        }

        // Apply movement modifiers from JumpSystem if charging on ground
        var (speedMultiplier, accelMultiplier) = JumpSystem.GetJumpChargeMovementMultipliers();
        _newMaxSpeed *= speedMultiplier;
        currentTurboSpeed *= speedMultiplier;

        if (JumpSystem.JumpState == JumpSystem.JumpStateEnum.Charging && JumpSystem.GetStartedChargingOnGround() && JumpSystem.IsMovementDisabledForJump())
        {
            _rb.linearVelocity = new Vector2(CurrentSpeed, _rb.linearVelocity.y);
            return;
        }

        // Check for stuck-against-wall condition
        var moveDirection = (int)Sign(horizontalInput);
        var isPressingAgainstWall = (horizontalInput < 0 && IsTouchingLeftWall) || (horizontalInput > 0 && IsTouchingRightWall);

        if (isPressingAgainstWall)
        {
            // Only check for stuck condition if not already wall sliding
            if (Abs(_rb.linearVelocity.x) < stuckVelocityThreshold && !IsGrounded && !IsWallSliding)
            {
                _wallStuckTimer += Time.deltaTime;

                if (_wallStuckTimer >= wallStuckThreshold)
                    _isPotentiallyStuck = true;
            }

            CurrentSpeed = MoveTowards(CurrentSpeed, 0, _newDeceleration * Time.deltaTime);
            _rb.linearVelocity = new Vector2(CurrentSpeed, _rb.linearVelocity.y);
            return;
        }
        else
        {
            // Reset stuck detection
            _wallStuckTimer = 0f;
            _isPotentiallyStuck = false;
        }

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

                var accelCurveMultiplier = accelerationCurve.Evaluate(_currentAccelTime / accelerationTime);
                var currentAccel = baseAcceleration * accelCurveMultiplier * accelMultiplier;

                var targetSpeed = _newMaxSpeed;

                if (Abs(CurrentSpeed) >= _newMaxSpeed * maxSpeedThreshold && Approximately(Sign(CurrentSpeed), moveDirection))
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

                CurrentSpeed = MoveTowards(CurrentSpeed, horizontalInput * targetSpeed, currentAccel * Time.deltaTime);
                break;

            case <= 0.01f:
            {
                _currentAccelTime = 0f;
                _timeAtMaxSpeed = 0f;
                _isTurboActive = false;
                _lastMoveDirection = 0;

                CurrentSpeed = MoveTowards(CurrentSpeed, 0, _newDeceleration * Time.deltaTime);
                break;
            }
        }

        _rb.linearVelocity = new Vector2(CurrentSpeed, _rb.linearVelocity.y);
    }
    #endregion

    #region Animations
    /// Checks if player is idle for animations
    private void CheckIdleState()
    {
        if (IsWallSliding || JumpSystem.JumpState == JumpSystem.JumpStateEnum.WallCharging)
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

        if (Approximately(CurrentSpeed, 0f) && Approximately(VerticalSpeed, 0f) && animator.GetBool(IsLaying) == false)
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
            JumpSystem.CheckJumpLanding(wasGrounded, IsGrounded);

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

    /// Moves player to specified position
    internal void Teleport(Vector3 position)
    {
        if (!photonView.IsMine)
            return;

        transform.position = position;
        spriteTransform.rotation = Quaternion.identity;
        spriteTransform.localPosition = OriginalSpritePosition;
        ResetAccelerationState();
        JumpSystem.CancelJumpCharge();
        CurrentSpeed = 0f;

        if (CameraController)
            CameraController.OnPlayerRespawn();
    }

    /// Teleports player to specified position without resetting camera
    internal void TeleportWithoutCameraReset(Vector3 position)
    {
        if (!photonView.IsMine)
            return;

        transform.position = position;
        spriteTransform.rotation = Quaternion.identity;
        spriteTransform.localPosition = OriginalSpritePosition;
        ResetAccelerationState();
        JumpSystem.CancelJumpCharge();
        CurrentSpeed = 0f;
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
            CatnipFx.ActivateCatnipEffect();
        else
            CatnipFx.DeactivateCatnipEffect();
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
        spriteTransform.localPosition = OriginalSpritePosition;
        photonView.RPC(nameof(RPC_SetCatnipEffectActive), RpcTarget.All, false);

        if (CameraController)
            CameraController.OnPlayerDeath();
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

        if (CameraController)
            CameraController.OnPlayerRespawn();

        JumpSystem.CancelJumpCharge();
    }
    #endregion

    #region Spectating
    /// Sets this player as being spectated
    internal void SetSpectatedState()
    {
        JumpSystem.SetSpectatedState();
    }

    /// Called when this player starts being spectated
    internal void OnStartSpectating()
    {
        JumpSystem.OnStartSpectating();
    }
    #endregion
}
