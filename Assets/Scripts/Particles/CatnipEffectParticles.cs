using UnityEngine;
using Photon.Pun;
using static UnityEngine.Color;
using static UnityEngine.Mathf;
using static UnityEngine.Vector3;

public class CatnipEffectParticles : MonoBehaviour
{
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    [Header("Player Reference")]
    [SerializeField] private GameObject playerObject;

    [Header("Particle Main Module")]
    [SerializeField] private float duration = 5f;
    [SerializeField] private float startLifetime = 2f;
    [SerializeField] private float startSpeedMin = 0.1f;
    [SerializeField] private float startSpeedMax = 0.5f;
    [SerializeField] private float startSizeMin = 0.2f;
    [SerializeField] private float startSizeMax = 0.5f;
    [SerializeField] private Color startColorMin = green;
    [SerializeField] private Color startColorMax = new(0.5f, 1f, 0.5f, 1f);
    [SerializeField] private float gravityModifier;
    [SerializeField] private int maxParticles = 50;
    [SerializeField] private ParticleSystemSimulationSpace simulationSpace = ParticleSystemSimulationSpace.Local;

    [Header("Emission Module")]
    [SerializeField] private float rateOverTime = 10f;

    [Header("Shape Module")]
    [SerializeField] private ParticleSystemShapeType shapeType = ParticleSystemShapeType.Sphere;
    [SerializeField] private float shapeRadius = 0.75f;
    [Range(0f, 1f)]
    [SerializeField] private float radiusThickness = 1f;

    [Header("Lifetime Modules")]
    [SerializeField] private bool enableColorOverLifetime = true;
    [SerializeField] private Gradient colorOverLifetime = DefaultFadeOutGradient();
    [SerializeField] private bool enableSizeOverLifetime = true;
    [SerializeField] private AnimationCurve sizeOverLifetime = DefaultShrinkCurve();

    [Header("Rotation Module")]
    [SerializeField] private bool enableRotationOverLifetime = true;
    [SerializeField] private float rotationSpeedMin;
    [SerializeField] private float rotationSpeedMax = 90f;
    [SerializeField] private bool useRotationCurve;
    [SerializeField] private AnimationCurve rotationOverLifetime = DefaultLinearCurve();

    [Header("Texture Sheet Animation Module")]
    [SerializeField] private bool enableTextureSheetAnimation = true;
    [SerializeField] private int textureSheetTilesX = 1;
    [SerializeField] private int textureSheetTilesY = 4;

    [Header("Noise Module")]
    [SerializeField] private bool enableNoise = true;
    [SerializeField] private float noiseStrength = 0.1f;
    [SerializeField] private float noiseFrequency = 0.5f;
    [SerializeField] private float noiseScrollSpeed = 0.5f;

    [Header("Renderer Module")]
    [SerializeField] private Material particleMaterial;
    [SerializeField] private ParticleSystemRenderMode renderMode = ParticleSystemRenderMode.Billboard;
    [SerializeField] private ParticleSystemSortMode sortMode = ParticleSystemSortMode.None;
    [Range(-1f, 1f)]
    [SerializeField] private float normalDirection = 1f;
    [SerializeField] private string sortingLayerName = "Foreground";
    [SerializeField] private int sortingOrder = 10;

    [Header("Remote Player Settings")]
    [SerializeField] private bool showForRemotePlayers = true;
    [Range(0.0f, 1.0f)]
    [SerializeField] private float remotePlayerOpacityMultiplier = 0.7f;

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

    private void Awake()
    {
        _particleSystem = GetComponent<ParticleSystem>() ? GetComponent<ParticleSystem>() : gameObject.AddComponent<ParticleSystem>();
        _particleSystemRenderer = GetComponent<ParticleSystemRenderer>() ? GetComponent<ParticleSystemRenderer>() : gameObject.AddComponent<ParticleSystemRenderer>();

        if (enableColorOverLifetime && colorOverLifetime != null)
            _baseGradient = colorOverLifetime;

        PrepareParticleMaterials();
    }

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

    private void Start()
    {
        if (!SetupPlayerReferences())
            return;

        SetupParticleSystem();
        _wasActiveLastFrame = false;
        UpdateParticleState(_playerController.HasCatnip);
        _hasCatnipLastFrame = _playerController.HasCatnip;
    }

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

    private void SetupParticleSystem()
    {
        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

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
    }

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

    private void SetupRenderer()
    {
        if (_isRemotePlayer && _remotePlayerMaterial != null)
            _particleSystemRenderer.material = _remotePlayerMaterial;
        else if (_localPlayerMaterial != null)
            _particleSystemRenderer.material = _localPlayerMaterial;
        else if (particleMaterial != null)
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

    private void Update()
    {
        if (!_playerController || !_particleSystem || !_playerTransform)
            return;

        // Check for scale changes
        if (_playerTransform.localScale != _lastParentScale)
        {
            _lastParentScale = _playerTransform.localScale;
            UpdateObjectScale();
        }

        // Check for catnip status change
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

    private void UpdateParticleState(bool isActive)
    {
        if (!_particleSystem) return;

        if (isActive)
            _particleSystem.Play(true);
        else
            _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting); // Changed from StopEmittingAndClear to StopEmitting
    }

    /// <summary>
    /// Force clears all particles, use for teleporting or respawning
    /// </summary>
    public void ClearAllParticles()
    {
        if (!_particleSystem) return;
        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

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

    private static AnimationCurve DefaultShrinkCurve()
    {
        return new AnimationCurve(new Keyframe(0.0f, 1.0f), new Keyframe(1.0f, 0.0f));
    }

    private static AnimationCurve DefaultLinearCurve()
    {
        return new AnimationCurve(new Keyframe(0.0f, 0.0f), new Keyframe(1.0f, 1.0f));
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (!gameObject.scene.isLoaded) return;

        _particleSystem = GetComponent<ParticleSystem>();
        _particleSystemRenderer = GetComponent<ParticleSystemRenderer>();

        if (_particleSystem == null || _particleSystemRenderer == null || !Application.isPlaying) return;

        SetupParticleSystem();

        if (_playerController == null && playerObject != null)
            SetupPlayerReferences();

        if (_playerController != null)
            UpdateParticleState(_playerController.HasCatnip && (!_isRemotePlayer || showForRemotePlayers));
    }
    #endif
}
