using System.Collections;
using Photon.Pun;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Handles player death events, respawning, and visual effects when player dies
/// </summary>
[RequireComponent(typeof(CheckpointManager))]
public class PlayerDeathHandler : MonoBehaviourPun
{
    [SerializeField] [Tooltip("Tag that identifies hazardous objects causing death")] private string hazardTag = "Death";
    [SerializeField] [Tooltip("Delay in seconds before respawning after death")] private float respawnDelay = 1f;
    [SerializeField] [Tooltip("Reference to the player controller component")] private PlayerController playerController;
    [SerializeField] [Tooltip("Death border effect for visual feedback")] private DeathBorderEffect deathBorderEffect;
    [SerializeField] [Tooltip("Prefab to spawn when player dies")] private GameObject deathExplosionPrefab;
    [SerializeField] [Tooltip("Player sprite to hide during death sequence")] private SpriteRenderer playerSpriteToHide;
    [SerializeField] [Tooltip("Player UI canvas to hide during death sequence")] private Canvas playerCanvasToHide;
    [SerializeField] [Tooltip("How long death explosion effect remains before being destroyed")] private float explosionDuration = 10f;

    [Header("Death Notifications")]
    [SerializeField] [Tooltip("Enable death notifications")] private bool enableDeathNotifications = true;

    private SpectatorModeManager _spectatorModeManager;
    private DynamicCameraController _cameraController;

    private bool _isRespawning;
    private Camera _mainCamera;

    /// Find required components on start
    private void Start()
    {
        _cameraController = FindFirstObjectByType<DynamicCameraController>();
        _spectatorModeManager = FindFirstObjectByType<SpectatorModeManager>();
        _mainCamera = Camera.main;
    }

    /// Detect collision with deadly objects
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isRespawning || !other.CompareTag(hazardTag))
            return;

        if (enableDeathNotifications && photonView.IsMine)
            photonView.RPC(nameof(RPC_NotifyPlayerDeath), RpcTarget.All);

        StartCoroutine(RespawnPlayer());
    }

    /// Handle the death and respawn sequence
    private IEnumerator RespawnPlayer()
    {
        _isRespawning = true;
        playerController.SetMovement(false);

        playerSpriteToHide.enabled = false;
        playerCanvasToHide.enabled = false;
        var explosion = Instantiate(deathExplosionPrefab, transform.position, Quaternion.identity);
        Destroy(explosion, explosionDuration);

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

        if (ShouldApplyCameraEffects())
        {
            _cameraController.OnPlayerRespawn();
            deathBorderEffect.HideDeathBorder();
        }

        playerController.SetMovement(true);
        _isRespawning = false;
    }

    /// Determine whether camera effects should be applied based on ownership or spectating
    private bool ShouldApplyCameraEffects()
    {
        if (!_mainCamera) return false;

        return playerController.photonView.IsMine || _spectatorModeManager.IsSpectating;
    }

    /// Handle player falling out of world boundaries
    internal void HandleOutOfBoundsDeath()
    {
        if (_isRespawning)
            return;

        if (enableDeathNotifications && photonView.IsMine)
            photonView.RPC(nameof(RPC_NotifyPlayerDeath), RpcTarget.All);

        StartCoroutine(RespawnPlayer());
    }

    /// <summary>
    /// RPC method to notify all clients when a player dies
    /// </summary>
    [PunRPC]
    private void RPC_NotifyPlayerDeath()
    {
        var playerName = photonView.Owner.NickName;
        FindFirstObjectByType<PlayerNotificationManager>().ShowDeathNotification(playerName);
    }
}
