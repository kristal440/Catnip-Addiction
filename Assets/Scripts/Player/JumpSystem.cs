using Photon.Pun;
using UnityEngine;
using static UnityEngine.Mathf;

/// <inheritdoc />
/// <summary>
/// Helper script for PlayerController that handles all jump-related mechanics using a state-driven approach
/// </summary>
public class JumpSystem : MonoBehaviourPunCallbacks
{
    #region Variables
    [Header("References")]
    [SerializeField] [Tooltip("Skin Animator component")] private Animator animator;

    [Header("Charged Jump")]
    [SerializeField] [Tooltip("Minimum jump force when uncharged")] internal float minJumpForce = 8.5f;
    [SerializeField] [Tooltip("Maximum jump force when fully charged")] internal float maxJumpForce = 14f;
    [SerializeField] [Range(0.1f, 2f)] [Tooltip("Maximum time to charge regular jump")] internal float maxChargeTime = 2f;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Time before player can jump again")] internal float jumpCooldown = 0.1f;

    [Header("Jump Movement Control")]
    [SerializeField] [Tooltip("Whether movement is allowed during jump charging")] internal bool allowMovementDuringCharge;
    [SerializeField] [Range(0.1f, 1f)] [Tooltip("Movement speed multiplier during jump charging (0.1-1)")] internal float chargeMovementSpeedMultiplier = 0.5f;
    [SerializeField] [Range(0.1f, 1f)] [Tooltip("Acceleration multiplier during jump charging (0.1-1)")] internal float chargeAccelerationMultiplier = 0.5f;
    [SerializeField] [Tooltip("Whether to gradually reduce movement as charge increases")] internal bool reduceMovementWithCharge = true;
    [SerializeField] [Range(0.1f, 0.9f)] [Tooltip("Minimum movement multiplier at full charge")] internal float minMovementMultiplierAtFullCharge = 0.2f;

    [Header("Wall Jumping")]
    [SerializeField] [Tooltip("Minimum jump force when uncharged")] internal float minWallJumpForce = 10f;
    [SerializeField] [Tooltip("Maximum jump force when fully charged")] internal float maxWallJumpForce = 16f;
    [SerializeField] [Range(0.1f, 2f)] [Tooltip("Maximum time to charge wall jump")] internal float maxWallChargeTime = 1f;
    [SerializeField] [Tooltip("Force applied perpendicular to the wall")] internal float wallDetachForce = 8f;
    [SerializeField] [Tooltip("Force pushing player away when reaching ground during wall jump charge")] internal float wallGroundPushForce = 5f;
    [SerializeField] [Tooltip("Minimum time player needs to be on wall before can wall jump")] internal float minWallContactTime = 0.05f;

    [Header("Wall Bounce")]
    [SerializeField] [Range(0f, 2f)] [Tooltip("Time window after a charged jump when wall bounces are enabled")] internal float wallBounceWindow = 0.5f;
    [SerializeField] [Range(1f, 15f)] [Tooltip("Force applied when bouncing off a wall")] internal float wallBounceForce = 8f;
    [SerializeField] [Range(0f, 2f)] [Tooltip("Vertical force multiplier for wall bounces")] internal float wallBounceVerticalMultiplier = 0.5f;
    [SerializeField] [Tooltip("Whether bounce preserves some of the player's momentum")] internal bool preserveMomentumOnBounce = true;
    [SerializeField] [Range(0f, 1f)] [Tooltip("How much of the player's momentum is preserved during a bounce (0-1)")] internal float momentumPreservation = 0.7f;

    // Animator variables
    private static readonly int IsJumpQueued = Animator.StringToHash("IsJumpQueued");

    // Component references
    private PlayerController _playerController;
    private JumpChargeUIManager _jumpChargeUIManager;
    private InputSystem_Actions _playerInputActions;
    private Rigidbody2D _rb;
    private DynamicCameraController _cameraController;
    private PhotonView _photonView;

    // State machine
    internal enum JumpStateEnum
    {
        Idle, // Not jumping
        Charging, // Charging jump on ground
        Buffered, // Buffered jump in air
        WallCharging, // Charging wall jump
        Bouncing // In bounce window
    }

    // Jump state properties
    internal JumpStateEnum JumpState { get; private set; } = JumpStateEnum.Idle;
    internal float StoredChargeProgress { get; private set; }
    internal bool HasBufferedChargeInAir => StoredChargeProgress > 0 && JumpState == JumpStateEnum.Idle;

    // Internal state tracking
    private float _jumpChargeStartTime;
    private float _lastJumpTime;
    private float _bounceWindowEndTime;
    private float _currentChargeProgress;
    private int _jumpChargeDirection;
    private bool _jumpButtonHeld;
    private bool _movementDisabledForJump;
    private bool _hasBounced;
    private bool _jumpFullyCharged;
    private JumpStateEnum _previousJumpState;
    private bool _releaseJumpInAir;
    private float _timeLeftGround;
    private bool _wasGroundedLastFrame;
    private bool _delayWallJumpQueuedReset;

    #endregion

    #region Unity Lifecycle

    /// Initialize component references and input system
    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _jumpChargeUIManager = GetComponent<JumpChargeUIManager>();
        _playerInputActions = new InputSystem_Actions();
        _rb = GetComponent<Rigidbody2D>();
        _photonView = GetComponent<PhotonView>();

        if (Camera.main != null)
            _cameraController = Camera.main.GetComponent<DynamicCameraController>();

        _previousJumpState = JumpState;
    }

    /// Enable input system
    public override void OnEnable()
    {
        _playerInputActions.Player.Enable();
    }

    /// Disable input system
    public override void OnDisable()
    {
        _playerInputActions.Player.Disable();
    }

    /// Update jump charging and bounce mechanics
    internal void UpdateJumpSystem()
    {
        if (!_photonView.IsMine) return;

        if (JumpState != _previousJumpState)
        {
            SyncJumpState();
            _previousJumpState = JumpState;
        }

        _jumpButtonHeld = _playerInputActions.Player.Jump.IsPressed();

        // Track when player left ground for coyote time
        var isGroundedNow = _playerController.IsGrounded;
        if (_wasGroundedLastFrame && !isGroundedNow)
        {
            _timeLeftGround = Time.time;

            // Check if we should auto-execute jump when falling off edge while charging
            if (JumpState == JumpStateEnum.Charging)
            {
                var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
                ExecuteJump(chargeTime);
                return;
            }
        }
        _wasGroundedLastFrame = isGroundedNow;

        if (_delayWallJumpQueuedReset && !_playerController.IsTouchingWall)
        {
            animator.SetBool(IsJumpQueued, false);
            _delayWallJumpQueuedReset = false;
        }

        if (JumpState == JumpStateEnum.WallCharging && !_playerController.IsTouchingWall)
        {
            CancelJumpCharge();
            return;
        }

        UpdateJumpCharging();
        UpdateBounceState();
    }
    #endregion

    #region State Management

    /// Updates the current jump charge progress and related effects
    private void UpdateJumpCharging()
    {
        switch (JumpState)
        {
            case JumpStateEnum.Idle:
                return;
            case JumpStateEnum.Charging:
            case JumpStateEnum.Buffered:
            {
                var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
                _currentChargeProgress = Clamp01(chargeTime / maxChargeTime);

                if (_cameraController)
                    _cameraController.UpdateChargingJumpFOV(_currentChargeProgress);

                if (chargeTime >= maxChargeTime && !_jumpFullyCharged)
                {
                    _jumpFullyCharged = true;
                    StoredChargeProgress = maxChargeTime;
                }

                break;
            }
            case JumpStateEnum.WallCharging:
            {
                var wallChargeTime = Min(Time.time - _jumpChargeStartTime, maxWallChargeTime);
                _currentChargeProgress = Clamp01(wallChargeTime / maxWallChargeTime);

                if (_cameraController)
                    _cameraController.UpdateChargingJumpFOV(_currentChargeProgress);

                if (wallChargeTime >= maxWallChargeTime && !_jumpFullyCharged)
                    _jumpFullyCharged = true;
                break;
            }
        }

        if (_jumpChargeUIManager) _jumpChargeUIManager.SetChargingState(true, _currentChargeProgress, _jumpFullyCharged);
    }

    /// Checks and updates wall bounce state
    private void UpdateBounceState()
    {
        if (JumpState != JumpStateEnum.Bouncing) return;

        if (Time.time > _bounceWindowEndTime)
        {
            JumpState = JumpStateEnum.Idle;
            return;
        }

        if (_playerController.IsWallSliding || _hasBounced) return;

        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
        var moveDirection = (int)Sign(horizontalInput);

        if (CheckBounceCondition(moveDirection)) return;

        // Check for passive bounce with velocity
        if (!(Abs(horizontalInput) < 0.01f)) return;

        var velocityDirection = (int)Sign(_rb.linearVelocity.x);
        CheckBounceCondition(velocityDirection);
    }

    /// Checks if bounce should be applied based on direction
    private bool CheckBounceCondition(int direction)
    {
        switch (direction)
        {
            case < 0 when _playerController.IsTouchingLeftWall:
                ApplyWallBounce(-1);
                _hasBounced = true;
                return true;
            case > 0 when _playerController.IsTouchingRightWall:
                ApplyWallBounce(1);
                _hasBounced = true;
                return true;
            default:
                return false;
        }
    }

    /// Syncs jump state to other players
    private void SyncJumpState()
    {
        if (!_photonView.IsMine) return;

        switch (JumpState)
        {
            case JumpStateEnum.Idle:
                _photonView.RPC(nameof(_jumpChargeUIManager.RPC_EndJumpCharge), RpcTarget.Others);
                break;
            case JumpStateEnum.Charging:
                _photonView.RPC(nameof(_jumpChargeUIManager.RPC_StartJumpCharge), RpcTarget.Others,
                    (int)JumpState, maxChargeTime);
                break;
            case JumpStateEnum.Buffered:
                _photonView.RPC(nameof(_jumpChargeUIManager.RPC_StartJumpCharge), RpcTarget.Others,
                    (int)JumpState, maxChargeTime);
                break;
            case JumpStateEnum.WallCharging:
                _photonView.RPC(nameof(_jumpChargeUIManager.RPC_StartJumpCharge), RpcTarget.Others,
                    (int)JumpState, maxWallChargeTime);
                break;
        }
    }
    #endregion

    #region Jump Input Handling

    /// Processes jump input and initiates jump
    internal void HandleJumpInput()
    {
        if (_playerController.IsPaused)
            return;

        var jumpOnCooldown = Time.time < _lastJumpTime + jumpCooldown;

        // Handle initial jump button press
        if (_playerInputActions.Player.Jump.WasPressedThisFrame() &&
            JumpState == JumpStateEnum.Idle &&
            _playerController.IsStanding &&
            !jumpOnCooldown)
            HandleInitialJumpPress();
        // Handle additional jump presses for buffered jumps
        else if (_playerInputActions.Player.Jump.WasPressedThisFrame() &&
                 JumpState == JumpStateEnum.Idle &&
                 !_playerController.IsGrounded &&
                 HasBufferedChargeInAir &&
                 !jumpOnCooldown)
            ResumeBufferedJump();

        // Handle jump button release
        if (_playerInputActions.Player.Jump.IsPressed() || !_jumpButtonHeld) return;

        _jumpButtonHeld = false;
        HandleJumpButtonRelease();
    }

    /// Handles the initial jump button press
    private void HandleInitialJumpPress()
    {
        if (_playerController.IsWallSliding && _playerController.WallContactTime >= minWallContactTime)
        {
            StartWallJumpCharge();
        }
        else
        {
            var inCoyoteTime = !_playerController.IsGrounded && Time.time - _timeLeftGround <= 0;
            StartRegularJumpCharge(inCoyoteTime);
        }
    }

    /// Starts charging a wall jump
    private void StartWallJumpCharge()
    {
        _jumpChargeStartTime = Time.time;
        JumpState = JumpStateEnum.WallCharging;
        _jumpFullyCharged = false;
        _jumpChargeUIManager.SetChargingState(true, 0f, false);
        animator.SetBool(IsJumpQueued, true);
    }

    /// Starts charging a regular jump
    private void StartRegularJumpCharge(bool inCoyoteTime = false)
    {
        _jumpChargeStartTime = Time.time - StoredChargeProgress;
        _jumpChargeDirection = (int)Sign(_rb.linearVelocity.x);
        var startedChargingOnGround = _playerController.IsGrounded || inCoyoteTime;

        JumpState = startedChargingOnGround ? JumpStateEnum.Charging : JumpStateEnum.Buffered;
        _jumpFullyCharged = false;

        if (startedChargingOnGround)
        {
            _movementDisabledForJump = !allowMovementDuringCharge;
            animator.SetBool(IsJumpQueued, true);

            // For coyote jumps, execute immediately if not on ground
            if (inCoyoteTime && !_playerController.IsGrounded)
            {
                ExecuteJump(StoredChargeProgress > 0 ? StoredChargeProgress : 0);
                StoredChargeProgress = 0f;
                return;
            }
        }

        StoredChargeProgress = startedChargingOnGround ? 0 : StoredChargeProgress;
        _jumpChargeUIManager.SetChargingState(true, StoredChargeProgress / maxChargeTime, false);
    }

    /// Resumes a previously buffered jump
    private void ResumeBufferedJump()
    {
        _jumpChargeStartTime = Time.time - StoredChargeProgress;
        _jumpChargeDirection = (int)Sign(_rb.linearVelocity.x);
        JumpState = JumpStateEnum.Buffered;
        _jumpFullyCharged = StoredChargeProgress >= maxChargeTime;
        _jumpChargeUIManager.SetChargingState(true, StoredChargeProgress / maxChargeTime, _jumpFullyCharged);
    }

    /// Handles jump button release
    private void HandleJumpButtonRelease()
    {
        switch (JumpState)
        {
            case JumpStateEnum.Charging:
                var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
                ExecuteJump(chargeTime);
                StoredChargeProgress = 0f;
                break;

            case JumpStateEnum.Buffered:
                chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
                StoredChargeProgress = chargeTime;

                if (_playerController.IsGrounded)
                {
                    ExecuteJump(chargeTime);
                    StoredChargeProgress = 0f;
                }
                else
                {
                    // Set flag to execute jump on landing if button was released in air
                    _releaseJumpInAir = true;
                    JumpState = JumpStateEnum.Idle;
                    _jumpChargeUIManager.SetChargingState(false, StoredChargeProgress / maxChargeTime, _jumpFullyCharged);
                }
                break;

            case JumpStateEnum.WallCharging:
                var wallChargeTime = Min(Time.time - _jumpChargeStartTime, maxWallChargeTime);
                var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
                ExecuteWallJump(wallChargeTime, horizontalInput);
                break;
        }
    }
    #endregion

    #region Jump Execution

    /// Performs the actual jump with calculated force
    private void ExecuteJump(float chargeTime)
    {
        JumpState = JumpStateEnum.Bouncing;
        _movementDisabledForJump = false;
        StoredChargeProgress = 0f;

        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        _jumpChargeUIManager.ForceUIStateSync();

        var chargeProgress = Clamp01(chargeTime / maxChargeTime);
        var jumpMultiplier = Lerp(minJumpForce, maxJumpForce, chargeProgress);

        if (_playerController.HasCatnip)
            jumpMultiplier *= _playerController.catnipJumpMultiplier;

        if (_cameraController)
            _cameraController.TriggerJumpFOV();

        _playerController.ResetAccelerationState();

        var currentHorizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
        var currentInputDirection = (int)Sign(currentHorizontalInput);

        var isOppositeDirection = _jumpChargeDirection != 0 &&
                                  currentInputDirection != 0 &&
                                  currentInputDirection != _jumpChargeDirection;

        // Apply jump force based on input direction
        _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0);
        if (isOppositeDirection)
        {
            _rb.linearVelocity = new Vector2(0, jumpMultiplier);
            _playerController.CurrentSpeed = 0;
        }
        else
        {
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpMultiplier);
        }

        // Setup bounce window
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

        _playerController.PostWallJump = true;

        var chargeProgress = Clamp01(chargeTime / maxWallChargeTime);
        var jumpMultiplier = Lerp(minWallJumpForce, maxWallJumpForce, chargeProgress);

        if (_playerController.HasCatnip)
            jumpMultiplier *= _playerController.catnipJumpMultiplier;

        if (_cameraController)
            _cameraController.TriggerJumpFOV();

        _playerController.ResetAccelerationState();

        var currentInputDirection = (int)Sign(currentHorizontalInput);
        var pushingAwayFromWall = (currentInputDirection == -1 && _playerController.WallSlideSide == 1) ||
                                  (currentInputDirection == 1 && _playerController.WallSlideSide == -1);

        if (pushingAwayFromWall)
        {
            _rb.linearVelocity = new Vector2(-_playerController.WallSlideSide * wallDetachForce, jumpMultiplier);
            _playerController.PostWallJump = false;

            _playerController.spriteTransform.rotation = Quaternion.identity;
            _playerController.spriteTransform.localPosition = _playerController.OriginalSpritePosition;

            _delayWallJumpQueuedReset = true;
        }
        else
        {
            _rb.linearVelocity = new Vector2(0, jumpMultiplier);
            animator.SetBool(IsJumpQueued, false);
        }

        _playerController.CurrentSpeed = _rb.linearVelocity.x;
        _lastJumpTime = Time.time;
    }

    /// Applies bounce force when hitting a wall during bounce window
    private void ApplyWallBounce(int wallSide)
    {
        var bounceDirection = -wallSide;
        var linearVelocity = _rb.linearVelocity;
        var currentXVelocity = linearVelocity.x;
        var currentYVelocity = linearVelocity.y;

        var xVelocity = bounceDirection * wallBounceForce;

        // Preserve momentum if enabled
        if (preserveMomentumOnBounce && Abs(currentXVelocity) > 0)
            if (Sign(currentXVelocity) * bounceDirection > 0)
            {
                var preservedMomentum = Abs(currentXVelocity) * momentumPreservation;
                xVelocity = bounceDirection * Max(wallBounceForce, preservedMomentum);
            }

        // Add vertical boost
        var yVelocity = Max(currentYVelocity, 0) + (wallBounceForce * wallBounceVerticalMultiplier);

        _rb.linearVelocity = new Vector2(xVelocity, yVelocity);
        _playerController.CurrentSpeed = xVelocity;
        _playerController.ResetAccelerationState();

        if (_cameraController)
            _cameraController.TriggerJumpFOV();
    }
    #endregion

    #region Jump Cancellation and Conversion

    /// Cancels the jump charge process
    internal void CancelJumpCharge()
    {
        if (JumpState == JumpStateEnum.Idle && !HasBufferedChargeInAir)
            return;

        // Save charge progress if in air
        if (!_playerController.IsGrounded &&
            (JumpState == JumpStateEnum.Charging || JumpState == JumpStateEnum.Buffered))
        {
            var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
            StoredChargeProgress = chargeTime;
            _releaseJumpInAir = false; // Reset the air release flag
        }
        else
        {
            StoredChargeProgress = 0f;
        }

        JumpState = JumpStateEnum.Idle;
        _movementDisabledForJump = false;
        _jumpButtonHeld = false;
        _releaseJumpInAir = false;
        _delayWallJumpQueuedReset = false;

        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        _jumpChargeUIManager.ForceUIStateSync();
        animator.SetBool(IsJumpQueued, false);

        // Reset sprite if wall sliding
        if (!_playerController.IsWallSliding &&
            _playerController.spriteTransform.localPosition == _playerController.OriginalSpritePosition)
            return;

        _playerController.spriteTransform.rotation = Quaternion.identity;
        _playerController.spriteTransform.localPosition = _playerController.OriginalSpritePosition;
    }

    /// Converts normal jump to wall jump when hitting a wall
    internal void ConvertToWallJump()
    {
        if (JumpState != JumpStateEnum.Charging && JumpState != JumpStateEnum.Buffered)
            return;

        var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
        var chargeProgress = Clamp01(chargeTime / maxChargeTime);

        JumpState = JumpStateEnum.WallCharging;
        _jumpChargeStartTime = Time.time - (chargeProgress * maxWallChargeTime);

        if (_playerController.IsTouchingLeftWall)
            _playerController.WallSlideSide = -1;
        else if (_playerController.IsTouchingRightWall)
            _playerController.WallSlideSide = 1;

        animator.SetBool(IsJumpQueued, true);
    }

    /// Pushes player away from wall when they reach ground during wall jump charge
    internal void CancelWallJumpWithGroundPush()
    {
        if (JumpState != JumpStateEnum.WallCharging)
            return;

        JumpState = JumpStateEnum.Idle;
        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        animator.SetBool(IsJumpQueued, false);
        _delayWallJumpQueuedReset = false;

        _rb.linearVelocity = new Vector2(-_playerController.WallSlideSide * wallGroundPushForce, _rb.linearVelocity.y);

        _playerController.spriteTransform.rotation = Quaternion.identity;
        _playerController.spriteTransform.localPosition = _playerController.OriginalSpritePosition;
    }
    #endregion

    #region Landing Detection

    /// Checks if a jump should execute on landing
    internal void CheckJumpLanding(bool wasGrounded, bool isGroundedNow)
    {
        switch (wasGrounded)
        {
            // Check if player has landed on a wall with a buffered jump
            case false when !isGroundedNow &&
                            (JumpState == JumpStateEnum.Buffered || HasBufferedChargeInAir) &&
                            (_playerController.IsTouchingLeftWall || _playerController.IsTouchingRightWall):
                CancelJumpCharge();
                return;
            // Handle landing from air to ground
            case false when isGroundedNow:
                HandleLanding();
                return;
        }

        switch (JumpState)
        {
            // Handle wall charging case
            case JumpStateEnum.WallCharging:
                CancelJumpCharge();
                return;
            // Handle active jump charging cases
            case JumpStateEnum.Charging:
            {
                var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
                ExecuteJump(chargeTime);
                return;
            }
            case JumpStateEnum.Buffered:
                HandleBufferedJumpLanding();
                return;
        }

        // Handle stored charge case
        if (HasBufferedChargeInAir)
            HandleStoredChargeLanding();
    }

    /// Handles basic landing reset
    private void HandleLanding()
    {
        _playerController.spriteTransform.rotation = Quaternion.identity;
        _playerController.spriteTransform.localPosition = _playerController.OriginalSpritePosition;
        _playerController.PostWallJump = false;
        _delayWallJumpQueuedReset = false;

        // Handle "released in air" jump execution
        if (_releaseJumpInAir)
        {
            animator.SetBool(IsJumpQueued, true);
            ExecuteJump(_jumpFullyCharged ? maxChargeTime : StoredChargeProgress);
            _releaseJumpInAir = false;
            StoredChargeProgress = 0f;
            return;
        }

        if (JumpState == JumpStateEnum.Bouncing)
            JumpState = JumpStateEnum.Idle;
    }

    /// Handles landing with an active buffered jump
    private void HandleBufferedJumpLanding()
    {
        // Check if player is touching a wall
        if (_playerController.IsTouchingLeftWall || _playerController.IsTouchingRightWall)
        {
            // If touching a wall, cancel the buffered jump
            CancelJumpCharge();
            return;
        }

        animator.SetBool(IsJumpQueued, true);

        if (!_jumpButtonHeld)
        {
            // Jump immediately if button not held
            ExecuteJump(_jumpFullyCharged ? maxChargeTime : _currentChargeProgress * maxChargeTime);
        }
        else
        {
            // Continue charging on ground
            JumpState = JumpStateEnum.Charging;
            _movementDisabledForJump = !allowMovementDuringCharge;
        }
    }

    /// Handles landing with stored charge progress
    private void HandleStoredChargeLanding()
    {
        animator.SetBool(IsJumpQueued, true);

        if (_jumpButtonHeld)
        {
            // Continue charging on ground
            JumpState = JumpStateEnum.Charging;
            _jumpChargeStartTime = Time.time - StoredChargeProgress;
            _movementDisabledForJump = !allowMovementDuringCharge;
            _jumpChargeUIManager.SetChargingState(true, StoredChargeProgress / maxChargeTime, _jumpFullyCharged);
        }
        else
        {
            // Jump immediately
            ExecuteJump(_jumpFullyCharged ? maxChargeTime : StoredChargeProgress);
            StoredChargeProgress = 0f;
        }
    }
    #endregion

    #region Movement Controls

    /// Returns if movement should be disabled for jump
    internal bool IsMovementDisabledForJump()
    {
        return _movementDisabledForJump;
    }

    /// Gets movement multipliers based on charge state
    internal (float speedMultiplier, float accelMultiplier) GetJumpChargeMovementMultipliers()
    {
        if (!allowMovementDuringCharge ||
            (JumpState != JumpStateEnum.Charging && JumpState != JumpStateEnum.Buffered) ||
            !_playerController.IsGrounded)
            return (1f, 1f);

        // Calculate dynamic multipliers based on charge progress if enabled
        if (!reduceMovementWithCharge) return (chargeMovementSpeedMultiplier, chargeAccelerationMultiplier);

        // Linearly reduce from base multiplier to minimum as charge increases
        var dynamicSpeedMultiplier = Lerp(chargeMovementSpeedMultiplier, minMovementMultiplierAtFullCharge, _currentChargeProgress);
        var dynamicAccelMultiplier = Lerp(chargeAccelerationMultiplier, minMovementMultiplierAtFullCharge, _currentChargeProgress);

        return (dynamicSpeedMultiplier, dynamicAccelMultiplier);

        // Return constant multipliers
    }

    #endregion

    #region Utility

    /// Sets this player as being spectated
    internal void SetSpectatedState()
    {
        _jumpChargeUIManager.SetSpectatedState();
    }

    /// Called when this player starts being spectated
    internal void OnStartSpectating()
    {
        if (_photonView.IsMine && JumpState != JumpStateEnum.Idle)
            SyncJumpState();
    }

    /// Returns if the player started charging jump on the ground
    internal bool GetStartedChargingOnGround()
    {
        return JumpState == JumpStateEnum.Charging;
    }

    /// Called when player starts contacting a wall
    internal void OnWallContactStart()
    {
        StoredChargeProgress = 0f;
        _releaseJumpInAir = false;
    }
    #endregion
}
