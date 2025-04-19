using System.Collections;
using UnityEngine;

public class TeleportParticles : MonoBehaviour
{
    [Header("Particle Settings")]
    [SerializeField] private Color startColor = new(0.7f, 0.9f, 0.7f, 0.8f);
    [SerializeField] private Color endColor = new(0.5f, 0.8f, 0.5f, 0f);
    [SerializeField] private float particleSize = 0.225f;
    [SerializeField] private float particleLifetime = 1f;
    [SerializeField] private int emissionRate = 30;

    [Header("Spread Settings")]
    [SerializeField] private float emissionRadius = 0.1f;
    [SerializeField] private float spreadForce = 1.0f;
    [SerializeField] private float directionVariance = 0.3f;

    private ParticleSystem _particleSystem;
    private ParticleSystem.EmissionModule _emission;

    private void Awake()
    {
        CreateParticleSystem();
    }

    private void CreateParticleSystem()
    {
        _particleSystem = gameObject.AddComponent<ParticleSystem>();

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
        particleSystemRenderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        particleSystemRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleSystemRenderer.sortingOrder = 10;

        _particleSystem.Stop();
    }

    internal void AnimateTeleport(Vector3 startPos, Vector3 endPos, float duration, AnimationCurve movementCurve = null)
    {
        StartCoroutine(TeleportAnimation(startPos, endPos, duration, movementCurve));
    }

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

        var burst = new ParticleSystem.Burst(0, 20);
        _emission.SetBurst(0, burst);

        _emission.rateOverTime = 0;
        yield return new WaitForSeconds(particleLifetime);

        Destroy(gameObject);
    }
}
