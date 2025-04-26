using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Manages and spawns particle-based firework effects with customizable burst patterns and sparkle trails
/// </summary>
public class FireworksManager : MonoBehaviour
{
    [Header("Firework Burst Settings")]
    [SerializeField] [Tooltip("The material to use for the firework particles. A simple additive particle material works well for dots.")] private Material particleMaterial;
    [SerializeField] [Tooltip("List of possible colors for the firework particles. Each particle will pick randomly from this gradient.")] private List<Color> burstColors = new() { Color.red, Color.yellow, Color.white, Color.cyan, Color.magenta, Color.green };
    [SerializeField] [Range(50, 1000)] [Tooltip("Number of particles per individual burst.")] private int particleCountPerBurst = 500;
    [SerializeField] [Range(0.5f, 5.0f)] [Tooltip("How long the particles last before fading.")] private float particleLifetime = 2.0f;
    [SerializeField] [Range(1.0f, 20.0f)] [Tooltip("Initial speed of the particles exploding outwards.")] private float burstSpeed = 10.0f;
    [SerializeField] [Range(0.01f, 0.5f)] [Tooltip("Size of the individual particles.")] private float particleSize = 0.05f;
    [SerializeField] [Range(0.0f, 2.0f)] [Tooltip("How much gravity affects the particles (1 = normal gravity).")] private float gravityModifier = 0.3f;

    [Header("Sparkle Trail Settings")]
    [SerializeField] [Tooltip("Enable subtle trails for a sparkle effect.")] private bool enableTrails = true;
    [SerializeField] [Tooltip("Material for the particle trails (often same/similar to the main material).")] private Material trailMaterial;
    [SerializeField] [Range(0.0f, 1.0f)] [Tooltip("Proportion of particles that will have trails (0 to 1). Lower for sparser sparkles.")] private float trailRatio = 0.2f;
    [SerializeField] [Range(0.1f, 1.0f)] [Tooltip("How long the trails last (relative to particle lifetime).")] private float trailLifetimeFactor = 0.3f;
    [SerializeField] [Range(0.01f, 0.5f)] [Tooltip("Width of the trails.")] private float trailWidth = 0.02f;

    [Header("Firework Sequence")]
    [SerializeField] [Tooltip("Number of bursts to spawn when TriggerFireworks is called.")] private int sequenceBurstCount = 5;
    [SerializeField] [Tooltip("Delay in seconds between each burst in the sequence.")] private float sequenceDelay = 0.3f;
    [SerializeField] [Tooltip("Maximum random offset distance from the base position for each burst.")] private float sequencePositionVariance = 1.5f;

    /// Initiates a sequence of firework effects at the specified position
    internal void TriggerFireworksSequence(Vector3 basePosition)
    {
        if (particleMaterial == null)
        {
            Debug.LogError("FireworksManager: Particle Material is not assigned!", this);
            return;
        }
        if (enableTrails && trailMaterial == null) Debug.LogWarning("FireworksManager: Enable Trails is true, but Trail Material is not assigned! Disabling trails for this sequence.", this);

        StartCoroutine(FireworkSequenceCoroutine(basePosition));
    }

    /// Coroutine that spawns multiple firework bursts with delays between each burst
    private IEnumerator FireworkSequenceCoroutine(Vector3 basePosition)
    {
        var trailsActuallyEnabled = enableTrails && trailMaterial;

        for (var i = 0; i < sequenceBurstCount; i++)
        {
            var randomOffset = Random.insideUnitCircle * sequencePositionVariance;
            var spawnPosition = basePosition + new Vector3(randomOffset.x, randomOffset.y, 0);

            SpawnSingleFireworkBurst(spawnPosition, trailsActuallyEnabled);

            yield return new WaitForSeconds(sequenceDelay);
        }
    }

    /// Creates a single firework burst with particle system at the specified position
    private void SpawnSingleFireworkBurst(Vector3 position, bool useTrails)
    {
        var fireworkObject = new GameObject("FireworkBurstEffect")
        {
            transform =
            {
                position = position,
                localScale = Vector3.one
            }
        };

        var ps = fireworkObject.AddComponent<ParticleSystem>();
        var psr = ps.GetComponent<ParticleSystemRenderer>();

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.duration = 0.5f;
        main.loop = false;
        main.startLifetime = particleLifetime;
        main.startSpeed = burstSpeed;
        main.startSize = particleSize;
        main.maxParticles = particleCountPerBurst + 50;
        main.gravityModifier = gravityModifier;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.stopAction = ParticleSystemStopAction.Destroy;

        var colorGradient = new Gradient();
        if (burstColors is { Count: > 0 })
        {
            var colorKeys = new GradientColorKey[burstColors.Count];

            GradientAlphaKey[] alphaKeys = { new(1.0f, 0.0f), new(1.0f, 1.0f) };
            for (var i = 0; i < burstColors.Count; i++)
            {
                var time = (burstColors.Count > 1) ? (float)i / (burstColors.Count - 1) : 0.0f;
                colorKeys[i] = new GradientColorKey(burstColors[i], time);
            }
            colorGradient.SetKeys(colorKeys, alphaKeys);
        }
        else
        {
            colorGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 1.0f) }
            );
        }

        var startColor = main.startColor;
        startColor.mode = ParticleSystemGradientMode.RandomColor;
        startColor.gradient = colorGradient;
        main.startColor = startColor;


        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0.0f, (short)particleCountPerBurst)
        });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;


        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.7f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLifetime.color = fadeGradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
            new Keyframe(0.0f, 1.0f),
            new Keyframe(0.8f, 0.8f),
            new Keyframe(1.0f, 0.2f)
        ));

        var trails = ps.trails;
        trails.enabled = useTrails;
        if (useTrails)
        {
            trails.lifetime = new ParticleSystem.MinMaxCurve(particleLifetime * trailLifetimeFactor);
            trails.ratio = trailRatio;
            trails.minVertexDistance = 0.1f;
            trails.worldSpace = true;
            trails.widthOverTrail = new ParticleSystem.MinMaxCurve(trailWidth);

            var trailGradient = new Gradient();
            trailGradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new[] { new GradientAlphaKey(0.6f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            trails.colorOverLifetime = trailGradient;
        }

        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.material = particleMaterial;
        if (useTrails) psr.trailMaterial = trailMaterial;

        ps.Play();
    }
}
