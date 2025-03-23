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
    [SerializeField] private ParticleSystem waterMovementParticles;
    [SerializeField] private float minSpeedToShowParticles = 0.5f;

    private PlayerController _playerController;
    private bool _isInWater;

    // Original values storage
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

        // Ensure particles are off at start
        if (waterMovementParticles != null)
            waterMovementParticles.Stop();
    }

    private void Update()
    {
        if (!_isInWater) return;

        // Show particles when moving in water at sufficient speed
        if (waterMovementParticles != null)
        {
            bool shouldEmit = Mathf.Abs(_playerController.currentSpeed) > minSpeedToShowParticles;

            if (shouldEmit && !waterMovementParticles.isEmitting)
                waterMovementParticles.Play();
            else if (!shouldEmit && waterMovementParticles.isEmitting)
                waterMovementParticles.Stop();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(waterTag) && !IsInWaterLayer(other.gameObject)) return;

        _isInWater = true;
        ApplyWaterEffects();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag(waterTag) && !IsInWaterLayer(other.gameObject)) return;

        _isInWater = false;
        RemoveWaterEffects();

        if (waterMovementParticles != null)
            waterMovementParticles.Stop();
    }

    private bool IsInWaterLayer(GameObject obj)
    {
        return (waterLayer.value & (1 << obj.layer)) != 0;
    }

    private void ApplyWaterEffects()
    {
        // Store current values which may already include catnip effects
        _originalMaxSpeed = _playerController.maxSpeed;
        _originalMinJumpForce = _playerController.minJumpForce;
        _originalMaxJumpForce = _playerController.maxJumpForce;
        _originalAcceleration = _playerController.acceleration;

        // Apply water multipliers to current values
        _playerController.maxSpeed *= speedMultiplierInWater;
        _playerController.minJumpForce *= jumpMultiplierInWater;
        _playerController.maxJumpForce *= jumpMultiplierInWater;
        _playerController.acceleration *= accelerationMultiplierInWater;
    }

    private void RemoveWaterEffects()
    {
        // Restore to pre-water values (which may include catnip effects)
        _playerController.maxSpeed = _originalMaxSpeed;
        _playerController.minJumpForce = _originalMinJumpForce;
        _playerController.maxJumpForce = _originalMaxJumpForce;
        _playerController.acceleration = _originalAcceleration;
    }
}
