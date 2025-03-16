using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CheckpointManager))]
public class PlayerDeathHandler : MonoBehaviour
{
    [SerializeField] private string hazardTag = "Death";
    [SerializeField] private float respawnDelay = 1f;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private DynamicCameraController cameraController;

    private bool _isRespawning;

    private void Start()
    {
        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<DynamicCameraController>();
            if (cameraController == null)
            {
                Debug.LogWarning("DynamicCameraController not found. Camera zoom on death won't work.");
            }
        }

        if (playerController != null) return;
        playerController = GetComponent<PlayerController>();
        if (playerController == null)
        {
            Debug.LogWarning("PlayerController reference is missing!");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag(hazardTag) && !_isRespawning)
        {
            StartCoroutine(RespawnPlayer());
        }
    }

    private IEnumerator RespawnPlayer()
    {
        _isRespawning = true;
        SetPlayerMovementEnabled(false);

        // Trigger camera zoom on death
        if (cameraController)
        {
            cameraController.OnPlayerDeath();
        }

        yield return new WaitForSeconds(respawnDelay);

        playerController.Teleport(CheckpointManager.LastCheckpointPosition);

        // Reset camera to normal after respawn
        if (cameraController)
        {
            cameraController.OnPlayerRespawn();
        }

        SetPlayerMovementEnabled(true);
        _isRespawning = false;
    }

    private void SetPlayerMovementEnabled(bool movementEnabled)
    {
        if (playerController)
        {
            playerController.SetMovement(movementEnabled);
        }
        else
        {
            Debug.LogWarning("PlayerController reference is missing!");
        }
    }
}