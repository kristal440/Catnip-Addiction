using System.Collections;
using Photon.Pun;
using UnityEngine;
using static UnityEngine.Mathf;

/// <summary>
/// Controls camera dynamics including field of view and positioning effects based on player movement and state.
/// </summary>
/// <inheritdoc />
public class DynamicCameraController : MonoBehaviour
{
    [Header("Field of View")]
    [SerializeField] [Tooltip("Base field of view when player is stationary")] public float defaultFOV = 37f;
    [SerializeField] [Tooltip("Maximum additional FOV when player is at max speed")] public float maxFOVOffset = 10f;
    [SerializeField] [Range(0.01f, 1f)] [Tooltip("Smoothing time for FOV changes")] public float fovSmoothTime = 0.5f;
    [SerializeField] [Tooltip("How much to increase FOV when player has catnip")] private float catnipFOVIncrease = 5f;

    [Header("Camera Positioning")]
    [SerializeField] [Tooltip("Maximum horizontal offset when moving at max speed")] public float maxHorizontalOffset = 0.25f;
    [SerializeField] [Range(0.01f, 1f)] [Tooltip("Smoothing time for horizontal position changes")] public float positionSmoothTime = 0.6f;
    [SerializeField] [Tooltip("Maximum vertical offset based on vertical speed")] public float maxVerticalOffset = 0.27f;
    [SerializeField] [Range(0.01f, 1f)] [Tooltip("Smoothing time for vertical position changes")] public float verticalSmoothTime = 0.3f;
    [SerializeField] [Tooltip("Default camera position relative to player")] public Vector2 defaultCameraOffset = new(0f, 0f);

    [Header("Player Search")]
    [SerializeField] [Tooltip("How long to search for player controller before disabling")] public float playerSearchTimeout = 5f;

    [Header("Jump Effects")]
    [SerializeField] [Tooltip("Target FOV when jumping")] public float jumpFOV = 45f;
    [SerializeField] [Tooltip("Target FOV when fully charging a jump")] public float minChargeJumpFOV = 25f;
    [SerializeField] [Tooltip("Speed of FOV transitions during jumps")] public float jumpFOVTransitionSpeed = 5f;

    [Header("Death Camera")]
    [SerializeField] [Tooltip("FOV when player dies")] public float deathZoomFOV = 25f;
    [SerializeField] [Tooltip("Speed of FOV transition when player dies")] public float deathZoomSpeed = 3f;

    [Header("Water Effects")]
    [SerializeField] [Range(-1f, 2f)] [Tooltip("FOV multiplier when underwater")] public float waterZoomMultiplier = 0.8f;

    [Header("Teleport Settings")]
    [SerializeField] [Range(0.1f, 2f)] [Tooltip("Duration of camera transition after teleportation")] public float teleportTransitionDuration = 0.5f;
    [SerializeField] [Tooltip("Animation curve for teleport camera movement")] private AnimationCurve teleportCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Initial Finish Target Settings")]
    [SerializeField] [Tooltip("Duration of camera transition from finish to player")] public float finishToPlayerTransitionDuration = 2.0f;
    [SerializeField] [Tooltip("Animation curve for finish to player transition")] private AnimationCurve finishToPlayerCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] [Tooltip("FOV when focusing on finish object")] public float finishFocusFOV = 45f;
    [SerializeField] [Tooltip("Duration the camera remains focused on finish object before transitioning to player")] public float finishFocusDuration = 1.5f;
    [SerializeField] [Tooltip("Delay after countdown starts before transitioning from finish to player")] public float countdownStartTransitionDelay = 2.0f;

    private float _defaultFOVBackup;
    private bool _isInJumpTransition;
    private bool _isInDeathZoom;
    private bool _isInTeleportTransition;
    private bool _isInInitialTransition;
    private float _jumpTransitionTimer;
    private const float JumpTransitionDuration = 0.2f;

    private Camera _camera;
    private float _currentFOVVelocity;
    private PlayerController _playerController;
    private float _currentHorizontalVelocity;
    private Vector2 _defaultPosition;
    private float _currentVerticalVelocity;

    private Vector2 _lastPlayerPosition;
    private float _actualPlayerSpeed;
    private GameObject _finishObject;

    /// Initializes the camera controller and triggers player search
    private void Start()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            Debug.LogError("DynamicCameraController requires a Camera component.");
            enabled = false;
            return;
        }

        _defaultFOVBackup = defaultFOV;
        StartCoroutine(InitialSetup());
    }

    /// Initial setup that finds finish object, player, and handles the transition
    private IEnumerator InitialSetup()
    {
        _finishObject = GameObject.FindWithTag("Finish");
        StartCoroutine(FindPlayerControllerWithTimeout());

        var startTime = Time.time;
        while (Time.time - startTime < playerSearchTimeout)
        {
            _playerController = GetComponentInParent<PlayerController>();
            if (_playerController) break;

            yield return null;
        }

        if (!_playerController)
        {
            Debug.LogError($"PlayerController not found after {playerSearchTimeout}s. Cannot transition to player.");
            enabled = false;
            yield break;
        }

        if (defaultFOV <= 0)
            defaultFOV = _camera.fieldOfView;

        _defaultPosition = defaultCameraOffset;
        _lastPlayerPosition = _playerController.transform.position;
    }

    /// Centers the camera on the finish object
    private void FocusOnFinishObject()
    {
        if (!_finishObject) return;

        var transform1 = transform;
        var position = _finishObject.transform.position;
        transform1.position = new Vector3(
            position.x,
            position.y,
            transform1.position.z
        );

        _camera.fieldOfView = finishFocusFOV;
    }

    /// Handles the transition from finish object to player
    private IEnumerator TransitionFromFinishToPlayer()
    {
        if (!_finishObject || !_playerController) yield break;

        var position1 = transform.position;
        var startFOV = _camera.fieldOfView;

        var position = _playerController.transform.position;
        var targetPosition = new Vector3(
            position.x + _defaultPosition.x,
            position.y + _defaultPosition.y,
            position1.z
        );
        var targetFOV = defaultFOV;

        var elapsedTime = 0f;

        while (elapsedTime < finishToPlayerTransitionDuration)
        {
            var t = elapsedTime / finishToPlayerTransitionDuration;
            var smoothT = finishToPlayerCurve.Evaluate(t);

            transform.position = Vector3.Lerp(position1, targetPosition, smoothT);
            _camera.fieldOfView = Lerp(startFOV, targetFOV, smoothT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
        _camera.fieldOfView = targetFOV;

        _currentHorizontalVelocity = 0f;
        _currentVerticalVelocity = 0f;
        _currentFOVVelocity = 0f;
        _isInInitialTransition = false;

        _lastPlayerPosition = _playerController.transform.position;
    }

    /// Searches for player controller with timeout
    private IEnumerator FindPlayerControllerWithTimeout()
    {
        var startTime = Time.time;

        while (Time.time - startTime < playerSearchTimeout)
        {
            _playerController = GetComponentInParent<PlayerController>();
            if (_playerController)
            {
                InitializeCamera();
                yield break;
            }
            yield return null;
        }

        Debug.LogError($"PlayerController not found after {playerSearchTimeout}s. Disabling camera controller.");
        enabled = false;
    }

    /// Sets up initial camera values and position
    private void InitializeCamera()
    {
        if (defaultFOV <= 0)
            defaultFOV = _camera.fieldOfView;
        else
            _camera.fieldOfView = defaultFOV;

        _defaultPosition = defaultCameraOffset;

        CenterCameraOnPlayer();

        _lastPlayerPosition = _playerController.transform.position;
    }

    /// Centers the camera on the player using the default offset
    private void CenterCameraOnPlayer()
    {
        if (!_playerController) return;

        var transform1 = transform;
        transform1.localPosition = new Vector3(_defaultPosition.x, _defaultPosition.y, transform1.localPosition.z);
        _currentHorizontalVelocity = 0;
        _currentVerticalVelocity = 0;
    }

    /// Updates camera effects based on player movement
    private void FixedUpdate()
    {
        if (!_playerController || _isInTeleportTransition || _isInInitialTransition) return;

        if (Time.deltaTime > 0)
        {
            Vector2 currentPlayerPosition = _playerController.transform.position;
            _actualPlayerSpeed = Vector2.Distance(_lastPlayerPosition, currentPlayerPosition) / Time.deltaTime;
            _lastPlayerPosition = currentPlayerPosition;
        }

        var effectiveSpeed = Min(Abs(_playerController.CurrentSpeed), _actualPlayerSpeed);
        var normalizedSpeed = Clamp01(effectiveSpeed / _playerController.maxSpeed);

        UpdateFOV(normalizedSpeed);
        UpdateCameraPosition(normalizedSpeed);
    }

    /// Updates FOV when charging a jump based on charge progress
    internal void UpdateChargingJumpFOV(float chargeProgress)
    {
        if (_isInDeathZoom || _isInInitialTransition) return;

        var targetFOV = Lerp(defaultFOV, minChargeJumpFOV, chargeProgress);
        _camera.fieldOfView = Lerp(_camera.fieldOfView, targetFOV, Time.deltaTime * jumpFOVTransitionSpeed);
    }

    /// Triggers jump transition FOV effect
    internal void TriggerJumpFOV()
    {
        if (_isInDeathZoom || _isInInitialTransition) return;

        _isInJumpTransition = true;
        _jumpTransitionTimer = 0f;
    }

    /// Updates FOV based on player state and movement
    private void UpdateFOV(float normalizedSpeed)
    {
        float targetFOV;

        if (_isInDeathZoom)
        {
            targetFOV = deathZoomFOV;
            _camera.fieldOfView = Lerp(_camera.fieldOfView, targetFOV, Time.deltaTime * deathZoomSpeed);
        }
        else if (_isInJumpTransition)
        {
            targetFOV = jumpFOV;
            _camera.fieldOfView = Lerp(_camera.fieldOfView, targetFOV, Time.deltaTime * jumpFOVTransitionSpeed);

            _jumpTransitionTimer += Time.deltaTime;
            if (_jumpTransitionTimer >= JumpTransitionDuration)
                _isInJumpTransition = false;
        }
        else
        {
            var baseFOV = defaultFOV;
            if (_playerController && _playerController.HasCatnip)
                baseFOV += catnipFOVIncrease;

            targetFOV = baseFOV + (normalizedSpeed * maxFOVOffset);
            _camera.fieldOfView = SmoothDamp(_camera.fieldOfView, targetFOV, ref _currentFOVVelocity, fovSmoothTime);
        }
    }

    /// Updates camera position based on player movement
    private void UpdateCameraPosition(float normalizedSpeed)
    {
        var targetX = _defaultPosition.x;
        if (!Approximately(normalizedSpeed, 0))
        {
            var direction = Sign(_playerController.CurrentSpeed);
            targetX += direction * normalizedSpeed * maxHorizontalOffset;
        }

        var localPosition = transform.localPosition;
        var newX = SmoothDamp(localPosition.x, targetX, ref _currentHorizontalVelocity, positionSmoothTime);

        var normalizedVerticalSpeed = Clamp(_playerController.VerticalSpeed / _playerController.maxSpeed, -1, 1);
        var targetY = _defaultPosition.y + (normalizedVerticalSpeed * maxVerticalOffset);
        var newY = SmoothDamp(localPosition.y, targetY, ref _currentVerticalVelocity, verticalSmoothTime);

        localPosition = new Vector3(newX, newY, localPosition.z);
        transform.localPosition = localPosition;
    }

    /// Activates death camera effect when player dies
    internal void OnPlayerDeath()
    {
        _isInDeathZoom = true;
    }

    /// Resets camera when player respawns
    internal void OnPlayerRespawn()
    {
        _isInDeathZoom = false;

        if (!_isInTeleportTransition && !_isInInitialTransition)
            CenterCameraOnPlayer();
    }

    /// Applies underwater FOV effect
    internal void EnterWater()
    {
        defaultFOV = _defaultFOVBackup * waterZoomMultiplier;
    }

    /// Resets FOV when exiting water
    internal void ExitWater()
    {
        defaultFOV = _defaultFOVBackup;
    }

    /// Handles smooth camera transition after player teleportation
    internal IEnumerator HandleTeleportTransition(Vector3 destination, float duration = -1f)
    {
        if (!_playerController || _isInInitialTransition) yield break;

        var transitionDuration = duration > 0 ? duration : teleportTransitionDuration;

        _isInTeleportTransition = true;

        var position = transform.position;
        var targetCameraPosition = new Vector3(
            destination.x + _defaultPosition.x,
            destination.y + _defaultPosition.y,
            position.z);

        var elapsedTime = 0f;

        while (elapsedTime < transitionDuration)
        {
            if (!_playerController)
            {
                _isInTeleportTransition = false;
                yield break;
            }

            var t = elapsedTime / transitionDuration;
            var smoothT = teleportCurve.Evaluate(t);
            transform.position = Vector3.Lerp(position, targetCameraPosition, smoothT);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.position = targetCameraPosition;

        _currentHorizontalVelocity = 0f;
        _currentVerticalVelocity = 0f;

        _isInTeleportTransition = false;

        _lastPlayerPosition = _playerController.transform.position;
    }

    /// Triggers the transition from finish to player after a synchronized delay
    internal void TriggerTransitionAfterCountdown(int serverStartTime)
    {
        StartCoroutine(DelayedTransitionFromFinishToPlayer(serverStartTime));
    }

    /// Delays the transition from finish to player using Photon server time for consistency
    private IEnumerator DelayedTransitionFromFinishToPlayer(int serverStartTime)
    {
        _isInInitialTransition = true;

        // If player controller is null, continue searching for it
        if (!_playerController)
        {
            Debug.Log("DynamicCameraController: PlayerController not found yet. Searching for it before transition.");

            var searchStartTime = Time.time;
            while (Time.time - searchStartTime < playerSearchTimeout && !_playerController)
            {
                _playerController = GetComponentInParent<PlayerController>();
                yield return null;
            }

            if (!_playerController)
            {
                Debug.LogError("DynamicCameraController: PlayerController still null after extended search. Cannot perform transition.");
                _isInInitialTransition = false;
                yield break;
            }

            // Initialize player parameters when found
            _defaultPosition = defaultCameraOffset;
            _lastPlayerPosition = _playerController.transform.position;
        }

        _playerController.SetMovement(false);
        _playerController.DisableRigidbody();

        var currentServerTime = PhotonNetwork.Time;
        var targetTransitionServerTime = (serverStartTime / 1000.0) + countdownStartTransitionDelay;
        var waitTime = Max(0, (float)(targetTransitionServerTime - currentServerTime));
        yield return new WaitForSeconds(waitTime);

        FocusOnFinishObject();
        yield return new WaitForSeconds(finishFocusDuration);

        if (!_playerController)
        {
            Debug.LogError("PlayerController lost during transition. Cannot continue transition to player.");
            _isInInitialTransition = false;
            yield break;
        }

        yield return StartCoroutine(TransitionFromFinishToPlayer());

        _playerController.SetMovement(true);
        _playerController.EnableRigidbody();
    }
}
