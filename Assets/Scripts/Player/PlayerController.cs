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
    [Tooltip("How quickly player accelerates")]
    public AnimationCurve accelerationCurve = new AnimationCurve(
        new Keyframe(0f, 0.3f),
        new Keyframe(0.6f, 0.7f),
        new Keyframe(1f, 1f)
    );
    public float baseAcceleration = 10f;
    [Tooltip("How long it takes to reach full acceleration")]
    public float accelerationTime = 0.8f;
    public float deceleration = 15f;
    public float maxSpeed = 5f;
    [Tooltip("Higher speed reached after maintaining max speed")]
    public float turboSpeed = 7f;
    [Tooltip("Time in seconds player needs to maintain max speed before reaching turbo speed")]
    public float timeToTurboSpeed = 1.5f;
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float verticalSpeed;

    // Acceleration tracking
    private float _currentAccelTime;
    private float _timeAtMaxSpeed;
    private bool _isTurboActive;
    private int _lastMoveDirection;

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
    private float _idleTimer;
    private Vector3 _previousPlayerScale;
    private float _originalGravityScale;
    private bool _wallCollisionHandled;
    private float _lastJumpTime;

    // Jump buffer
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
    internal bool IsDead { get; private set; }

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

        UpdateJumpCharging();

        if (transform.position.y < deathHeight && !IsDead)
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

        _isTouchingWall = Physics2D.OverlapCircle(wallCheck.position, wallCheckRadius, wallLayerMask);

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
        var currentTurboSpeed = HasCatnip ? turboSpeed * 1.1f : turboSpeed;

        var facingRight = transform.localScale.x > 0;
        var movingIntoWall = _isTouchingWall &&
                             ((facingRight && horizontalInput > 0) ||
                              (!facingRight && horizontalInput < 0));

        if (movingIntoWall && !_wallCollisionHandled)
        {
            currentSpeed = 0;
            _wallCollisionHandled = true;
            _currentAccelTime = 0f;
            _timeAtMaxSpeed = 0f;
            _isTurboActive = false;
        }

        var moveDirection = (int)Sign(horizontalInput);
        switch (Abs(horizontalInput))
        {
            case > 0.01f when !movingIntoWall:
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

    // For compatibility with existing code that referenced acceleration
    internal float Acceleration
    {
        get => baseAcceleration * accelerationCurve.Evaluate(_currentAccelTime / accelerationTime);
        set => baseAcceleration = value;
    }

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

    internal void RespawnAtLastCheckpoint()
    {
        Teleport(CheckpointManager.LastCheckpointPosition);
        OnPlayerRespawn();
    }

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
