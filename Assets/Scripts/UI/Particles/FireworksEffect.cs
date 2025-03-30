using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 4 prompts to gemini 2.5 pro exp
public class FireworksManager : MonoBehaviour
{
    [Header("Firework Burst Settings")]
    [Tooltip("The material to use for the firework particles. A simple additive particle material works well for dots.")]
    public Material particleMaterial;

    [Tooltip("List of possible colors for the firework particles. Each particle will pick randomly from this gradient.")]
    public List<Color> burstColors = new() { Color.red, Color.yellow, Color.white, Color.cyan, Color.magenta, Color.green };

    [Tooltip("Number of particles per individual burst.")]
    [Range(50, 1000)]
    public int particleCountPerBurst = 500;

    [Tooltip("How long the particles last before fading.")]
    [Range(0.5f, 5.0f)]
    public float particleLifetime = 2.0f;

    [Tooltip("Initial speed of the particles exploding outwards.")]
    [Range(1.0f, 20.0f)]
    public float burstSpeed = 10.0f;

    [Tooltip("Size of the individual particles.")]
    [Range(0.01f, 0.5f)]
    public float particleSize = 0.05f;

    [Tooltip("How much gravity affects the particles (1 = normal gravity).")]
    [Range(0.0f, 2.0f)]
    public float gravityModifier = 0.3f;

    [Header("Sparkle Trail Settings")]
    [Tooltip("Enable subtle trails for a sparkle effect.")]
    public bool enableTrails = true;

    [Tooltip("Material for the particle trails (often same/similar to the main material).")]
    public Material trailMaterial;

    [Tooltip("Proportion of particles that will have trails (0 to 1). Lower for sparser sparkles.")]
    [Range(0.0f, 1.0f)]
    public float trailRatio = 0.2f;

    [Tooltip("How long the trails last (relative to particle lifetime).")]
    [Range(0.1f, 1.0f)]
    public float trailLifetimeFactor = 0.3f;

    [Tooltip("Width of the trails.")]
    [Range(0.01f, 0.5f)]
    public float trailWidth = 0.02f;

    [Header("Firework Sequence")]
    [Tooltip("Number of bursts to spawn when TriggerFireworks is called.")]
    public int sequenceBurstCount = 5;

    [Tooltip("Delay in seconds between each burst in the sequence.")]
    public float sequenceDelay = 0.3f;

    [Tooltip("Maximum random offset distance from the base position for each burst.")]
    public float sequencePositionVariance = 1.5f;


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
