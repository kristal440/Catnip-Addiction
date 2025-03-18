using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CheckpointManager))]
public class PlayerDeathHandler : MonoBehaviour
{
    [SerializeField] private string hazardTag = "Death";
    [SerializeField] private float respawnDelay = 1f;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private DynamicCameraController cameraController;
    [SerializeField] private DeathBorderEffect deathBorderEffect;
    [SerializeField] private GameObject deathExplosionPrefab;
    [SerializeField] private SpriteRenderer playerSpriteToHide;
    [SerializeField] private Canvas playerCanvasToHide;
    [SerializeField, Range(0f, 1f)] private float colliderOverlapThreshold = 0.3f; // Percentage of overlap required to die

    private bool _isRespawning;
    private Collider2D _playerCollider;

    private void Start()
    {
        cameraController = FindFirstObjectByType<DynamicCameraController>();
        _playerCollider = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(hazardTag) || _isRespawning) return;
        var overlapPercentage = CalculateOverlapPercentage(_playerCollider, other);
        if (overlapPercentage >= colliderOverlapThreshold)
        {
            StartCoroutine(RespawnPlayer());
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag(hazardTag) || _isRespawning) return;
        var overlapPercentage = CalculateOverlapPercentage(_playerCollider, other);
        if (overlapPercentage >= colliderOverlapThreshold)
        {
            StartCoroutine(RespawnPlayer());
        }
    }

    private static float CalculateOverlapPercentage(Collider2D collider1, Collider2D collider2)
    {
        var bounds1 = collider1.bounds;
        var bounds2 = collider2.bounds;

        var intersection = new Bounds();
        intersection.SetMinMax(
            Vector3.Max(bounds1.min, bounds2.min),
            Vector3.Min(bounds1.max, bounds2.max)
        );

        if (intersection.size.x <= 0 || intersection.size.y <= 0)
            return 0;

        var overlapArea = intersection.size.x * intersection.size.y;
        var playerArea = bounds1.size.x * bounds1.size.y;

        return overlapArea / playerArea;
    }

    private IEnumerator RespawnPlayer()
    {
        _isRespawning = true;
        SetPlayerMovementEnabled(false);

        // Death
        playerController.OnPlayerDeath();
        playerSpriteToHide.enabled = false;
        playerCanvasToHide.enabled = false;
        Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);
        cameraController.OnPlayerDeath();
        deathBorderEffect.ShowDeathBorder();

        yield return new WaitForSeconds(respawnDelay);

        // Respawn
        playerController.Teleport(CheckpointManager.LastCheckpointPosition);
        playerController.OnPlayerRespawn();
        playerSpriteToHide.enabled = true;
        playerCanvasToHide.enabled = true;
        cameraController.OnPlayerRespawn();
        deathBorderEffect.HideDeathBorder();

        SetPlayerMovementEnabled(true);
        _isRespawning = false;
    }

    private void SetPlayerMovementEnabled(bool movementEnabled)
    {
        playerController.SetMovement(movementEnabled);
    }
}
