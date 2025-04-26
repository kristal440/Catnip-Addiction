using System.Collections;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Creates and manages teleportation particle effects with customizable appearance, movement, and lighting
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

    private ParticleSystem _particleSystem;
    private ParticleSystem.EmissionModule _emission;

    // Initializes the component and creates the particle system
    private void Awake()
    {
        CreateParticleSystem();
    }

    // Sets up particle system with all configured parameters
    private void CreateParticleSystem()
    {
        _particleSystem = gameObject.AddComponent<ParticleSystem>();

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

        var particleMaterial = new Material(Shader.Find("Particles/Standard Unlit"));
        particleMaterial.EnableKeyword("_EMISSION");
        particleMaterial.SetColor(EmissionColor, startColor * emissionMultiplier);

        particleMaterial.SetFloat(Mode, 2);
        particleMaterial.SetInt(SrcBlend, (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        particleMaterial.SetInt(DstBlend, (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        particleMaterial.SetInt(ZWrite, 0);
        particleMaterial.DisableKeyword("_ALPHATEST_ON");
        particleMaterial.EnableKeyword("_ALPHABLEND_ON");
        particleMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        particleMaterial.renderQueue = 3000;

        particleSystemRenderer.material = particleMaterial;
        particleSystemRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleSystemRenderer.sortingOrder = 10;

        main.startSize = particleSize;
        main.startSizeMultiplier = particleSize;

        if (!emitLight) return;

        var lightsModule = _particleSystem.lights;
        lightsModule.enabled = true;
        lightsModule.ratio = lightRatio;
        lightsModule.useParticleColor = true;
        lightsModule.intensityMultiplier = lightIntensity;
        lightsModule.rangeMultiplier = lightRange;

        var tempLightObj = new GameObject("TempLight");
        var tempLight = tempLightObj.AddComponent<Light>();
        tempLight.color = lightColor;
        tempLight.intensity = lightIntensity;
        tempLight.range = lightRange;
        tempLight.type = LightType.Point;

        lightsModule.light = tempLight;

        Destroy(tempLightObj);
    }

    // Initiates teleport animation between two points
    internal void AnimateTeleport(Vector3 startPos, Vector3 endPos, float duration, AnimationCurve movementCurve = null)
    {
        gameObject.SetActive(true);
        _particleSystem.Clear();

        StartCoroutine(TeleportAnimation(startPos, endPos, duration, movementCurve));
    }

    // Handles the movement and particle animation over time
    private IEnumerator TeleportAnimation(Vector3 startPos, Vector3 endPos, float duration, AnimationCurve movementCurve)
    {
        movementCurve ??= AnimationCurve.EaseInOut(0, 0, 1, 1);

        _particleSystem.Play();

        var startTime = Time.time;
        var endTime = startTime + duration;

        while (Time.time < endTime)
        {
            var normalizedTime = (Time.time - startTime) / duration;
            var curvedProgress = movementCurve.Evaluate(normalizedTime);

            transform.position = Vector3.Lerp(startPos, endPos, curvedProgress);

            var velocityFactor = movementCurve.Evaluate(normalizedTime + 0.01f) -
                                 movementCurve.Evaluate(normalizedTime - 0.01f);
            _emission.rateOverTime = emissionRate * (1 + Mathf.Abs(velocityFactor) * 20);

            yield return null;
        }

        transform.position = endPos;
        _emission.rateOverTime = 0;
        yield return new WaitForSeconds(3);

        Destroy(gameObject);
    }
}
