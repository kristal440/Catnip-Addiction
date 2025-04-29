using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

/// <inheritdoc />
/// <summary>
/// Manages jump charge UI for both local and spectated players, handling synchronization and visibility with smooth transitions
/// </summary>
public class JumpChargeUIManager : MonoBehaviourPunCallbacks
{
    [SerializeField] [Tooltip("Container for jump charge bar")] private GameObject jumpChargeBarGameObject;
    [SerializeField] [Tooltip("Jump charge fill bar")] private Image jumpChargeBar;
    [SerializeField] [Range(1f, 15f)] [Tooltip("How quickly the charge bar transitions for spectated players")] private float transitionSpeed = 8f;

    private PlayerController _playerController;
    private DynamicCameraController _cameraController;
    private bool _isCharging;
    private float _chargeProgress;
    private bool _fullyCharged;
    private float _lastSyncTime;

    // Target values for smooth transitions when spectating
    private float _targetChargeProgress;
    private bool _targetIsCharging;
    private bool _targetFullyCharged;

    private const float SyncInterval = 0.1f;

    /// Gets references to required components
    private void Start()
    {
        _playerController = GetComponent<PlayerController>();

        // Get camera controller if it's a local player
        if (photonView.IsMine)
        {
            var mainCamera = Camera.main;
            if (mainCamera != null)
                _cameraController = mainCamera.GetComponent<DynamicCameraController>();
        }

        // Hide charge bar initially
        HideChargeBar();
    }

    /// Handles updating charge visualization and syncing
    private void Update()
    {
        if (photonView.IsMine && _playerController.IsGrounded && _playerController.JumpState == PlayerController.JumpStateEnum.Idle)
            HideChargeBar();

        // Only show and update charge bar for local player or while being spectated
        if (!photonView.IsMine && !IsBeingSpectated())
        {
            HideChargeBar();
            return;
        }

        // For spectated players, smoothly transition to target values
        if (!photonView.IsMine && IsBeingSpectated())
        {
            // Transition charging state
            if (_targetIsCharging != _isCharging)
                _isCharging = _targetIsCharging;

            // Smoothly transition charge progress
            if (!Mathf.Approximately(_chargeProgress, _targetChargeProgress))
                _chargeProgress = Mathf.Lerp(_chargeProgress, _targetChargeProgress, transitionSpeed * Time.deltaTime);

            // Transition fully charged state
            if (_targetFullyCharged != _fullyCharged)
                _fullyCharged = _targetFullyCharged;
        }

        // Update the charge bar visibility and fill amount
        jumpChargeBarGameObject.SetActive(_isCharging);

        if (_isCharging)
        {
            jumpChargeBar.fillAmount = _chargeProgress;

            // Update camera FOV effect for local player
            if (photonView.IsMine && _cameraController)
                _cameraController.UpdateChargingJumpFOV(_chargeProgress);
        }

        // Sync charge data to other players if we own this player, and it's time to sync
        if (!photonView.IsMine || !(Time.time > _lastSyncTime + SyncInterval)) return;

        _lastSyncTime = Time.time;
        SyncChargeData();
    }

    /// Sets the charging state and progress (immediate for local player)
    internal void SetChargingState(bool isCharging, float progress, bool fullyCharged)
    {
        if (!photonView.IsMine) return;
        // Local player gets immediate feedback
        _isCharging = isCharging;
        _chargeProgress = progress;
        _fullyCharged = fullyCharged;

        // Also set target values
        _targetIsCharging = isCharging;
        _targetChargeProgress = progress;
        _targetFullyCharged = fullyCharged;

        // Immediately hide bar if no longer charging
        if (!isCharging)
            HideChargeBar();
    }

    /// Hides the charge bar
    private void HideChargeBar()
    {
        jumpChargeBarGameObject.SetActive(false);
    }

    /// Checks if this player is being spectated
    private bool IsBeingSpectated()
    {
        var spectatorManager = SpectatorModeManager.GetInstance();
        return spectatorManager && SpectatorModeManager.IsPlayerBeingSpectated(photonView.ViewID);
    }

    /// Syncs charge data to other players for spectating
    private void SyncChargeData()
    {
        photonView.RPC(nameof(RPC_SyncJumpCharge), RpcTarget.Others, _isCharging, _chargeProgress, _fullyCharged);
    }

    /// RPC to sync jump charge data for spectators
    [PunRPC]
    private void RPC_SyncJumpCharge(bool isCharging, float chargeProgress, bool fullyCharged)
    {
        // Only update for remote players that are being spectated
        if (photonView.IsMine) return;

        // Check if this player is being spectated
        var spectatorManager = SpectatorModeManager.GetInstance();
        if (spectatorManager == null || !spectatorManager.IsSpectating ||
            spectatorManager.GetCurrentlySpectatedPlayerViewId() != photonView.ViewID)
            return;

        // Update the target values for smooth transition
        _targetIsCharging = isCharging;
        _targetChargeProgress = chargeProgress;
        _targetFullyCharged = fullyCharged;

        // Set initial values if this is the first update
        if (!_isCharging && isCharging)
        {
            _isCharging = true;
            _chargeProgress = 0f; // Start from zero for a smooth fill
        }

        // Hide immediately if no longer charging
        if (isCharging || !_isCharging) return;

        _isCharging = false;
        HideChargeBar();
    }

    /// Called when this player starts being spectated
    internal void OnStartSpectating()
    {
        // Force an immediate sync of current charge state if we own this player
        if (!photonView.IsMine) return;

        SyncChargeData();
    }

    /// Sets this player as being spectated or not
    internal void SetSpectatedState(bool isBeingSpectated)
    {
        // If no longer being spectated and not the local player, hide UI
        if (!isBeingSpectated && !photonView.IsMine)
            HideChargeBar();
    }
}
