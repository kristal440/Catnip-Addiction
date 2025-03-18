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
    [Range(0f, 1f)][SerializeField] private float overlapThreshold = 0.7f;

    private bool _isRespawning;
    private Collider2D _playerCollider;
    private Collider2D _currentHazardCollider;

    private void Start()
    {
        cameraController = FindFirstObjectByType<DynamicCameraController>();
        _playerCollider = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isRespawning || !other.CompareTag(hazardTag))
            return;

        _currentHazardCollider = other;
        StartCoroutine(CheckOverlapAndDie());
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == _currentHazardCollider)
            _currentHazardCollider = null;
    }

    private IEnumerator CheckOverlapAndDie()
    {
        while (_currentHazardCollider != null)
        {
            float overlapPercentage = GetOverlapPercentage(_playerCollider, _currentHazardCollider);

            if (overlapPercentage >= overlapThreshold)
            {
                StartCoroutine(RespawnPlayer());
                break;
            }

            yield return null;
        }
    }

    private float GetOverlapPercentage(Collider2D collider1, Collider2D collider2)
    {
        // Get bounds of both colliders
        Bounds bounds1 = collider1.bounds;
        Bounds bounds2 = collider2.bounds;

        // Calculate intersection volume
        Bounds intersection = new Bounds();
        bool intersects = bounds1.Intersects(bounds2);

        if (!intersects)
            return 0f;

        // Calculate overlap bounds
        float minX = Mathf.Max(bounds1.min.x, bounds2.min.x);
        float minY = Mathf.Max(bounds1.min.y, bounds2.min.y);
        float maxX = Mathf.Min(bounds1.max.x, bounds2.max.x);
        float maxY = Mathf.Min(bounds1.max.y, bounds2.max.y);

        // Calculate areas
        float overlapArea = (maxX - minX) * (maxY - minY);
        float playerArea = bounds1.size.x * bounds1.size.y;

        // Return percentage of player collider that's overlapping
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