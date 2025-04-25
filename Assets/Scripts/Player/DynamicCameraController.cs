using System.Collections;
using UnityEngine;
using static UnityEngine.Mathf;

public class DynamicCameraController : MonoBehaviour
{
    #region Variables
    [Header("Field of View")]
    public float defaultFOV = 37f;
    public float maxFOVOffset = 10f;
    [Range(0.01f, 1f)] public float fovSmoothTime = 0.5f;
    [SerializeField] private float catnipFOVIncrease = 5f;

    [Header("Camera Positioning")]
    public float maxHorizontalOffset = 0.25f;
    [Range(0.01f, 1f)] public float positionSmoothTime = 0.6f;
    public float maxVerticalOffset = 0.27f;
    [Range(0.01f, 1f)] public float verticalSmoothTime = 0.3f;

    [Header("Player Search")]
    public float playerSearchTimeout = 5f;

    [Header("Jump Effects")]
    public float jumpFOV = 45f;
    public float minChargeJumpFOV = 25f;
    public float jumpFOVTransitionSpeed = 5f;

    [Header("Death Camera")]
    public float deathZoomFOV = 25f;
    public float deathZoomSpeed = 3f;

    [Header("Water Effects")]
    [Range(-1f, 2f)] public float waterZoomMultiplier = 0.8f;

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
    #endregion

    #region Initialization
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

    private void InitializeCamera()
    {
        if (defaultFOV <= 0)
            defaultFOV = _camera.fieldOfView;
        else
            _camera.fieldOfView = defaultFOV;

        var localPosition = transform.localPosition;
        _defaultPosition = new Vector2(localPosition.x, localPosition.y);
        _lastPlayerPosition = _playerController.transform.position;
    }
    #endregion

    #region Core Updates
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
    #endregion

    #region FOV Management

    internal void UpdateChargingJumpFOV(float chargeProgress)
    {
        if (_isInDeathZoom) return;

        var targetFOV = Lerp(defaultFOV, minChargeJumpFOV, chargeProgress);
        _camera.fieldOfView = Lerp(_camera.fieldOfView, targetFOV, Time.deltaTime * jumpFOVTransitionSpeed);
    }

    internal void TriggerJumpFOV()
    {
        if (_isInDeathZoom) return;

        _isInJumpTransition = true;
        _jumpTransitionTimer = 0f;
    }

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
    #endregion

    #region Position Management
    private void UpdateCameraPosition(float normalizedSpeed)
    {
        // Horizontal position
        var targetX = _defaultPosition.x + (normalizedSpeed * maxHorizontalOffset);
        var localPosition = transform.localPosition;
        var newX = SmoothDamp(localPosition.x, targetX, ref _currentHorizontalVelocity, positionSmoothTime);

        // Vertical position
        var normalizedVerticalSpeed = Clamp(_playerController.verticalSpeed / _playerController.maxSpeed, -1, 1);
        var targetY = _defaultPosition.y + (normalizedVerticalSpeed * maxVerticalOffset);
        var newY = SmoothDamp(localPosition.y, targetY, ref _currentVerticalVelocity, verticalSmoothTime);

        // Apply combined position
        localPosition = new Vector3(newX, newY, localPosition.z);
        transform.localPosition = localPosition;
    }
    #endregion

    #region Player Events

    internal void OnPlayerDeath()
    {
        _isInDeathZoom = true;
    }

    internal void OnPlayerRespawn()
    {
        _isInDeathZoom = false;
    }

    internal void EnterWater()
    {
        defaultFOV = _defaultFOVBackup * waterZoomMultiplier;
    }

    internal void ExitWater()
    {
        defaultFOV = _defaultFOVBackup;
    }
    #endregion
}
