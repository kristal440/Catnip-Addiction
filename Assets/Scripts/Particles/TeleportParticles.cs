using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates and manages teleportation particle effects with customizable appearance, movement, and lighting
/// that works consistently across all platforms (Editor, Windows, Mac, Linux)
/// </summary>
public class TeleportParticles : MonoBehaviour
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    private static readonly int Mode = Shader.PropertyToID("_Mode");
    private static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWrite = Shader.PropertyToID("_ZWrite");

    [Header("Particle Settings")]
    [SerializeField] [Tooltip("Initial color of particles when spawned")] private Color startColor = new(0.7f, 0.9f, 0.7f, 0.8f);
    [SerializeField] [Tooltip("Final color of particles before disappearing")] private Color endColor = new(0.5f, 0.8f, 0.5f, 0f);
    [SerializeField] [Tooltip("Size of individual particles")] private float particleSize = 0.2f;
    [SerializeField] [Tooltip("How long particles remain visible")] private float particleLifetime = 1.5f;
    [SerializeField] [Tooltip("Number of particles emitted per second")] private int emissionRate = 40;

    [Header("Spread Settings")]
    [SerializeField] [Tooltip("Radius of the emission sphere")] private float emissionRadius = 0.1f;
    [SerializeField] [Tooltip("Force applied to particles when emitted")] private float spreadForce = 1.0f;
    [SerializeField] [Tooltip("Random variation in particle movement direction")] private float directionVariance = 0.3f;

    [Header("Light Settings")]
    [SerializeField] [Tooltip("Whether particles should emit light")] private bool emitLight = true;
    [SerializeField] [Tooltip("Brightness of particle lights")] private float lightIntensity = 2f;
    [SerializeField] [Tooltip("How far light reaches from particles")] private float lightRange = 2.0f;
    [SerializeField] [Range(0.0f, 1.0f)] [Tooltip("Percentage of particles that emit light")] private float lightRatio = 0.5f;
    [SerializeField] [Tooltip("Color of the emitted light")] private Color lightColor = Color.green;
    [SerializeField] [Tooltip("Multiplies emission intensity for particle material")] private float emissionMultiplier = 2.0f;
    [SerializeField] [Tooltip("Force pixel lighting for better compatibility")]
    private bool forcePixelLighting = true;

    private ParticleSystem _particleSystem;
    private ParticleSystem.EmissionModule _emission;
    private Light _particleLight;
    private GameObject _lightObject;

    // Initializes the component and creates the particle system
    private void Awake()
    {
        CreateParticleSystem();
    }

    // Sets up particle system with all configured parameters
    private void CreateParticleSystem()
    {
        // Create particle system if it doesn't exist
        if (_particleSystem == null)
        {
            _particleSystem = gameObject.GetComponent<ParticleSystem>();
            if (_particleSystem == null)
            {
                _particleSystem = gameObject.AddComponent<ParticleSystem>();
            }
        }

        _particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = _particleSystem.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = particleLifetime;
        main.startSpeed = spreadForce;
        main.startSize = particleSize;
        main.startSizeMultiplier = particleSize;
        main.startColor = new ParticleSystem.MinMaxGradient(startColor, endColor);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // This is important for cross-platform compatibility
        main.prewarm = false;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;

        _emission = _particleSystem.emission;
        _emission.rateOverTime = emissionRate;

        var shape = _particleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = emissionRadius;

        var velocityOverLifetime = _particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;

        var velocityCurve = new AnimationCurve();
        velocityCurve.AddKey(0.0f, 1.0f);
        velocityCurve.AddKey(1.0f, 0.5f);

        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(
            spreadForce,
            new AnimationCurve(velocityCurve.keys),
            new AnimationCurve(velocityCurve.keys)
        );
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(
            spreadForce,
            new AnimationCurve(velocityCurve.keys),
            new AnimationCurve(velocityCurve.keys)
        );
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(
            spreadForce,
            new AnimationCurve(velocityCurve.keys),
            new AnimationCurve(velocityCurve.keys)
        );

        var noise = _particleSystem.noise;
        noise.enabled = true;
        noise.strength = directionVariance;
        noise.frequency = 0.5f;
        noise.quality = ParticleSystemNoiseQuality.Medium;

        var colorOverLifetime = _particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(startColor, 0.0f), new GradientColorKey(endColor, 1.0f) },
            new[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0f, 1.0f) }
        );

        colorOverLifetime.color = gradient;

        var sizeOverLifetime = _particleSystem.sizeOverLifetime;
        sizeOverLifetime.enabled = true;

        var sizeOverLifetimeCurve = new AnimationCurve();
        sizeOverLifetimeCurve.AddKey(0.0f, 1.0f);
        sizeOverLifetimeCurve.AddKey(1.0f, 0.0f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeOverLifetimeCurve);

        var particleSystemRenderer = _particleSystem.GetComponent<ParticleSystemRenderer>();

        // Find a cross-platform compatible shader - first try getting a built-in one that's always included
        var shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null)
        {
            // Fallback to default particle shader if Standard Unlit isn't available
            shader = Shader.Find("Particles/Standard Surface");

            // Last resort fallback
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
        }

        var particleMaterial = new Material(shader);
        particleMaterial.EnableKeyword("_EMISSION");
        particleMaterial.SetColor(EmissionColor, startColor * emissionMultiplier);

        // Configure the material for transparency
        particleMaterial.SetFloat(Mode, 2);
        particleMaterial.SetInt(SrcBlend, (int)BlendMode.SrcAlpha);
        particleMaterial.SetInt(DstBlend, (int)BlendMode.OneMinusSrcAlpha);
        particleMaterial.SetInt(ZWrite, 0);
        particleMaterial.DisableKeyword("_ALPHATEST_ON");
        particleMaterial.EnableKeyword("_ALPHABLEND_ON");
        particleMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        particleMaterial.renderQueue = 3000;

        particleSystemRenderer.material = particleMaterial;
        particleSystemRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleSystemRenderer.sortingOrder = 10;
        // Use the material's color property instead of the built-in property for better compatibility
        particleSystemRenderer.enableGPUInstancing = true;

        main.startSize = particleSize;
        main.startSizeMultiplier = particleSize;

        if (!emitLight) return;

        // Create a permanent light reference for the particle system
        SetupLight();

        // Configure particle lights module
        var lightsModule = _particleSystem.lights;
        lightsModule.enabled = true;
        lightsModule.ratio = lightRatio;
        lightsModule.useParticleColor = true;
        lightsModule.intensityMultiplier = lightIntensity;
        lightsModule.rangeMultiplier = lightRange;

        // Important: Set these explicitly for cross-platform compatibility
        lightsModule.sizeAffectsRange = false;
        lightsModule.alphaAffectsIntensity = true;
        lightsModule.maxLights = Mathf.Min(8, Mathf.CeilToInt(emissionRate * lightRatio * 0.25f));

        // Apply the reference light
        lightsModule.light = _particleLight;

        // Don't use render pipeline settings to ensure consistent behavior
        if (lightsModule.GetType().GetProperty("useRenderPipelineSettings") != null)
        {
            // This reflection approach works around potential compilation issues
            // if the project doesn't use a SRP
            System.Reflection.PropertyInfo propertyInfo =
                lightsModule.GetType().GetProperty("useRenderPipelineSettings");
            if (propertyInfo != null)
            {
                propertyInfo.SetValue(lightsModule, false);
            }
        }
    }

    // Creates a persistent light reference for the particle system
    private void SetupLight()
    {
        // Clean up any existing light
        if (_lightObject != null)
        {
            Destroy(_lightObject);
            _lightObject = null;
            _particleLight = null;
        }

        // Create a new child object for the light
        _lightObject = new GameObject("ParticleLight");
        _lightObject.transform.SetParent(transform);
        _lightObject.transform.localPosition = Vector3.zero;

        // Add and configure the light component
        _particleLight = _lightObject.AddComponent<Light>();
        _particleLight.color = lightColor;
        _particleLight.intensity = lightIntensity;
        _particleLight.range = lightRange;
        _particleLight.type = LightType.Point;

        // Force pixel lighting for better compatibility across platforms
        if (forcePixelLighting)
        {
            _particleLight.renderMode = LightRenderMode.ForcePixel;
        }

        // These settings help with cross-platform compatibility
        _particleLight.shadows = LightShadows.None;
        _particleLight.cullingMask = 1 << gameObject.layer;
    }

    // Called when the object is destroyed
    private void OnDestroy()
    {
        // Clean up light reference if it exists
        if (_lightObject != null)
        {
            Destroy(_lightObject);
        }
    }

    // Initiates teleport animation between two points
    public void AnimateTeleport(Vector3 startPos, Vector3 endPos, float duration, AnimationCurve movementCurve = null)
    {
        gameObject.SetActive(true);

        // Ensure particle system is properly set up
        if (_particleSystem == null)
        {
            CreateParticleSystem();
        }

        _particleSystem.Clear();

        StartCoroutine(TeleportAnimation(startPos, endPos, duration, movementCurve));
    }

    // Handles the movement and particle animation over time
    private IEnumerator TeleportAnimation(Vector3 startPos, Vector3 endPos, float duration, AnimationCurve movementCurve)
    {
        movementCurve ??= AnimationCurve.EaseInOut(0, 0, 1, 1);

        // Ensure the light is properly set up
        if (emitLight && (_particleLight == null || _lightObject == null))
        {
            SetupLight();

            // Reconnect the light to the particles
            var lightsModule = _particleSystem.lights;
            lightsModule.light = _particleLight;
        }

        _particleSystem.Play();

        var startTime = Time.time;
        var endTime = startTime + duration;

        while (Time.time < endTime)
        {
            var normalizedTime = (Time.time - startTime) / duration;
            var curvedProgress = movementCurve.Evaluate(normalizedTime);

            transform.position = Vector3.Lerp(startPos, endPos, curvedProgress);

            var velocityFactor = movementCurve.Evaluate(Mathf.Min(normalizedTime + 0.01f, 1f)) -
                                 movementCurve.Evaluate(Mathf.Max(normalizedTime - 0.01f, 0f));
            _emission.rateOverTime = emissionRate * (1 + Mathf.Abs(velocityFactor) * 20);

            yield return null;
        }

        transform.position = endPos;
        _emission.rateOverTime = 0;

        // Let remaining particles fade out
        yield return new WaitForSeconds(particleLifetime);

        // Final cleanup
        _particleSystem.Stop();
        Destroy(gameObject);
    }
}
