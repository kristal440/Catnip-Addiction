using Photon.Pun;
using UnityEngine;
using static UnityEngine.Mathf;

public class PlayerDirtParticleSystem : MonoBehaviour
{
    [SerializeField] private GameObject playerObject;

    [Header("Interaction Filters")]
    [Tooltip("Particles won't spawn when player is colliding with objects on these layers")]
    [SerializeField] private LayerMask excludedLayers;
    [Tooltip("Radius to check for collisions with excluded objects")]
    [SerializeField] private float collisionCheckRadius = 0.2f;

    [Header("Base Particle Settings")]
    [SerializeField] private float baseEmissionRate = 5f;
    [SerializeField] private float walkingEmissionMultiplier = 2f;
    [SerializeField] private float particleLifetimeMin = 0.5f;
    [SerializeField] private float particleLifetimeMax = 1.0f;
    [SerializeField] private float particleSizeMin = 0.05f;
    [SerializeField] private float particleSizeMax = 0.15f;
    [SerializeField] private float gravityModifier = 0.5f;
    [SerializeField] private Color dirtColorMin = new(0.6f, 0.4f, 0.2f, 0.7f);
    [SerializeField] private Color dirtColorMax = new(0.7f, 0.5f, 0.3f, 0.5f);

    [Header("Jump Particles")]
    [SerializeField] private bool enableJumpParticles = true;
    [SerializeField] private float jumpBurstCountMin = 5f;
    [SerializeField] private float jumpBurstCountMax = 20f;
    [SerializeField] private float jumpBurstSpeedMin = 1f;
    [SerializeField] private float jumpBurstSpeedMax = 3f;

    [Header("Landing Particles")]
    [SerializeField] private bool enableLandingParticles = true;
    [SerializeField] private float landingBurstCountMin = 3f;
    [SerializeField] private float landingBurstCountMax = 15f;
    [SerializeField] private float landingBurstSpeedMin = 0.7f;
    [SerializeField] private float landingBurstSpeedMax = 2.1f;
    [SerializeField] private float minimumLandingForce = 0.5f;
    [SerializeField] private float maximumLandingForce = 10f;

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Foreground";
    [SerializeField] private int sortingOrder = 10;

    [Header("Performance")]
    [SerializeField] private int maxParticles = 100;

    [Header("Remote Player Settings")]
    [SerializeField] private float remotePlayerOpacityMultiplier = 0.7f;

    private PlayerController _playerController;
    private ParticleSystem _dirtParticleSystem;
    private ParticleSystem.EmissionModule _emission;
    private ParticleSystem.MainModule _main;
    private PhotonView _photonView;
    private Material _particleMaterial;

    private bool _wasGrounded;
    private float _lastFallingSpeed;
    private float _inverseJumpForceRange;
    private float _inverseLandingForceRange;
    private float _inverseMaxSpeed;

    private void Awake()
    {
        _dirtParticleSystem = gameObject.AddComponent<ParticleSystem>();
        _emission = _dirtParticleSystem.emission;
        _main = _dirtParticleSystem.main;

        _particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));

        SetupParticleSystem();
    }

    private void Start()
    {
        if (playerObject == null)
        {
            Debug.LogError("Player object reference is missing!");
            enabled = false;
            return;
        }

        _playerController = playerObject.GetComponent<PlayerController>();
        if (_playerController == null)
        {
            Debug.LogError("PlayerController component not found on player object!");
            enabled = false;
            return;
        }

        _photonView = playerObject.GetComponent<PhotonView>();
        _wasGrounded = _playerController.IsGrounded;

        _inverseJumpForceRange = 1f / (_playerController.maxJumpForce - _playerController.minJumpForce);
        _inverseLandingForceRange = 1f / (maximumLandingForce - minimumLandingForce);
        _inverseMaxSpeed = 1f / _playerController.maxSpeed;

        if (_photonView && !_photonView.IsMine)
            AdjustForRemotePlayer();
    }

    private void OnDestroy()
    {
        if (_particleMaterial != null)
            Destroy(_particleMaterial);
    }

    private void Update()
    {
        if (!_playerController)
            return;

        if (_playerController.IsDead || IsCollidingWithExcludedObjects())
        {
            if (_emission.rateOverTime.constant > 0)
                _emission.rateOverTime = 0;
            return;
        }

        var isGrounded = _playerController.IsGrounded;
        var verticalSpeed = _playerController.verticalSpeed;

        if (!isGrounded && verticalSpeed < 0)
            _lastFallingSpeed = Abs(verticalSpeed);

        if (_wasGrounded && !isGrounded && verticalSpeed > 0)
            if (enableJumpParticles)
                EmitJumpParticles(verticalSpeed);

        if (isGrounded && !_wasGrounded)
            if (enableLandingParticles)
                EmitLandingParticles(_lastFallingSpeed);

        if (isGrounded)
        {
            var currentSpeed = Abs(_playerController.currentSpeed);
            UpdateWalkingParticles(currentSpeed);
        }
        else if (_emission.rateOverTime.constant > 0)
        {
            _emission.rateOverTime = 0;
        }

        _wasGrounded = isGrounded;
    }

    private bool IsCollidingWithExcludedObjects()
    {
        if (excludedLayers.value == 0) return false;

        var layerHit = Physics2D.OverlapCircle(
            playerObject.transform.position,
            collisionCheckRadius,
            excludedLayers);

        return layerHit && layerHit.gameObject != playerObject;
    }

    private void SetupParticleSystem()
    {
        _main.startColor = new ParticleSystem.MinMaxGradient(dirtColorMin, dirtColorMax);
        _main.startSize = new ParticleSystem.MinMaxCurve(particleSizeMin, particleSizeMax);
        _main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetimeMin, particleLifetimeMax);
        _main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.0f);
        _main.simulationSpace = ParticleSystemSimulationSpace.World;
        _main.gravityModifier = gravityModifier;
        _main.maxParticles = maxParticles;

        _emission.rateOverTime = 0;

        var shape = _dirtParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;
        shape.radiusThickness = 0;

        var colorOverLifetime = _dirtParticleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new(Color.white, 0.0f), new(Color.white, 1.0f) },
            new GradientAlphaKey[] { new(1.0f, 0.0f), new(0.0f, 1.0f) }
        );
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var sizeOverLifetime = _dirtParticleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        var curve = new AnimationCurve();
        curve.AddKey(0.0f, 1.0f);
        curve.AddKey(1.0f, 0.0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

        SetupParticleRenderer();
    }

    private void SetupParticleRenderer()
    {
        var component = _dirtParticleSystem.GetComponent<ParticleSystemRenderer>();
        if (component == null) return;

        component.sortingLayerName = sortingLayerName;
        component.sortingOrder = sortingOrder;

        if (_particleMaterial != null)
        {
            component.material = _particleMaterial;
            component.trailMaterial = _particleMaterial;
        }
        else
        {
            Debug.LogWarning("Could not find 'Particles/Standard Unlit' shader. Using default material.");
        }

        component.renderMode = ParticleSystemRenderMode.Billboard;
        component.alignment = ParticleSystemRenderSpace.View;
    }

    private void UpdateWalkingParticles(float speed)
    {
        if (speed > 0.1f)
        {
            var speedFactor = Clamp01(speed * _inverseMaxSpeed);
            _emission.rateOverTime = baseEmissionRate * speedFactor * walkingEmissionMultiplier;
        }
        else if (_emission.rateOverTime.constant > 0)
        {
            _emission.rateOverTime = 0;
        }
    }

    private void EmitJumpParticles(float jumpForce)
    {
        var jumpForceFactor = Clamp01(
            (jumpForce - _playerController.minJumpForce) * _inverseJumpForceRange
        );

        var burstCount = RoundToInt(Lerp(jumpBurstCountMin, jumpBurstCountMax, jumpForceFactor));
        var burstSpeed = Lerp(jumpBurstSpeedMin, jumpBurstSpeedMax, jumpForceFactor);

        _main.startSpeed = new ParticleSystem.MinMaxCurve(burstSpeed * 0.5f, burstSpeed);
        _dirtParticleSystem.Emit(burstCount);
    }

    private void EmitLandingParticles(float landingForce)
    {
        if (landingForce < minimumLandingForce)
            return;

        var landingForceFactor = Clamp01(
            (landingForce - minimumLandingForce) * _inverseLandingForceRange
        );

        var burstCount = RoundToInt(Lerp(landingBurstCountMin, landingBurstCountMax, landingForceFactor));
        var burstSpeed = Lerp(landingBurstSpeedMin, landingBurstSpeedMax, landingForceFactor);

        _main.startSpeed = new ParticleSystem.MinMaxCurve(burstSpeed * 0.3f, burstSpeed);
        _dirtParticleSystem.Emit(burstCount);
    }

    private void AdjustForRemotePlayer()
    {
        var startColor = _main.startColor;
        var minColor = startColor.colorMin;
        var maxColor = startColor.colorMax;

        minColor.a *= remotePlayerOpacityMultiplier;
        maxColor.a *= remotePlayerOpacityMultiplier;

        _main.startColor = new ParticleSystem.MinMaxGradient(minColor, maxColor);
    }
}
