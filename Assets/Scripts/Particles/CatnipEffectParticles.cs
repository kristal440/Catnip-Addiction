using UnityEngine;
using Photon.Pun;
using static UnityEngine.Color;
using static UnityEngine.Debug;
using static UnityEngine.Mathf;
using static UnityEngine.Vector3;

public class CatnipEffectParticles : MonoBehaviour
{
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
    private Gradient _localPlayerGradient;
    private Gradient _remotePlayerGradient;

    private void Awake()
    {
        TryGetComponent(out _particleSystem);
        if (_particleSystem == null)
        {
            var o = gameObject;
            LogWarning($"CatnipEffectParticles: Missing ParticleSystem component on {o.name}. Adding it automatically.", o);
            _particleSystem = gameObject.AddComponent<ParticleSystem>();
        }

        TryGetComponent(out _particleSystemRenderer);
        if (_particleSystemRenderer == null)
        {
            var o = gameObject;
            LogWarning($"CatnipEffectParticles: Missing ParticleSystemRenderer component on {o.name}. Adding it automatically.", o);
            _particleSystemRenderer = gameObject.AddComponent<ParticleSystemRenderer>();
        }

        if (enableColorOverLifetime && colorOverLifetime != null)
        {
            _localPlayerGradient = colorOverLifetime;
            _remotePlayerGradient = CreateRemotePlayerGradient(_localPlayerGradient);
        }

        SetupPlayerReferences();

        if (particleMaterial == null && _particleSystemRenderer.sharedMaterial == null)
        {
            var o = gameObject;
            LogWarning($"CatnipEffectParticles: Particle Material is not assigned on {o.name}. Particles may not render correctly.", o);
        }

        SetupParticleSystem();

        if (_playerController == null) return;

        _wasActiveLastFrame = _playerController.HasCatnip;
        UpdateParticleState(_wasActiveLastFrame, true);
    }

    private void SetupPlayerReferences()
    {
        if (playerObject == null)
        {
            LogError("CatnipEffectParticles: Player GameObject reference is missing. Please assign it in the inspector.", gameObject);
            enabled = false;
            return;
        }

        _playerTransform = playerObject.transform;
        _playerController = playerObject.GetComponent<PlayerController>();

        if (_playerController == null)
        {
            LogError($"CatnipEffectParticles: PlayerController component not found on {playerObject.name}. Disabling script.", gameObject);
            enabled = false;
            return;
        }

        _playerPhotonView = playerObject.GetComponent<PhotonView>();
        _isRemotePlayer = _playerPhotonView != null && !_playerPhotonView.IsMine;

        _lastParentScale = _playerTransform.localScale;
        UpdateObjectScale();
    }

    private Gradient CreateRemotePlayerGradient(Gradient sourceGradient)
    {
        var remoteGradient = new Gradient();
        remoteGradient.SetKeys(
            sourceGradient.colorKeys,
            new GradientAlphaKey[sourceGradient.alphaKeys.Length]
        );

        for (int i = 0; i < sourceGradient.alphaKeys.Length; i++)
        {
            var originalKey = sourceGradient.alphaKeys[i];
            remoteGradient.alphaKeys[i] = new GradientAlphaKey(
                originalKey.alpha * remotePlayerOpacityMultiplier,
                originalKey.time
            );
        }

        return remoteGradient;
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
        if (_particleSystem == null || _particleSystemRenderer == null)
        {
            LogError("SetupParticleSystem called but components are missing (this should not happen after Awake).", gameObject);
            return;
        }

        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        SetupMainModule();
        SetupEmissionModule();
        SetupShapeModule();
        SetupColorOverLifetimeModule();
        SetupSizeOverLifetimeModule();
        SetupRotationOverLifetimeModule();
        SetupTextureSheetAnimationModule();
        SetupNoiseModule();
        SetupRendererModule();
    }

    private void SetupMainModule()
    {
        var main = _particleSystem.main;
        main.duration = duration;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(startLifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(startSpeedMin, startSpeedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(startSizeMin, startSizeMax);
        main.startColor = new ParticleSystem.MinMaxGradient(startColorMin, startColorMax);
        main.gravityModifier = gravityModifier;
        main.playOnAwake = false;
        main.maxParticles = maxParticles;
        main.simulationSpace = simulationSpace;
    }

    private void SetupEmissionModule()
    {
        var emission = _particleSystem.emission;
        emission.rateOverTime = rateOverTime;
        emission.enabled = false;
    }

    private void SetupShapeModule()
    {
        var shape = _particleSystem.shape;
        shape.shapeType = shapeType;
        shape.radius = shapeRadius;
        shape.radiusThickness = radiusThickness;
    }

    private void SetupColorOverLifetimeModule()
    {
        var col = _particleSystem.colorOverLifetime;
        col.enabled = enableColorOverLifetime;
        if (enableColorOverLifetime)
        {
            if (_isRemotePlayer)
                col.color = new ParticleSystem.MinMaxGradient(_remotePlayerGradient);
            else
                col.color = new ParticleSystem.MinMaxGradient(_localPlayerGradient);
        }
    }

    private void SetupSizeOverLifetimeModule()
    {
        var sol = _particleSystem.sizeOverLifetime;
        sol.enabled = enableSizeOverLifetime;
        if (enableSizeOverLifetime)
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeOverLifetime);
    }

    private void SetupRotationOverLifetimeModule()
    {
        var rot = _particleSystem.rotationOverLifetime;
        rot.enabled = enableRotationOverLifetime;
        if (!enableRotationOverLifetime) return;

        rot.z = useRotationCurve ? new ParticleSystem.MinMaxCurve(1f, rotationOverLifetime) : new ParticleSystem.MinMaxCurve(rotationSpeedMin * Deg2Rad, rotationSpeedMax * Deg2Rad);
    }

    private void SetupTextureSheetAnimationModule()
    {
        var texSheetAnim = _particleSystem.textureSheetAnimation;
        texSheetAnim.enabled = enableTextureSheetAnimation;
        if (!enableTextureSheetAnimation) return;

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

    private void SetupNoiseModule()
    {
        var noise = _particleSystem.noise;
        noise.enabled = enableNoise;
        if (!enableNoise) return;

        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = noiseScrollSpeed;
    }

    private void SetupRendererModule()
    {
        if (particleMaterial != null) _particleSystemRenderer.material = particleMaterial;

        _particleSystemRenderer.renderMode = renderMode;
        _particleSystemRenderer.sortMode = sortMode;
        _particleSystemRenderer.normalDirection = normalDirection;

        _particleSystemRenderer.sortingLayerName = sortingLayerName;
        _particleSystemRenderer.sortingOrder = sortingOrder;
    }

    private void Update()
    {
        if (!_playerController || !_particleSystem || !_playerTransform) return;

        if (!Approximately(_playerTransform.localScale.x, _lastParentScale.x) ||
            !Approximately(_playerTransform.localScale.y, _lastParentScale.y) ||
            !Approximately(_playerTransform.localScale.z, _lastParentScale.z))
        {
            _lastParentScale = _playerTransform.localScale;
            UpdateObjectScale();
        }

        bool isCurrentlyActive;

        if (_isRemotePlayer)
            isCurrentlyActive = showForRemotePlayers && _playerController.HasCatnip;
        else
            isCurrentlyActive = _playerController.HasCatnip;

        if (isCurrentlyActive == _wasActiveLastFrame) return;

        UpdateParticleState(isCurrentlyActive, false);
        _wasActiveLastFrame = isCurrentlyActive;
    }

    private void UpdateParticleState(bool isActive, bool isInitialSetup)
    {
        if (!_particleSystem) return;

        var emission = _particleSystem.emission;

        if (isActive)
        {
            if (emission.enabled && _particleSystem.isPlaying) return;

            emission.enabled = true;
            _particleSystem.Play();
        }
        else
        {
            if (emission.enabled || _particleSystem.isPlaying)
                _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (isInitialSetup)
                _particleSystem.Clear(true);
        }
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

        if (_particleSystem == null)
            TryGetComponent(out _particleSystem);
        if (_particleSystemRenderer == null)
            TryGetComponent(out _particleSystemRenderer);

        if (_particleSystem == null || _particleSystemRenderer == null || !Application.isPlaying) return;

        SetupParticleSystem();

        if (_playerController == null && playerObject != null)
            SetupPlayerReferences();

        if (_playerController != null)
            UpdateParticleState(_playerController.HasCatnip, false);
    }
    #endif
}
