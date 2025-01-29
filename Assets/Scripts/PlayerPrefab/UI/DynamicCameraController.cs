using UnityEngine;
using System.Collections;

public class DynamicCameraController : MonoBehaviour
{
    [Header("Orthographic Size Offset")]
    [Tooltip("Maximum increase in Orthographic Size.")]
    public float maxOrthographicSizeOffset = 1f;

    [Tooltip("Smoothing factor for Orthographic Size changes.")] [Range(0.01f, 1f)]
    public float orthographicSizeSmoothTime = 0.5f;

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

    private Camera _camera;
    private float _defaultOrthographicSize;
    private float _currentOrthographicSizeVelocity;
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
        _defaultOrthographicSize = _camera.orthographicSize;
        _defaultPosition = transform.localPosition;
    }

    private void Update()
    {
        if (!_playerController) return;

        var normalizedSpeed = Mathf.Clamp01(Mathf.Abs(_playerController.currentSpeed) / _playerController.maxSpeed);

        UpdateOrthographicSizeOffset(normalizedSpeed);
        UpdateHorizontalOffset(normalizedSpeed);
        UpdateVerticalOffset();
    }

    // Smoothly increase Orthographic Size based on player speed
    private void UpdateOrthographicSizeOffset(float normalizedSpeed)
    {
        if (_playerController.transform.localScale != _previousPlayerScale)
            transform.localPosition = new Vector3(transform.localPosition.x * -1, transform.localPosition.y, transform.localPosition.z);
        _previousPlayerScale = _playerController.transform.localScale;

        var targetOrthographicSizeOffset = normalizedSpeed * maxOrthographicSizeOffset;
        var targetOrthographicSize = _defaultOrthographicSize + targetOrthographicSizeOffset;

        targetOrthographicSize = Mathf.Clamp(targetOrthographicSize, _defaultOrthographicSize,
            _defaultOrthographicSize + maxOrthographicSizeOffset);

        _camera.orthographicSize = Mathf.SmoothDamp(_camera.orthographicSize, targetOrthographicSize,
            ref _currentOrthographicSizeVelocity, orthographicSizeSmoothTime);
    }

    // Smoothly move the camera horizontally based on player speed
    private void UpdateHorizontalOffset(float normalizedSpeed)
    {
        var targetHorizontalOffset = normalizedSpeed * maxHorizontalOffset;

        var targetPosition = _defaultPosition + new Vector3(targetHorizontalOffset, 0, 0);
        var newPosition = transform.localPosition;
        newPosition.x = Mathf.SmoothDamp(transform.localPosition.x, targetPosition.x, ref _currentHorizontalVelocity,
            positionSmoothTime);

        transform.localPosition = newPosition;
    }

    private void UpdateVerticalOffset()
    {
        var normalizedVerticalSpeed = Mathf.Clamp(_playerController.verticalSpeed / _playerController.maxSpeed, -1, 1);

        var targetVerticalOffset = normalizedVerticalSpeed * maxVerticalOffset;
        var targetPosition = _defaultPosition + new Vector3(0, targetVerticalOffset, 0);
        var newPosition = transform.localPosition;

        newPosition.y = Mathf.SmoothDamp(transform.localPosition.y, targetPosition.y, ref _currentVerticalVelocity,
            verticalSmoothTime);

        transform.localPosition = newPosition;
    }
}
