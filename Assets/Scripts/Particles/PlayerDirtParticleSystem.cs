using Photon.Pun;
using UnityEngine;

public class DirtParticleController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject playerObject;

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

    private bool _wasGrounded;
    private bool _isJumping;
    private float _lastFallingSpeed;

    private void Awake()
    {
        _dirtParticleSystem = gameObject.AddComponent<ParticleSystem>();
        SetupParticleSystem();
    }

    private void Start()
    {
        if (playerObject == null)
        {
            Debug.LogError("Player object reference is missing!");
            return;
        }

        _playerController = playerObject.GetComponent<PlayerController>();
        if (_playerController == null)
        {
            Debug.LogError("PlayerController component not found on player object!");
            return;
        }

        _photonView = playerObject.GetComponent<PhotonView>();

        _emission = _dirtParticleSystem.emission;
        _main = _dirtParticleSystem.main;

        if (_photonView && !_photonView.IsMine)
            AdjustForRemotePlayer();

        _wasGrounded = _playerController.IsGrounded;
    }

    private void Update()
    {
        if (!_playerController)
            return;

        var isGrounded = _playerController.IsGrounded;
        var currentSpeed = Mathf.Abs(_playerController.currentSpeed);
        var verticalSpeed = _playerController.verticalSpeed;

        // Store the falling speed to use for landing particles
        if (!isGrounded && verticalSpeed < 0)
        {
            _lastFallingSpeed = Mathf.Abs(verticalSpeed);
        }

        if (_wasGrounded && !isGrounded && verticalSpeed > 0)
        {
            _isJumping = true;
            if (enableJumpParticles)
                EmitJumpParticles(verticalSpeed);
        }

        if (isGrounded && !_wasGrounded)
        {
            if (enableLandingParticles)
                EmitLandingParticles(_lastFallingSpeed);
            _isJumping = false;
        }

        if (isGrounded)
            UpdateWalkingParticles(currentSpeed);
        else
            _emission.rateOverTime = 0;

        _wasGrounded = isGrounded;
    }

    private void SetupParticleSystem()
    {
        var main = _dirtParticleSystem.main;
        main.startColor = new ParticleSystem.MinMaxGradient(dirtColorMin, dirtColorMax);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSizeMin, particleSizeMax);
        main.startLifetime = new ParticleSystem.MinMaxCurve(particleLifetimeMin, particleLifetimeMax);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 1.0f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = gravityModifier;
        main.maxParticles = maxParticles;

        var emission = _dirtParticleSystem.emission;
        emission.rateOverTime = 0;

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

        var defaultParticleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
        if (defaultParticleMaterial != null)
        {
            component.material = defaultParticleMaterial;
            component.trailMaterial = defaultParticleMaterial;
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
            var speedFactor = Mathf.Clamp01(speed / _playerController.maxSpeed);
            _emission.rateOverTime = baseEmissionRate * speedFactor * walkingEmissionMultiplier;
        }
        else
        {
            _emission.rateOverTime = 0;
        }
    }

    private void EmitJumpParticles(float jumpForce)
    {
        var jumpForceFactor = Mathf.Clamp01(
            (jumpForce - _playerController.minJumpForce) /
            (_playerController.maxJumpForce - _playerController.minJumpForce)
        );

        var burstCount = Mathf.RoundToInt(Mathf.Lerp(jumpBurstCountMin, jumpBurstCountMax, jumpForceFactor));

        var burstSpeed = Mathf.Lerp(jumpBurstSpeedMin, jumpBurstSpeedMax, jumpForceFactor);

        var mainTemp = _main;
        mainTemp.startSpeed = new ParticleSystem.MinMaxCurve(burstSpeed * 0.5f, burstSpeed);

        _dirtParticleSystem.Emit(burstCount);
    }

    private void EmitLandingParticles(float landingForce)
    {
        Debug.Log($"Landing force: {landingForce}, Minimum: {minimumLandingForce}");

        if (landingForce < minimumLandingForce)
            return;

        var landingForceFactor = Mathf.Clamp01(
            (landingForce - minimumLandingForce) /
            (maximumLandingForce - minimumLandingForce)
        );

        var burstCount = Mathf.RoundToInt(Mathf.Lerp(landingBurstCountMin, landingBurstCountMax, landingForceFactor));

        var burstSpeed = Mathf.Lerp(landingBurstSpeedMin, landingBurstSpeedMax, landingForceFactor);

        var mainTemp = _main;
        mainTemp.startSpeed = new ParticleSystem.MinMaxCurve(burstSpeed * 0.3f, burstSpeed);

        _dirtParticleSystem.Emit(burstCount);
    }

    private void AdjustForRemotePlayer()
    {
        var main = _dirtParticleSystem.main;
        var startColor = main.startColor;

        var minColor = startColor.colorMin;
        var maxColor = startColor.colorMax;

        minColor.a *= remotePlayerOpacityMultiplier;
        maxColor.a *= remotePlayerOpacityMultiplier;

        main.startColor = new ParticleSystem.MinMaxGradient(minColor, maxColor);
    }
}
