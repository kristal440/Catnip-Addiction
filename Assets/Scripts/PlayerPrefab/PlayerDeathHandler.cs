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

    private bool _isRespawning;

    private void Start()
    {
        cameraController = FindFirstObjectByType<DynamicCameraController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(hazardTag) || _isRespawning) return;
        StartCoroutine(RespawnPlayer());
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
