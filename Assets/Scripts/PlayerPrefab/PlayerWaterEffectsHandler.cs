using UnityEngine;
using System.Collections;

[RequireComponent(typeof(PlayerController))]
public class WaterEffectsHandler : MonoBehaviour
{
    [Header("Water Detection")]
    [SerializeField] private string waterTag = "Water";
    [SerializeField] private LayerMask waterLayer;

    [Header("Movement Modifiers")]
    [Range(0.1f, 1.0f)]
    [SerializeField] private float speedMultiplierInWater = 0.6f;
    [Range(0.1f, 1.0f)]
    [SerializeField] private float jumpMultiplierInWater = 0.7f;
    [Range(0.1f, 1.0f)]
    [SerializeField] private float accelerationMultiplierInWater = 0.5f;

    [Header("Particle Effects")]
    [SerializeField] private GameObject waterSplashPrefab;
    [SerializeField] private GameObject waterEntrySplashPrefab;
    [SerializeField] private float minSpeedToShowParticles = 0.5f;
    [SerializeField] private float particleSpawnInterval = 0.2f;
    [SerializeField] private float regularSplashLifetime = 2f;
    [SerializeField] private float entrySplashLifetime = 3f;

    [Header("Water Tint Effect")]
    [SerializeField] private bool enableWaterTint = true;
    [SerializeField] private Color waterTintColor = new Color(0.6f, 0.8f, 1.0f, 0.9f);
    [SerializeField] private float colorTransitionDuration = 0.5f;

    [Header("Camera Effects")]
    [SerializeField] private bool affectCamera = true;

    private PlayerController _playerController;
    private SpriteRenderer _spriteRenderer;
    private DynamicCameraController _cameraController;
    private Color _originalColor;
    private Coroutine _colorTransitionCoroutine;
    private bool _isInWater;
    private float _lastParticleTime;

    private float _originalMaxSpeed;
    private float _originalMinJumpForce;
    private float _originalMaxJumpForce;
    private float _originalAcceleration;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();

        // Find the camera controller
        if (affectCamera)
        {
            var playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null)
                _cameraController = playerCamera.GetComponent<DynamicCameraController>();

            if (_cameraController == null)
                _cameraController = Camera.main?.GetComponent<DynamicCameraController>();
        }

        _originalMaxSpeed = _playerController.maxSpeed;
        _originalMinJumpForce = _playerController.minJumpForce;
        _originalMaxJumpForce = _playerController.maxJumpForce;
        _originalAcceleration = _playerController.acceleration;

        if (waterEntrySplashPrefab == null)
        {
            waterEntrySplashPrefab = waterSplashPrefab;
        }

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }
    }

    private void Update()
    {
        if (!_isInWater) return;

        if (!waterSplashPrefab ||
            !(Mathf.Abs(_playerController.currentSpeed) > minSpeedToShowParticles) ||
            !(Time.time > _lastParticleTime + particleSpawnInterval)) return;
        var splash = Instantiate(waterSplashPrefab, transform.position, Quaternion.identity);
        Destroy(splash, regularSplashLifetime);
        _lastParticleTime = Time.time;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(waterTag) && !IsInWaterLayer(other.gameObject)) return;

        _isInWater = true;
        ApplyWaterEffects();
        StartWaterTintTransition(true);

        // Initial splash
        if (waterEntrySplashPrefab == null) return;
        var splash = Instantiate(waterEntrySplashPrefab, transform.position, Quaternion.identity);
        Destroy(splash, entrySplashLifetime);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(waterTag) && !IsInWaterLayer(other.gameObject)) return;

        _isInWater = false;
        RemoveWaterEffects();
        StartWaterTintTransition(false);

        // Exit splash
        if (waterEntrySplashPrefab == null) return;
        var splash = Instantiate(waterEntrySplashPrefab, transform.position, Quaternion.identity);
        Destroy(splash, entrySplashLifetime);
    }

    private bool IsInWaterLayer(GameObject obj)
    {
        return (waterLayer.value & (1 << obj.layer)) != 0;
    }

    private void ApplyWaterEffects()
    {
        // Apply movement effects
        _originalMaxSpeed = _playerController.maxSpeed;
        _originalMinJumpForce = _playerController.minJumpForce;
        _originalMaxJumpForce = _playerController.maxJumpForce;
        _originalAcceleration = _playerController.acceleration;

        _playerController.maxSpeed *= speedMultiplierInWater;
        _playerController.minJumpForce *= jumpMultiplierInWater;
        _playerController.maxJumpForce *= jumpMultiplierInWater;
        _playerController.acceleration *= accelerationMultiplierInWater;

        // Apply camera effects
        if (affectCamera && _cameraController != null)
            _cameraController.EnterWater();
    }

    private void RemoveWaterEffects()
    {
        // Remove movement effects
        _playerController.maxSpeed = _originalMaxSpeed;
        _playerController.minJumpForce = _originalMinJumpForce;
        _playerController.maxJumpForce = _originalMaxJumpForce;
        _playerController.acceleration = _originalAcceleration;

        // Remove camera effects
        if (affectCamera && _cameraController != null)
            _cameraController.ExitWater();
    }

    private void StartWaterTintTransition(bool entering)
    {
        if (!enableWaterTint || _spriteRenderer == null) return;

        if (_colorTransitionCoroutine != null)
        {
            StopCoroutine(_colorTransitionCoroutine);
        }

        _colorTransitionCoroutine = StartCoroutine(TransitionColor(entering ? waterTintColor : _originalColor));
    }

    private IEnumerator TransitionColor(Color targetColor)
    {
        var startColor = _spriteRenderer.color;
        var elapsedTime = 0f;

        while (elapsedTime < colorTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            var t = Mathf.Clamp01(elapsedTime / colorTransitionDuration);
            _spriteRenderer.color = Color.Lerp(startColor, targetColor, t);
            yield return null;
        }

        _spriteRenderer.color = targetColor;
        _colorTransitionCoroutine = null;
    }
}
