using UnityEngine;

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

    private PlayerController _playerController;
    private bool _isInWater;
    private float _lastParticleTime;

    private float _originalMaxSpeed;
    private float _originalMinJumpForce;
    private float _originalMaxJumpForce;
    private float _originalAcceleration;

    private void Awake()
    {
        _playerController = GetComponent<PlayerController>();

        _originalMaxSpeed = _playerController.maxSpeed;
        _originalMinJumpForce = _playerController.minJumpForce;
        _originalMaxJumpForce = _playerController.maxJumpForce;
        _originalAcceleration = _playerController.acceleration;

        if (waterEntrySplashPrefab == null)
        {
            waterEntrySplashPrefab = waterSplashPrefab;
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
        _originalMaxSpeed = _playerController.maxSpeed;
        _originalMinJumpForce = _playerController.minJumpForce;
        _originalMaxJumpForce = _playerController.maxJumpForce;
        _originalAcceleration = _playerController.acceleration;

        _playerController.maxSpeed *= speedMultiplierInWater;
        _playerController.minJumpForce *= jumpMultiplierInWater;
        _playerController.maxJumpForce *= jumpMultiplierInWater;
        _playerController.acceleration *= accelerationMultiplierInWater;
    }

    private void RemoveWaterEffects()
    {
        _playerController.maxSpeed = _originalMaxSpeed;
        _playerController.minJumpForce = _originalMinJumpForce;
        _playerController.maxJumpForce = _originalMaxJumpForce;
        _playerController.acceleration = _originalAcceleration;
    }
}
