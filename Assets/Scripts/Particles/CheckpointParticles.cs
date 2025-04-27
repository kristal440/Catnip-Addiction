using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Manages firework particle effects that trigger when players reach checkpoints
/// </summary>
public class CheckpointParticles : MonoBehaviour
{
    [Header("Particle Settings")]
    [SerializeField] [Tooltip("How many fireworks to spawn per checkpoint trigger")] private int fireworkCount = 5;
    [SerializeField] [Tooltip("Radius around checkpoint where fireworks can spawn")] private float spawnRadius = 3f;
    [SerializeField] [Tooltip("Minimum height offset for fireworks")] private float minHeightOffset = 2f;
    [SerializeField] [Tooltip("Maximum height offset for fireworks")] private float maxHeightOffset = 5f;
    [SerializeField] [Tooltip("Duration of the firework particle effect")] private float fireworkDuration = 2f;

    [Header("Particle Configuration")]
    [SerializeField] [Tooltip("Number of particles per burst")] private int particleCount = 50;
    [SerializeField] [Tooltip("Minimum particle speed")] private float minSpeed = 2f;
    [SerializeField] [Tooltip("Maximum particle speed")] private float maxSpeed = 5f;
    [SerializeField] [Tooltip("Minimum particle lifetime")] private float minParticleLifetime = 0.5f;
    [SerializeField] [Tooltip("Maximum particle lifetime")] private float maxParticleLifetime = 1.5f;
    [SerializeField] [Tooltip("Minimum particle size")] private float minParticleSize = 0.1f;
    [SerializeField] [Tooltip("Maximum particle size")] private float maxParticleSize = 0.3f;

    [Header("Colors")]
    [SerializeField] [Tooltip("Random colors to use for the fireworks")] private Color[] fireworkColors = { new(1.0f, 0.7f, 0.7f), new(0.7f, 0.7f, 1.0f), new(0.7f, 1.0f, 0.7f), new(1.0f, 1.0f, 0.7f), new(1.0f, 0.7f, 1.0f) };

    /// Triggers firework particles locally (for single-player or direct calls)
    internal void TriggerFireworksLocally(Vector3 previousCheckpointPosition)
    {
        if (Vector3.Distance(transform.position, previousCheckpointPosition) < 0.01f)
            return;

        SpawnFireworkEffects(transform.position);
    }

    /// Spawns the actual firework particle effects at the given position
    private void SpawnFireworkEffects(Vector3 checkpointPosition)
    {
        for (var i = 0; i < fireworkCount; i++)
        {
            var randomOffset = Random.insideUnitCircle * spawnRadius;
            var heightOffset = Random.Range(minHeightOffset, maxHeightOffset);
            var spawnPosition = new Vector3(
                checkpointPosition.x + randomOffset.x,
                checkpointPosition.y + heightOffset,
                0
            );

            var randomColor = fireworkColors[Random.Range(0, fireworkColors.Length)];
            var fireworkObj = CreateFireworkParticleSystem(spawnPosition, randomColor);

            Destroy(fireworkObj, fireworkDuration);
        }
    }

    /// Creates a firework particle system at the specified position with the given color
    private GameObject CreateFireworkParticleSystem(Vector3 position, Color color)
    {
        var fireworkObject = new GameObject("Firework")
        {
            transform =
            {
                position = position
            }
        };

        var addComponent = fireworkObject.AddComponent<ParticleSystem>();

        var main = addComponent.main;
        main.startColor = color;
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(minParticleSize, maxParticleSize);
        main.startLifetime = new ParticleSystem.MinMaxCurve(minParticleLifetime, maxParticleLifetime);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = particleCount;

        var emission = addComponent.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0.0f, (short)particleCount)
        });

        var shape = addComponent.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.1f;

        var velocity = addComponent.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(-4f, -2f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var colorOverLifetime = addComponent.colorOverLifetime;
        colorOverLifetime.enabled = true;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0.0f), new GradientColorKey(color, 0.7f) },
            new[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLifetime.color = gradient;

        var particleSystemRenderer = addComponent.GetComponent<ParticleSystemRenderer>();
        particleSystemRenderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        particleSystemRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleSystemRenderer.sortMode = ParticleSystemSortMode.Distance;

        addComponent.Play();

        return fireworkObject;
    }
}
