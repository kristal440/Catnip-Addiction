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
    [SerializeField] private float explosionDuration = 10f;

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
        while (_currentHazardCollider)
        {
            var overlapPercentage = GetOverlapPercentage(_playerCollider, _currentHazardCollider);

            if (overlapPercentage >= overlapThreshold)
            {
                StartCoroutine(RespawnPlayer());
                break;
            }

            yield return null;
        }
    }

    private static float GetOverlapPercentage(Collider2D collider1, Collider2D collider2)
    {
        var bounds1 = collider1.bounds;
        var bounds2 = collider2.bounds;

        var intersects = bounds1.Intersects(bounds2);

        if (!intersects)
            return 0f;

        var minX = Mathf.Max(bounds1.min.x, bounds2.min.x);
        var minY = Mathf.Max(bounds1.min.y, bounds2.min.y);
        var maxX = Mathf.Min(bounds1.max.x, bounds2.max.x);
        var maxY = Mathf.Min(bounds1.max.y, bounds2.max.y);

        var overlapArea = (maxX - minX) * (maxY - minY);
        var playerArea = bounds1.size.x * bounds1.size.y;

        return overlapArea / playerArea;
    }

    private IEnumerator RespawnPlayer()
    {
        _isRespawning = true;
        SetPlayerMovementEnabled(false);

        playerSpriteToHide.enabled = false;
        playerCanvasToHide.enabled = false;
        var explosion = Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);
        Destroy(explosion, explosionDuration);
        cameraController.OnPlayerDeath();
        deathBorderEffect.ShowDeathBorder();

        playerController.OnPlayerDeath();

        yield return new WaitForSeconds(respawnDelay);

        playerController.RespawnAtLastCheckpoint();

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
