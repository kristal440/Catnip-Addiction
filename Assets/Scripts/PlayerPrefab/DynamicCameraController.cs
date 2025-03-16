using System.Collections;
using UnityEngine;
using static UnityEngine.Mathf;

public class DynamicCameraController : MonoBehaviour
{
    [Header("Field of View Adjustment")]
    [Tooltip("Default Field of View value.")]
    public float defaultFOV = 37f;

    [Tooltip("Maximum increase in Field of View.")]
    public float maxFOVOffset = 10f;

    [Tooltip("Smoothing factor for FOV changes.")] [Range(0.01f, 1f)]
    public float fovSmoothTime = 0.5f;

    [Header("Horizontal Offset")]
    [Tooltip("Maximum horizontal distance the camera will move left/right based on player direction")]
    public float maxHorizontalOffset = 0.25f;
    [Tooltip("Smoothing factor for camera position movement.")]
    [Range(0.01f, 1f)]
    public float positionSmoothTime = 0.6f;

    [Header("Vertical Offset")]
    [Tooltip("Maximum vertical distance the camera will move up/down based on player speed")]
    public float maxVerticalOffset = 0.27f;
    [Tooltip("Smoothing factor for vertical camera movement.")]
    [Range(0.01f, 1f)]
    public float verticalSmoothTime = 0.3f;

    [Header("Player Search Timeout")]
    [Tooltip("Time in seconds to search for the PlayerController before disabling the script.")]
    public float playerSearchTimeout = 5f;

    [Header("Jump FOV Settings")]
    [Tooltip("FOV value when player initiates a jump")]
    public float jumpFOV = 45f;

    [Tooltip("Minimum FOV value during jump charge")]
    public float minChargeJumpFOV = 25f;

    [Tooltip("How quickly to transition to jump FOV")]
    public float jumpFOVTransitionSpeed = 5f;

    [Header("Death Camera Settings")]
    [Tooltip("FOV value when player dies (zoomed in)")]
    public float deathZoomFOV = 25f;

    [Tooltip("How quickly to zoom in when player dies")]
    public float deathZoomSpeed = 3f;

    private bool _isInJumpTransition;
    private bool _isInDeathZoom;
    private float _jumpTransitionTimer;
    private const float JumpTransitionDuration = 0.2f;

    private Camera _camera;
    private float _currentFOVVelocity;
    private PlayerController _playerController;
    private float _currentHorizontalVelocity;
    private Vector3 _defaultPosition;
    private Vector3 _previousPlayerScale;
    private float _currentVerticalVelocity;

    private void Start()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            Debug.LogError("DynamicCameraController needs to be attached to a Camera component.");
            enabled = false;
            return;
        }

        // Ensure the camera is set to perspective mode
        if (_camera.orthographic)
        {
            Debug.LogWarning("Camera is set to Orthographic mode. Switching to Perspective mode for FOV adjustment.");
            _camera.orthographic = false;
        }

        StartCoroutine(FindPlayerControllerWithTimeout());
    }

    // Automatically find the PlayerController in the parent hierarchy
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

        Debug.LogError("PlayerController not found in parent hierarchy after " + playerSearchTimeout +
                       " seconds. Disabling DynamicCameraController.");
        enabled = false;
    }

    private void InitializeCamera()
    {
        // If defaultFOV is not explicitly set in the Inspector, use the camera's current FOV
        if (defaultFOV <= 0)
            defaultFOV = _camera.fieldOfView;
        else
            _camera.fieldOfView = defaultFOV;

        _defaultPosition = transform.localPosition;
    }

    private void FixedUpdate()
    {
        if (!_playerController) return;

        var normalizedSpeed = Clamp01(Abs(_playerController.currentSpeed) / _playerController.maxSpeed);

        UpdateFOV(normalizedSpeed);
        UpdateHorizontalOffset(normalizedSpeed);
        UpdateVerticalOffset();
    }

    #region FOV stuff
    public void UpdateChargingJumpFOV(float chargeProgress)
    {
        // Skip if in death zoom
        if (_isInDeathZoom) return;

        // Calculate target FOV based on charge progress (0 to 1)
        // From default FOV down to minChargeJumpFOV
        var targetFOV = Lerp(defaultFOV, minChargeJumpFOV, chargeProgress);

        // Apply the FOV change with smoothing
        _camera.fieldOfView = Lerp(_camera.fieldOfView, targetFOV,
            Time.deltaTime * jumpFOVTransitionSpeed);
    }

    public void TriggerJumpFOV()
    {
        // Skip if in death zoom
        if (_isInDeathZoom) return;

        _isInJumpTransition = true;
        _jumpTransitionTimer = 0f;
    }

    private void UpdateFOV(float normalizedSpeed)
    {
        float targetFOV;

        if (_isInDeathZoom)
        {
            // Death zoom effect - zoom in on player
            targetFOV = deathZoomFOV;
            _camera.fieldOfView = Lerp(_camera.fieldOfView, targetFOV,
                Time.deltaTime * deathZoomSpeed);
        }
        if (_isInJumpTransition)
        {
            // Handle jump FOV transition
            _jumpTransitionTimer += Time.deltaTime;

            // Quickly transition to jump FOV
            targetFOV = jumpFOV;
            _camera.fieldOfView = Lerp(_camera.fieldOfView, targetFOV,
                Time.deltaTime * jumpFOVTransitionSpeed);

            // After transition duration, return to normal FOV calculation
            if (_jumpTransitionTimer >= JumpTransitionDuration)
                _isInJumpTransition = false;
        }
        else
        {
            // Normal FOV calculation based on speed
            var targetFOVOffset = normalizedSpeed * maxFOVOffset;
            targetFOV = defaultFOV + targetFOVOffset;
            targetFOV = Clamp(targetFOV, defaultFOV, defaultFOV + maxFOVOffset);

            _camera.fieldOfView = SmoothDamp(_camera.fieldOfView, targetFOV,
                ref _currentFOVVelocity, fovSmoothTime);
        }
    }
    #endregion

    // Smoothly move the camera horizontally based on player speed
    private void UpdateHorizontalOffset(float normalizedSpeed)
    {
        var targetHorizontalOffset = normalizedSpeed * maxHorizontalOffset;

        var targetPosition = _defaultPosition + new Vector3(targetHorizontalOffset, 0, 0);
        var newPosition = transform.localPosition;
        newPosition.x = SmoothDamp(transform.localPosition.x, targetPosition.x, ref _currentHorizontalVelocity,
            positionSmoothTime);

        transform.localPosition = newPosition;
    }

    private void UpdateVerticalOffset()
    {
        var normalizedVerticalSpeed = Clamp(_playerController.verticalSpeed / _playerController.maxSpeed, -1, 1);

        var targetVerticalOffset = normalizedVerticalSpeed * maxVerticalOffset;
        var targetPosition = _defaultPosition + new Vector3(0, targetVerticalOffset, 0);
        var newPosition = transform.localPosition;

        newPosition.y = SmoothDamp(transform.localPosition.y, targetPosition.y, ref _currentVerticalVelocity,
            verticalSmoothTime);

        transform.localPosition = newPosition;
    }

    // Call this method when the player dies
    public void OnPlayerDeath()
    {
        _isInDeathZoom = true;
    }

    // Call this method when the player respawns
    public void OnPlayerRespawn()
    {
        _isInDeathZoom = false;
    }
}