using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CheckpointManager))]
public class PlayerDeathHandler : MonoBehaviour
{
    [SerializeField] private string hazardTag = "Death";
    [SerializeField] private float respawnDelay = 1f;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private DeathBorderEffect deathBorderEffect;
    [SerializeField] private GameObject deathExplosionPrefab;
    [SerializeField] private SpriteRenderer playerSpriteToHide;
    [SerializeField] private Canvas playerCanvasToHide;
    [SerializeField] private float explosionDuration = 10f;
    
    private SpectatorModeManager _spectatorModeManager;
    private DynamicCameraController _cameraController;

    private bool _isRespawning;
    private Camera _mainCamera;

    private void Start()
    {
        _cameraController = FindFirstObjectByType<DynamicCameraController>();
        _spectatorModeManager = FindFirstObjectByType<SpectatorModeManager>();
        _mainCamera = Camera.main;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isRespawning || !other.CompareTag(hazardTag))
            return;

        StartCoroutine(RespawnPlayer());
    }

    private IEnumerator RespawnPlayer()
    {
        _isRespawning = true;
        SetPlayerMovementEnabled(false);

        playerSpriteToHide.enabled = false;
        playerCanvasToHide.enabled = false;
        var explosion = Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);
        Destroy(explosion, explosionDuration);

        // Only apply camera effects if this is the local player or being spectated
        if (ShouldApplyCameraEffects())
        {
            _cameraController.OnPlayerDeath();
            deathBorderEffect.ShowDeathBorder();
        }

        playerController.OnPlayerDeath();

        yield return new WaitForSeconds(respawnDelay);

        playerController.RespawnAtLastCheckpoint();

        playerSpriteToHide.enabled = true;
        playerCanvasToHide.enabled = true;

        // Only apply camera effects if this is the local player or being spectated
        if (ShouldApplyCameraEffects())
        {
            _cameraController.OnPlayerRespawn();
            deathBorderEffect.HideDeathBorder();
        }

        SetPlayerMovementEnabled(true);
        _isRespawning = false;
    }

    private bool ShouldApplyCameraEffects()
    {
        if (!_mainCamera) return false;
        return playerController.photonView.IsMine || _spectatorModeManager.IsSpectating;
    }

    private void SetPlayerMovementEnabled(bool movementEnabled)
    {
        playerController.SetMovement(movementEnabled);
    }

    public void HandleOutOfBoundsDeath()
    {
        if (_isRespawning)
            return;

        StartCoroutine(RespawnPlayer());
    }
}