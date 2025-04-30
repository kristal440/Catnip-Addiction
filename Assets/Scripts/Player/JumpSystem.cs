using Photon.Pun;
using UnityEngine;
using static UnityEngine.Mathf;

/// <inheritdoc />
/// <summary>
/// Helper script for PlayerController that handles all jump-related mechanics
/// </summary>
public class JumpSystem : MonoBehaviour
{
    #region Variables
    [Header("References")]
    [SerializeField] [Tooltip("Skin Animator component")] private Animator animator;

    [Header("Charged Jump")]
    [SerializeField] [Tooltip("Minimum jump force when uncharged")] internal float minJumpForce = 8.5f;
    [SerializeField] [Tooltip("Maximum jump force when fully charged")] internal float maxJumpForce = 14f;
    [SerializeField] [Range(0.1f, 2f)] [Tooltip("Maximum time to charge regular jump")] internal float maxChargeTime = 2f;
    [SerializeField] [Range(0.05f, 0.5f)] [Tooltip("Time before player can jump again")] internal float jumpCooldown = 0.1f;

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

    // Internal state variables
    private float _jumpChargeStartTime;
    private bool _jumpButtonHeld;
    private bool _movementDisabledForJump;
    private bool _startedChargingOnGround;
    private bool _releaseJumpInAir;
    private float _jumpChargeLevel;
    private bool _jumpFullyCharged;
    private int _jumpChargeDirection;
    private float _lastJumpTime;
    private bool _isInBounceWindow;
    private float _bounceWindowEndTime;
    private bool _hasBounced;
    private JumpStateEnum _previousJumpState;

    // Component references
    private PlayerController _playerController;
    private JumpChargeUIManager _jumpChargeUIManager;
    private InputSystem_Actions _playerInputActions;
    private Rigidbody2D _rb;
    private DynamicCameraController _cameraController;
    private PhotonView _photonView;

    // Properties
    internal float StoredChargeProgress { get; private set; }
    internal bool HasBufferedChargeInAir { get; private set; }
    internal JumpStateEnum JumpState { get; private set; } = JumpStateEnum.Idle;
    internal enum JumpStateEnum
    {
        Idle,
        Charging,
        WallCharging,
        Buffered
    }
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
    private void OnEnable()
    {
        _playerInputActions.Player.Enable();
    }

    /// Disable input system
    private void OnDisable()
    {
        _playerInputActions.Player.Disable();
    }

    /// Update jump charging and bounce mechanics
    internal void UpdateJumpSystem()
    {
        if (JumpState != _previousJumpState)
        {
            SyncJumpState();
            _previousJumpState = JumpState;
        }

        _jumpButtonHeld = _playerInputActions.Player.Jump.IsPressed();

        UpdateJumpCharging();
        CheckWallBounce();

        if (_isInBounceWindow && Time.time > _bounceWindowEndTime)
            _isInBounceWindow = false;
    }
    #endregion

    #region Jump Input Handling

    /// Processes jump input and initiates jump
    internal void HandleJumpInput()
    {
        if (_playerController.IsPaused)
            return;

        var jumpOnCooldown = Time.time < _lastJumpTime + jumpCooldown;
        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;

        // Handle initial jump button press
        if (_playerInputActions.Player.Jump.WasPressedThisFrame() && JumpState == JumpStateEnum.Idle &&
            _playerController.IsStanding && !jumpOnCooldown)
        {
            switch (_playerController.IsWallSliding)
            {
                case true when _playerController.WallContactTime >= minWallContactTime:
                    _jumpChargeStartTime = Time.time;
                    JumpState = JumpStateEnum.WallCharging;
                    _jumpFullyCharged = false;
                    _jumpChargeUIManager.SetChargingState(true, 0f, false);
                    animator.SetBool(IsJumpQueued, true);
                    break;
                case false:
                    _jumpChargeStartTime = Time.time - StoredChargeProgress;
                    _jumpChargeDirection = (int)Sign(_rb.linearVelocity.x);
                    _startedChargingOnGround = _playerController.IsGrounded;
                    _releaseJumpInAir = false;
                    JumpState = _startedChargingOnGround ? JumpStateEnum.Charging : JumpStateEnum.Buffered;
                    _jumpFullyCharged = false;
                    HasBufferedChargeInAir = !_startedChargingOnGround;
                    if (_startedChargingOnGround)
                    {
                        _movementDisabledForJump = true;
                        animator.SetBool(IsJumpQueued, true);
                    }
                    _jumpChargeUIManager.SetChargingState(true, StoredChargeProgress / maxChargeTime, false);
                    break;
            }
        }
        // Handle additional jump presses while buffered jump is active
        else if (_playerInputActions.Player.Jump.WasPressedThisFrame() &&
                 JumpState == JumpStateEnum.Idle &&
                 !_playerController.IsGrounded &&
                 HasBufferedChargeInAir &&
                 !jumpOnCooldown)
        {
            // Resume buffered jump with stored progress
            _jumpChargeStartTime = Time.time - StoredChargeProgress;
            _jumpChargeDirection = (int)Sign(_rb.linearVelocity.x);
            _releaseJumpInAir = false;
            JumpState = JumpStateEnum.Buffered;
            _jumpFullyCharged = StoredChargeProgress >= maxChargeTime;
            _jumpChargeUIManager.SetChargingState(true, StoredChargeProgress / maxChargeTime, _jumpFullyCharged);
        }

        if (_playerInputActions.Player.Jump.IsPressed() || !_jumpButtonHeld) return;

        _jumpButtonHeld = false;

        switch (JumpState)
        {
            case JumpStateEnum.Charging:
            case JumpStateEnum.Buffered:
                var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
                _jumpChargeLevel = chargeTime;
                StoredChargeProgress = chargeTime;

                if (_playerController.IsGrounded)
                {
                    ExecuteJump(chargeTime);
                    HasBufferedChargeInAir = false;
                    StoredChargeProgress = 0f;
                }
                else
                {
                    _releaseJumpInAir = true;
                    HasBufferedChargeInAir = true;
                    JumpState = JumpStateEnum.Idle;

                    _jumpChargeUIManager.SetChargingState(false, StoredChargeProgress / maxChargeTime, _jumpFullyCharged);
                }
                break;

            case JumpStateEnum.WallCharging:
                var wallChargeTime = Min(Time.time - _jumpChargeStartTime, maxWallChargeTime);
                ExecuteWallJump(wallChargeTime, horizontalInput);
                break;
        }
    }

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
        if (!_photonView.IsMine) return;

        if (_previousJumpState == JumpState) return;

        if (JumpState == JumpStateEnum.Idle)
        {
            _photonView.RPC(nameof(_jumpChargeUIManager.RPC_EndJumpCharge), RpcTarget.Others);
        }
        else if (_previousJumpState == JumpStateEnum.Idle)
        {
            var maxChargeTimeToUse = JumpState == JumpStateEnum.WallCharging ? maxWallChargeTime : maxChargeTime;
            _photonView.RPC(nameof(_jumpChargeUIManager.RPC_StartJumpCharge), RpcTarget.Others, (int)JumpState, maxChargeTimeToUse);
        }

        _previousJumpState = JumpState;
    }
    #endregion

    #region Jump Execution

    /// Performs the actual jump with calculated force
    private void ExecuteJump(float chargeTime)
    {
        JumpState = JumpStateEnum.Idle;
        _movementDisabledForJump = false;
        _releaseJumpInAir = false;
        _startedChargingOnGround = false;
        HasBufferedChargeInAir = false;
        StoredChargeProgress = 0f;

        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        _jumpChargeUIManager.ForceUIStateSync();

        var chargeProgress = Clamp01(chargeTime / maxChargeTime);
        var jumpMultiplier = Lerp(minJumpForce, maxJumpForce, chargeProgress);

        if (_playerController.HasCatnip)
            jumpMultiplier *= _playerController.catnipJumpMultiplier;

        _cameraController.TriggerJumpFOV();

        _playerController.ResetAccelerationState();

        var currentHorizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
        var currentInputDirection = (int)Sign(currentHorizontalInput);

        var isOppositeDirection = _jumpChargeDirection != 0 && currentInputDirection != 0 && currentInputDirection != _jumpChargeDirection;

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

        _playerController.PostWallJump = true;

        var chargeProgress = Clamp01(chargeTime / maxWallChargeTime);
        var jumpMultiplier = Lerp(minWallJumpForce, maxWallJumpForce, chargeProgress);

        if (_playerController.HasCatnip)
            jumpMultiplier *= _playerController.catnipJumpMultiplier;

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
        }
        else
        {
            _rb.linearVelocity = new Vector2(0, jumpMultiplier);
        }

        _playerController.CurrentSpeed = _rb.linearVelocity.x;
        _lastJumpTime = Time.time;
    }

    /// Cancels the jump charge process
    internal void CancelJumpCharge()
    {
        if (JumpState == JumpStateEnum.Idle && !HasBufferedChargeInAir)
            return;

        if (!_playerController.IsGrounded && JumpState != JumpStateEnum.WallCharging && JumpState != JumpStateEnum.Idle)
        {
            var chargeTime = Min(Time.time - _jumpChargeStartTime, maxChargeTime);
            StoredChargeProgress = chargeTime;
            HasBufferedChargeInAir = true;
        }
        else
        {
            HasBufferedChargeInAir = false;
            StoredChargeProgress = 0f;
        }

        JumpState = JumpStateEnum.Idle;

        _movementDisabledForJump = false;
        _jumpButtonHeld = false;
        _releaseJumpInAir = false;
        _startedChargingOnGround = false;

        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        _jumpChargeUIManager.ForceUIStateSync();
        animator.SetBool(IsJumpQueued, false);

        if (!_playerController.IsWallSliding && _playerController.spriteTransform.localPosition == _playerController.OriginalSpritePosition) return;

        _playerController.spriteTransform.rotation = Quaternion.identity;
        _playerController.spriteTransform.localPosition = _playerController.OriginalSpritePosition;
    }

    /// Converts normal jump to wall jump when hitting a wall
    internal void ConvertToWallJump()
    {
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
        JumpState = JumpStateEnum.Idle;

        _jumpChargeUIManager.SetChargingState(false, 0f, false);
        animator.SetBool(IsJumpQueued, false);

        _rb.linearVelocity = new Vector2(-_playerController.WallSlideSide * wallGroundPushForce, _rb.linearVelocity.y);

        _playerController.spriteTransform.rotation = Quaternion.identity;
        _playerController.spriteTransform.localPosition = _playerController.OriginalSpritePosition;
    }
    #endregion

    #region Jump Landing

    /// Checks if a jump should execute on landing
    internal void CheckJumpLanding(bool wasGrounded, bool isGroundedNow)
    {
        if (!wasGrounded && isGroundedNow)
        {
            _playerController.spriteTransform.rotation = Quaternion.identity;
            _playerController.spriteTransform.localPosition = _playerController.OriginalSpritePosition;
            _playerController.PostWallJump = false;
            _isInBounceWindow = false;
        }

        if (wasGrounded || !isGroundedNow) return;

        if (JumpState == JumpStateEnum.WallCharging)
        {
            CancelJumpCharge();
            return;
        }

        if (JumpState != JumpStateEnum.Charging && JumpState != JumpStateEnum.Buffered && !HasBufferedChargeInAir) return;

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

        // Handle landing with stored charge
        if (HasBufferedChargeInAir)
        {
            animator.SetBool(IsJumpQueued, true);
            if (_jumpButtonHeld)
            {
                JumpState = JumpStateEnum.Charging;
                _jumpChargeStartTime = Time.time - StoredChargeProgress;
                _startedChargingOnGround = true;
                _movementDisabledForJump = true;
                _jumpChargeUIManager.SetChargingState(true, StoredChargeProgress / maxChargeTime, _jumpFullyCharged);
            }
            else
            {
                ExecuteJump(_jumpFullyCharged ? maxChargeTime : StoredChargeProgress);
                HasBufferedChargeInAir = false;
                StoredChargeProgress = 0f;
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
            HasBufferedChargeInAir = false;
            StoredChargeProgress = 0f;
        }
        else
        {
            JumpState = JumpStateEnum.Idle;
            _jumpChargeUIManager.SetChargingState(false, 0f, false);
        }
    }
    #endregion

    #region Wall Bounce

    /// Checks if player should bounce off wall after a charged jump
    private void CheckWallBounce()
    {
        if (!_isInBounceWindow || _playerController.IsWallSliding || _hasBounced || JumpState == JumpStateEnum.WallCharging)
            return;

        var horizontalInput = _playerInputActions.Player.Move.ReadValue<Vector2>().x;
        var moveDirection = (int)Sign(horizontalInput);

        if (_playerController.IsTouchingLeftWall && moveDirection < 0)
        {
            ApplyWallBounce(-1);
            _hasBounced = true;
            return;
        }

        if (_playerController.IsTouchingRightWall && moveDirection > 0)
        {
            ApplyWallBounce(1);
            _hasBounced = true;
            return;
        }

        if (!(Abs(horizontalInput) < 0.01f)) return;

        var velocityDirection = (int)Sign(_rb.linearVelocity.x);

        if (_playerController.IsTouchingLeftWall && velocityDirection < 0)
        {
            ApplyWallBounce(-1);
            _hasBounced = true;
            return;
        }

        if (!_playerController.IsTouchingRightWall || velocityDirection <= 0) return;

        ApplyWallBounce(1);
        _hasBounced = true;
    }

    /// Applies bounce force when hitting a wall during bounce window
    private void ApplyWallBounce(int wallSide)
    {
        var bounceDirection = -wallSide;
        var linearVelocity = _rb.linearVelocity;
        var currentXVelocity = linearVelocity.x;
        var currentYVelocity = linearVelocity.y;

        var xVelocity = bounceDirection * wallBounceForce;

        if (preserveMomentumOnBounce && Abs(currentXVelocity) > 0)
            if (Sign(currentXVelocity) * bounceDirection > 0)
            {
                var preservedMomentum = Abs(currentXVelocity) * momentumPreservation;
                xVelocity = bounceDirection * Max(wallBounceForce, preservedMomentum);
            }

        var yVelocity = Max(currentYVelocity, 0) + (wallBounceForce * wallBounceVerticalMultiplier);

        _rb.linearVelocity = new Vector2(xVelocity, yVelocity);
        _playerController.CurrentSpeed = xVelocity;

        _playerController.ResetAccelerationState();

        if (_cameraController)
            _cameraController.TriggerJumpFOV();
    }
    #endregion

    #region Utility

    /// Sets this player as being spectated or not
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

    /// Returns if movement should be disabled for jump
    internal bool IsMovementDisabledForJump()
    {
        return _movementDisabledForJump;
    }

    /// Returns if the player started charging jump on the ground
    internal bool GetStartedChargingOnGround()
    {
        return _startedChargingOnGround;
    }
    #endregion
}
