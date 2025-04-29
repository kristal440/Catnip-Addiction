using System.Collections;
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
    [SerializeField] [Tooltip("Default camera position relative to player")] public Vector2 defaultCameraOffset = new Vector2(0f, 0f);

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

    private float _defaultFOVBackup;
    private bool _isInJumpTransition;
    private bool _isInDeathZoom;
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
        StartCoroutine(FindPlayerControllerWithTimeout());
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

        // Center camera on player with the configured offset
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
        if (!_playerController) return;

        if (Time.deltaTime > 0)
        {
            Vector2 currentPlayerPosition = _playerController.transform.position;
            _actualPlayerSpeed = Vector2.Distance(_lastPlayerPosition, currentPlayerPosition) / Time.deltaTime;
            _lastPlayerPosition = currentPlayerPosition;
        }

        var effectiveSpeed = Min(Abs(_playerController.currentSpeed), _actualPlayerSpeed);
        var normalizedSpeed = Clamp01(effectiveSpeed / _playerController.maxSpeed);

        UpdateFOV(normalizedSpeed);
        UpdateCameraPosition(normalizedSpeed);
    }

    /// Updates FOV when charging a jump based on charge progress
    internal void UpdateChargingJumpFOV(float chargeProgress)
    {
        if (_isInDeathZoom) return;

        var targetFOV = Lerp(defaultFOV, minChargeJumpFOV, chargeProgress);
        _camera.fieldOfView = Lerp(_camera.fieldOfView, targetFOV, Time.deltaTime * jumpFOVTransitionSpeed);
    }

    /// Triggers jump transition FOV effect
    internal void TriggerJumpFOV()
    {
        if (_isInDeathZoom) return;

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
            var direction = Sign(_playerController.currentSpeed);
            targetX += direction * normalizedSpeed * maxHorizontalOffset;
        }

        var localPosition = transform.localPosition;
        var newX = SmoothDamp(localPosition.x, targetX, ref _currentHorizontalVelocity, positionSmoothTime);

        var normalizedVerticalSpeed = Clamp(_playerController.verticalSpeed / _playerController.maxSpeed, -1, 1);
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
}
