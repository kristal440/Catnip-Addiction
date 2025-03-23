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
    [SerializeField] private float minSpeedToShowParticles = 0.5f;
    [SerializeField] private float particleSpawnInterval = 0.2f;

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

        // Store original values
        _originalMaxSpeed = _playerController.maxSpeed;
        _originalMinJumpForce = _playerController.minJumpForce;
        _originalMaxJumpForce = _playerController.maxJumpForce;
        _originalAcceleration = _playerController.acceleration;
    }

    private void Update()
    {
        if (!_isInWater) return;

        if (!waterSplashPrefab ||
            !(Mathf.Abs(_playerController.currentSpeed) > minSpeedToShowParticles) ||
            !(Time.time > _lastParticleTime + particleSpawnInterval)) return;
        var splash = Instantiate(waterSplashPrefab, transform.position, Quaternion.identity);
        Destroy(splash, 2f);
        _lastParticleTime = Time.time;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(waterTag) && !IsInWaterLayer(other.gameObject)) return;

        _isInWater = true;
        ApplyWaterEffects();

        // Initial splash when entering water
        if (waterSplashPrefab == null) return;
        var splash = Instantiate(waterSplashPrefab, transform.position, Quaternion.identity);
        Destroy(splash, 2f);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(waterTag) && !IsInWaterLayer(other.gameObject)) return;

        _isInWater = false;
        RemoveWaterEffects();

        // Exit splash
        if (waterSplashPrefab == null) return;
        var splash = Instantiate(waterSplashPrefab, transform.position, Quaternion.identity);
        Destroy(splash, 2f);
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
