using System.Collections;
using Photon.Pun;
using UnityEngine;
using static UnityEngine.Color;
using static UnityEngine.Mathf;
using static UnityEngine.Vector3;

/// <inheritdoc />
/// <summary>
/// Creates and manages particle effects that visualize catnip influence on players with local and remote player support
/// </summary>
public class CatnipEffectParticles : MonoBehaviour
{
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    [Header("Player Reference")]
    [SerializeField] [Tooltip("The player GameObject this particle effect is attached to")] private GameObject playerObject;

    [Header("Particle Main Module")]
    [SerializeField] [Tooltip("Total duration of the particle effect in seconds")] private float duration = 5f;
    [SerializeField] [Tooltip("How long individual particles will exist before being destroyed")] private float startLifetime = 2f;
    [SerializeField] [Tooltip("Minimum initial speed of particles")] private float startSpeedMin = 0.1f;
    [SerializeField] [Tooltip("Maximum initial speed of particles")] private float startSpeedMax = 0.5f;
    [SerializeField] [Tooltip("Minimum starting size of particles")] private float startSizeMin = 0.2f;
    [SerializeField] [Tooltip("Maximum starting size of particles")] private float startSizeMax = 0.5f;
    [SerializeField] [Tooltip("Minimum color for particles")] private Color startColorMin = green;
    [SerializeField] [Tooltip("Maximum color for particles")] private Color startColorMax = new(0.5f, 1f, 0.5f, 1f);
    [SerializeField] [Tooltip("How much gravity affects particles")] private float gravityModifier;
    [SerializeField] [Tooltip("Maximum number of particles allowed in system")] private int maxParticles = 50;
    [SerializeField] [Tooltip("Space in which particles are simulated")] private ParticleSystemSimulationSpace simulationSpace = ParticleSystemSimulationSpace.Local;

    [Header("Emission Module")]
    [SerializeField] [Tooltip("Number of particles emitted per second")] private float rateOverTime = 10f;

    [Header("Shape Module")]
    [SerializeField] [Tooltip("Shape of the particle emission area")] private ParticleSystemShapeType shapeType = ParticleSystemShapeType.Sphere;
    [SerializeField] [Tooltip("Radius of the emission shape")] private float shapeRadius = 0.75f;
    [SerializeField] [Range(0f, 1f)] [Tooltip("Thickness of the radius from center to edge")] private float radiusThickness = 1f;

    [Header("Lifetime Modules")]
    [SerializeField] [Tooltip("Enable color changes over particle lifetime")] private bool enableColorOverLifetime = true;
    [SerializeField] [Tooltip("Color gradient over particle lifetime")] private Gradient colorOverLifetime = DefaultFadeOutGradient();
    [SerializeField] [Tooltip("Enable size changes over particle lifetime")] private bool enableSizeOverLifetime = true;
    [SerializeField] [Tooltip("Animation curve controlling size over lifetime")] private AnimationCurve sizeOverLifetime = DefaultShrinkCurve();

    [Header("Rotation Module")]
    [SerializeField] [Tooltip("Enable particle rotation over lifetime")] private bool enableRotationOverLifetime = true;
    [SerializeField] [Tooltip("Minimum rotation speed in degrees per second")] private float rotationSpeedMin;
    [SerializeField] [Tooltip("Maximum rotation speed in degrees per second")] private float rotationSpeedMax = 90f;
    [SerializeField] [Tooltip("Use animation curve for rotation instead of constant speed")] private bool useRotationCurve;
    [SerializeField] [Tooltip("Animation curve controlling rotation over lifetime")] private AnimationCurve rotationOverLifetime = DefaultLinearCurve();

    [Header("Texture Sheet Animation Module")]
    [SerializeField] [Tooltip("Enable texture sheet animation for particles")] private bool enableTextureSheetAnimation = true;
    [SerializeField] [Tooltip("Number of horizontal tiles in the texture sheet")] private int textureSheetTilesX = 1;
    [SerializeField] [Tooltip("Number of vertical tiles in the texture sheet")] private int textureSheetTilesY = 4;

    [Header("Noise Module")]
    [SerializeField] [Tooltip("Enable noise for more organic particle movement")] private bool enableNoise = true;
    [SerializeField] [Tooltip("Strength of the noise effect on particle movement")] private float noiseStrength = 0.1f;
    [SerializeField] [Tooltip("Frequency of the noise pattern")] private float noiseFrequency = 0.5f;
    [SerializeField] [Tooltip("Speed at which the noise pattern scrolls")] private float noiseScrollSpeed = 0.5f;

    [Header("Renderer Module")]
    [SerializeField] [Tooltip("Material used for rendering particles")] private Material particleMaterial;
    [SerializeField] [Tooltip("How particles are rendered (Billboard, Stretched, etc)")] private ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard;
    [SerializeField] [Tooltip("How particles are sorted for rendering order")] private ParticleSystemSortMode sortMode = ParticleSystemSortMode.None;
    [SerializeField] [Range(-1f, 1f)] [Tooltip("Direction of normals for lighting")] private float normalDirection = 1f;
    [SerializeField] [Tooltip("Unity sorting layer for the particles")] private string sortingLayerName = "Foreground";
    [SerializeField] [Tooltip("Sorting order within the layer")] private int sortingOrder = 10;

    [Header("Remote Player Settings")]
    [SerializeField] [Tooltip("Whether to show effects for other players in multiplayer")] private bool showForRemotePlayers = true;
    [SerializeField] [Range(0.0f, 1.0f)] [Tooltip("Transparency multiplier for remote player effects")] private float remotePlayerOpacityMultiplier = 0.7f;

    private ParticleSystem _particleSystem;
    private ParticleSystemRenderer _particleSystemRenderer;
    private PlayerController _playerController;
    private bool _wasActiveLastFrame;
    private Vector3 _lastParentScale = one;
    private Transform _playerTransform;
    private PhotonView _playerPhotonView;
    private bool _isRemotePlayer;
    private Gradient _baseGradient;
    private Color _remoteStartColorMin;
    private Color _remoteStartColorMax;
    private Material _localPlayerMaterial;
    private Material _remotePlayerMaterial;
    private bool _hasCatnipLastFrame;

    /// Initializes component references and prepares particle materials
    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>() ? GetComponent<ParticleSystem>() : gameObject.AddComponent<ParticleSystem>();
        _particleSystemRenderer = GetComponent<ParticleSystemRenderer>() ? GetComponent<ParticleSystemRenderer>() : gameObject.AddComponent<ParticleSystemRenderer>();

        if (enableColorOverLifetime && colorOverLifetime != null)
            _baseGradient = colorOverLifetime;

        PrepareParticleMaterials();
    }

    /// Creates separate materials for local and remote players with appropriate opacity settings
    private void PrepareParticleMaterials()
    {
        if (particleMaterial != null)
        {
            _localPlayerMaterial = new Material(particleMaterial);
            _remotePlayerMaterial = new Material(particleMaterial)
            {
                color = new Color(
                    particleMaterial.color.r,
                    particleMaterial.color.g,
                    particleMaterial.color.b,
                    particleMaterial.color.a * remotePlayerOpacityMultiplier
                )
            };
        }

        _remoteStartColorMin = new Color(
            startColorMin.r,
            startColorMin.g,
            startColorMin.b,
            startColorMin.a * remotePlayerOpacityMultiplier
        );

        _remoteStartColorMax = new Color(
            startColorMax.r,
            startColorMax.g,
            startColorMax.b,
            startColorMax.a * remotePlayerOpacityMultiplier
        );
    }

    /// Sets up player references and initializes particle system
    private void Start()
    {
        if (!SetupPlayerReferences())
            return;

        SetupParticleSystem();
        _wasActiveLastFrame = false;
        UpdateParticleState(_playerController.HasCatnip);
        _hasCatnipLastFrame = _playerController.HasCatnip;
    }

    /// Finds and sets up references to player objects and components
    private bool SetupPlayerReferences()
    {
        if (playerObject == null)
        {
            enabled = false;
            return false;
        }

        _playerTransform = playerObject.transform;
        _playerController = playerObject.GetComponent<PlayerController>();

        if (_playerController == null)
        {
            enabled = false;
            return false;
        }

        _playerPhotonView = playerObject.GetComponent<PhotonView>();
        _isRemotePlayer = _playerPhotonView != null && !_playerPhotonView.IsMine;
        _lastParentScale = _playerTransform.localScale;
        UpdateObjectScale();
        return true;
    }

    /// Updates the scale of particle system to match the player's scale
    private void UpdateObjectScale()
    {
        if (!_playerTransform) return;

        var localScale = _playerTransform.localScale;
        transform.localScale = new Vector3(
            Abs(localScale.x),
            Abs(localScale.y),
            Abs(localScale.z)
        );
    }

    /// Configures the particle system with all settings from inspector
    private void SetupParticleSystem()
    {
        if (_particleSystem.isPlaying)
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        StartCoroutine(SetupParticleSystemDelayed());
    }

    /// Delays particle system setup for one frame to ensure all components are ready
    private IEnumerator SetupParticleSystemDelayed()
    {
        yield return null;

        var main = _particleSystem.main;
        main.duration = duration;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(startLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeedMin, startSpeedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(startSizeMin, startSizeMax);
        main.startColor = _isRemotePlayer
            ? new ParticleSystem.MinMaxGradient(_remoteStartColorMin, _remoteStartColorMax)
            : new ParticleSystem.MinMaxGradient(startColorMin, startColorMax);
        main.gravityModifier = gravityModifier;
        main.playOnAwake = false;
        main.maxParticles = maxParticles;
        main.simulationSpace = simulationSpace;

        var emission = _particleSystem.emission;
        emission.rateOverTime = rateOverTime;
        emission.enabled = true;

        var shape = _particleSystem.shape;
        shape.shapeType = shapeType;
        shape.radius = shapeRadius;
        shape.radiusThickness = radiusThickness;

        SetupModules();
        SetupRenderer();

        if (_wasActiveLastFrame && _playerController && _playerController.HasCatnip)
            _particleSystem.Play(true);
    }

    /// Configures additional particle system modules like color, size, rotation, etc.
    private void SetupModules()
    {
        var col = _particleSystem.colorOverLifetime;
        col.enabled = enableColorOverLifetime;
        if (enableColorOverLifetime)
            col.color = new ParticleSystem.MinMaxGradient(_baseGradient);

        var sol = _particleSystem.sizeOverLifetime;
        sol.enabled = enableSizeOverLifetime;
        if (enableSizeOverLifetime)
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeOverLifetime);

        var rot = _particleSystem.rotationOverLifetime;
        rot.enabled = enableRotationOverLifetime;
        if (enableRotationOverLifetime)
            rot.z = useRotationCurve
                ? new ParticleSystem.MinMaxCurve(1f, rotationOverLifetime)
                : new ParticleSystem.MinMaxCurve(rotationSpeedMin * Deg2Rad, rotationSpeedMax * Deg2Rad);

        var texSheetAnim = _particleSystem.textureSheetAnimation;
        texSheetAnim.enabled = enableTextureSheetAnimation;
        if (enableTextureSheetAnimation)
        {
            texSheetAnim.mode = ParticleSystemAnimationMode.Grid;
            texSheetAnim.numTilesX = textureSheetTilesX;
            texSheetAnim.numTilesY = textureSheetTilesY;
            var maxFrameIndex = (textureSheetTilesX * textureSheetTilesY) - 1;
            if (maxFrameIndex < 0) maxFrameIndex = 0;
            texSheetAnim.startFrame = new ParticleSystem.MinMaxCurve(0f, maxFrameIndex);
            texSheetAnim.animation = ParticleSystemAnimationType.SingleRow;
            texSheetAnim.frameOverTime = new ParticleSystem.MinMaxCurve(0f);
            texSheetAnim.cycleCount = 1;
        }

        var noise = _particleSystem.noise;
        noise.enabled = enableNoise;
        if (!enableNoise) return;

        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = noiseScrollSpeed;
    }

    /// Configures particle renderer properties and materials
    private void SetupRenderer()
    {
        if (_isRemotePlayer && _remotePlayerMaterial)
            _particleSystemRenderer.material = _remotePlayerMaterial;
        else if (_localPlayerMaterial)
            _particleSystemRenderer.material = _localPlayerMaterial;
        else if (particleMaterial)
            _particleSystemRenderer.material = particleMaterial;

        _particleSystemRenderer.renderMode = renderMode;
        _particleSystemRenderer.sortMode = sortMode;
        _particleSystemRenderer.normalDirection = normalDirection;
        _particleSystemRenderer.sortingLayerName = sortingLayerName;
        _particleSystemRenderer.sortingOrder = sortingOrder;

        if (!_isRemotePlayer) return;

        var materialPropertyBlock = new MaterialPropertyBlock();
        _particleSystemRenderer.GetPropertyBlock(materialPropertyBlock);
        materialPropertyBlock.SetColor(ColorProperty, new Color(1, 1, 1, remotePlayerOpacityMultiplier));
        _particleSystemRenderer.SetPropertyBlock(materialPropertyBlock);
    }

    /// Monitors player catnip state and updates particles accordingly
    private void Update()
    {
        if (!_playerController || !_particleSystem || !_playerTransform)
            return;

        if (_playerTransform.localScale != _lastParentScale)
        {
            _lastParentScale = _playerTransform.localScale;
            UpdateObjectScale();
        }

        var hasCatnip = _playerController.HasCatnip;
        if (hasCatnip == _hasCatnipLastFrame) return;

        var isCurrentlyActive = _isRemotePlayer
            ? showForRemotePlayers && hasCatnip
            : hasCatnip;

        if (isCurrentlyActive != _wasActiveLastFrame)
        {
            UpdateParticleState(isCurrentlyActive);
            _wasActiveLastFrame = isCurrentlyActive;
        }

        _hasCatnipLastFrame = hasCatnip;
    }

    /// Enables or disables the particle system based on active state
    private void UpdateParticleState(bool isActive)
    {
        if (!_particleSystem) return;

        if (isActive)
            _particleSystem.Play(true);
        else
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    /// Force clears all particles, use for teleporting or respawning
    public void ClearAllParticles()
    {
        if (!_particleSystem) return;

        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    /// Creates a default gradient that fades out particles over their lifetime
    private static Gradient DefaultFadeOutGradient()
    {
        var gradient = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(white, 0.0f),
                new GradientColorKey(white, 1.0f)
            },
            alphaKeys = new[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.8f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f)
            }
        };

        return gradient;
    }

    /// Creates a default curve for shrinking particles over their lifetime
    private static AnimationCurve DefaultShrinkCurve()
    {
        return new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 0.0f));
    }

    /// Creates a default linear curve from 0 to 1
    private static AnimationCurve DefaultLinearCurve()
    {
        return new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 1.0f));
    }

    #if UNITY_EDITOR
    /// Updates particle system in editor when properties change
    private void OnValidate()
    {
        if (!gameObject.scene.isLoaded) return;

        _particleSystem = GetComponent<ParticleSystem>();
        _particleSystemRenderer = GetComponent<ParticleSystemRenderer>();

        if (_particleSystem == null || _particleSystemRenderer == null || !Application.isPlaying) return;

        var wasPlaying = false;
        if (_particleSystem.isPlaying)
        {
            wasPlaying = true;
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        SetupParticleSystem();

        if (_playerController == null && playerObject != null)
            SetupPlayerReferences();

        if (wasPlaying && _playerController != null && _playerController.HasCatnip && (!_isRemotePlayer || showForRemotePlayers))
            _particleSystem.Play(true);
    }
    #endif
}
