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

        if (deathBorderEffect == null)
        {
            deathBorderEffect = FindFirstObjectByType<DeathBorderEffect>();
        }

        if (playerSpriteToHide != null) return;
        playerSpriteToHide = GetComponent<SpriteRenderer>();
        if (playerSpriteToHide != null) return;
        playerSpriteToHide = GetComponentInChildren<SpriteRenderer>();
        if (playerSpriteToHide == null)
        {
            Debug.LogWarning("SpriteRenderer not found! Please assign it in the inspector.");
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

        playerSpriteToHide.enabled = false;
        playerCanvasToHide.enabled = false;

        Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);

        cameraController.OnPlayerDeath();

        // Show the red border effect
        if (deathBorderEffect)
        {
            deathBorderEffect.ShowDeathBorder();
        }

        yield return new WaitForSeconds(respawnDelay);

        playerController.Teleport(CheckpointManager.LastCheckpointPosition);

        playerSpriteToHide.enabled = true;
        playerCanvasToHide.enabled = true;

        cameraController.OnPlayerRespawn();

        deathBorderEffect.HideDeathBorder();

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