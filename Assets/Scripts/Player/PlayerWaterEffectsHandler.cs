using System.Collections;
using Photon.Pun;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Handles water interaction effects including movement modification, visual tints, and splash particles
/// </summary>
[RequireComponent(typeof(PlayerController), typeof(PhotonView))]
public class PlayerWaterEffectsHandler : MonoBehaviour
{
    [Header("Water Detection")]
    [SerializeField] [Tooltip("Tag used to identify water objects")] private string waterTag = "Water";
    [SerializeField] [Tooltip("Layer mask for water objects")] private LayerMask waterLayer;

    [Header("Movement Modifiers")]
    [SerializeField] [Range(0.1f, 1.0f)] [Tooltip("How much player speed is reduced in water")] private float speedMultiplierInWater = 0.6f;
    [SerializeField] [Range(0.1f, 1.0f)] [Tooltip("How much jump force is reduced in water")] private float jumpMultiplierInWater = 0.7f;
    [SerializeField] [Range(0.1f, 1.0f)] [Tooltip("How much acceleration is reduced in water")] private float accelerationMultiplierInWater = 0.5f;

    [Header("Particle Effects")]
    [SerializeField] [Tooltip("Particle effect for regular water movement")] private GameObject waterSplashPrefab;
    [SerializeField] [Tooltip("Particle effect when entering/exiting water")] private GameObject waterEntrySplashPrefab;
    [SerializeField] [Tooltip("Minimum speed required to show splash particles")] private float minSpeedToShowParticles = 0.5f;
    [SerializeField] [Tooltip("Time between spawning splash particles")] private float particleSpawnInterval = 0.2f;
    [SerializeField] [Tooltip("How long regular splash effects last before being destroyed")] private float regularSplashLifetime = 2f;
    [SerializeField] [Tooltip("How long entry splash effects last before being destroyed")] private float entrySplashLifetime = 3f;

    [Header("Water Tint Effect")]
    [SerializeField] [Tooltip("Whether to apply color tint when in water")] private bool enableWaterTint = true;
    [SerializeField] [Tooltip("Color applied to the player when underwater")] private Color waterTintColor = new(0.6f, 0.8f, 1.0f, 0.9f);
    [SerializeField] [Tooltip("Time to transition between normal and underwater colors")] private float colorTransitionDuration = 0.5f;

    [Header("Camera Effects")]
    [SerializeField] [Tooltip("Whether water affects the camera")] private bool affectCamera = true;

    private PlayerController _playerController;
    private PhotonView _photonView;
    private SpriteRenderer _spriteRenderer;
    private DynamicCameraController _cameraController;
    private SpectatorModeManager _spectatorModeManager;
    private Color _originalColor;
    private Coroutine _colorTransitionCoroutine;
    private bool _isInWater;
    private float _lastParticleTime;

    private float _originalMaxSpeed;
    private float _originalMinJumpForce;
    private float _originalMaxJumpForce;
    private float _originalBaseAcceleration;
    private bool _waterEffectsApplied;

    /// Initialize component references and store original movement values
    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();
        _photonView = GetComponent<PhotonView>();

        if (affectCamera)
        {
            var playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera != null)
                _cameraController = playerCamera.GetComponent<DynamicCameraController>();

            if (_cameraController == null)
                if (Camera.main != null)
                    _cameraController = Camera.main.GetComponent<DynamicCameraController>();
        }

        _originalMaxSpeed = _playerController.maxSpeed;
        _originalMinJumpForce = _playerController.JumpSystem.minJumpForce;
        _originalMaxJumpForce = _playerController.JumpSystem.maxJumpForce;
        _originalBaseAcceleration = _playerController.baseAcceleration;

        if (waterEntrySplashPrefab == null)
            waterEntrySplashPrefab = waterSplashPrefab;

        _spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;
    }

    /// Find spectator manager on start
    private void Start()
    {
        _spectatorModeManager = FindFirstObjectByType<SpectatorModeManager>();
    }

    /// Spawn water splash particles while moving in water
    private void Update()
    {
        if (!_isInWater) return;

        if (!_photonView.IsMine ||
            !waterSplashPrefab ||
            !(Mathf.Abs(_playerController.CurrentSpeed) > minSpeedToShowParticles) ||
            !(Time.time > _lastParticleTime + particleSpawnInterval)) return;

        _photonView.RPC(nameof(SpawnWaterSplash), RpcTarget.All);
        _lastParticleTime = Time.time;
    }

    /// Create water splash particle effect via RPC
    [PunRPC]
    private void SpawnWaterSplash()
    {
        var splash = Instantiate(waterSplashPrefab, transform.position, Quaternion.identity);
        Destroy(splash, regularSplashLifetime);
    }

    /// Apply water effects when entering water
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(waterTag) && !IsInWaterLayer(other.gameObject)) return;

        _isInWater = true;
        ApplyWaterEffects();
        StartWaterTintTransition(true);

        if (waterEntrySplashPrefab == null) return;

        var splash = Instantiate(waterEntrySplashPrefab, transform.position, Quaternion.identity);
        Destroy(splash, entrySplashLifetime);
    }

    /// Remove water effects when exiting water
    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(waterTag) && !IsInWaterLayer(other.gameObject)) return;

        _isInWater = false;
        RemoveWaterEffects();
        StartWaterTintTransition(false);

        if (waterEntrySplashPrefab == null) return;

        var splash = Instantiate(waterEntrySplashPrefab, transform.position, Quaternion.identity);
        Destroy(splash, entrySplashLifetime);
    }

    /// Check if object is in the water layer
    private bool IsInWaterLayer(GameObject obj)
    {
        return (waterLayer.value & (1 << obj.layer)) != 0;
    }

    /// Apply movement modifications for water physics
    private void ApplyWaterEffects()
    {
        if (_waterEffectsApplied)
            return;

        _waterEffectsApplied = true;

        _originalMaxSpeed = _playerController.maxSpeed;
        _originalMinJumpForce = _playerController.JumpSystem.minJumpForce;
        _originalMaxJumpForce = _playerController.JumpSystem.maxJumpForce;
        _originalBaseAcceleration = _playerController.baseAcceleration;

        _playerController.maxSpeed *= speedMultiplierInWater;
        _playerController.JumpSystem.minJumpForce *= jumpMultiplierInWater;
        _playerController.JumpSystem.maxJumpForce *= jumpMultiplierInWater;
        _playerController.baseAcceleration *= accelerationMultiplierInWater;

        if (affectCamera && _cameraController != null && (_photonView.IsMine || _spectatorModeManager.IsSpectating))
            _cameraController.EnterWater();
    }

    /// Reset movement values to original state when exiting water
    private void RemoveWaterEffects()
    {
        if (!_waterEffectsApplied)
            return;

        _waterEffectsApplied = false;

        _playerController.maxSpeed = _originalMaxSpeed;
        _playerController.JumpSystem.minJumpForce = _originalMinJumpForce;
        _playerController.JumpSystem.maxJumpForce = _originalMaxJumpForce;
        _playerController.baseAcceleration = _originalBaseAcceleration;

        if (_photonView.IsMine)
            _playerController.ResetAccelerationState();

        if (affectCamera && _cameraController != null && (_photonView.IsMine || _spectatorModeManager.IsSpectating))
            _cameraController.ExitWater();
    }

    /// Begin color transition for water tint effect
    private void StartWaterTintTransition(bool entering)
    {
        if (!enableWaterTint || _spriteRenderer == null) return;

        if (_colorTransitionCoroutine != null)
            StopCoroutine(_colorTransitionCoroutine);

        _colorTransitionCoroutine = StartCoroutine(TransitionColor(entering ? waterTintColor : _originalColor));
    }

    /// Smoothly transition between normal and water tint colors
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
