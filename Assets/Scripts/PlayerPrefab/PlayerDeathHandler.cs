using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CheckpointManager))]
public class PlayerDeathHandler : MonoBehaviour
{
    [SerializeField] private string hazardTag = "Death";
    [SerializeField] private float respawnDelay = 1f;
    [SerializeField] private PlayerController playerController;

    private bool _isRespawning;

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

        yield return new WaitForSeconds(respawnDelay);

        playerController.Teleport(CheckpointManager.LastCheckpointPosition);

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