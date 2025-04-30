using Photon.Pun;
using UnityEngine;
using UnityEngine.UI;

/// <inheritdoc />
/// <summary>
/// Manages jump charge UI for both local and spectated players, handling synchronization and visibility with locally calculated values
/// </summary>
public class JumpChargeUIManager : MonoBehaviourPunCallbacks
{
    [Header("UI References")]
    [SerializeField] [Tooltip("Container for jump charge bar")] private GameObject jumpChargeBarGameObject;
    [SerializeField] [Tooltip("Jump charge fill bar")] private Image jumpChargeBar;

    [Header("Buffered Jump Visualization")]
    [SerializeField] [Tooltip("Should charge bar remain visible for buffered jumps")] private bool showStoredChargeBar = true;
    [SerializeField] [Range(0.1f, 1.0f)] [Tooltip("Alpha transparency for stored charge visualization")] private float storedChargeAlpha = 0.7f;
    [SerializeField] [Tooltip("Color for stored charge bar")] private Color storedChargeColor = new(0.7f, 0.7f, 1f);

    [Header("Remote Player Settings")]
    [SerializeField] [Tooltip("Show jump charge for all other players regardless of spectating")] private bool alwaysShowOtherPlayersCharge;
    [SerializeField] [Tooltip("Scale of the jump charge bar for remote players (X axis)")] private float remotePlayerChargeBarScaleX = 1f;
    [SerializeField] [Tooltip("Scale of the jump charge bar for remote players (Y axis)")] private float remotePlayerChargeBarScaleY = 1f;

    private PlayerController _playerController;
    private DynamicCameraController _cameraController;
    private bool _isCharging;
    private float _chargeProgress;
    private Vector3 _originalScale;
    private Color _originalChargeColor;
    private bool _showingStoredCharge;

    // Values for local calculation for remote players
    private bool _remotePlayerIsCharging;
    private float _remoteJumpStartTime;
    private float _remoteMaxChargeTime;

    /// Gets references to required components
    private void Start()
    {
        _playerController = GetComponent<PlayerController>();

        if (photonView.IsMine)
        {
            var mainCamera = Camera.main;
            if (mainCamera != null)
                _cameraController = mainCamera.GetComponent<DynamicCameraController>();
        }
        else
        {
            _originalScale = jumpChargeBarGameObject.transform.localScale;
            jumpChargeBarGameObject.transform.localScale = new Vector3(
                _originalScale.x * remotePlayerChargeBarScaleX,
                _originalScale.y * remotePlayerChargeBarScaleY,
                _originalScale.z
            );
        }

        _originalChargeColor = jumpChargeBar.color;
        HideChargeBar();
    }

    /// Handles updating charge visualization
    private void Update()
    {
        if (photonView.IsMine)
            // Local player simply needs to show/hide based on state
            UpdateLocalChargeBar();
        else if (_remotePlayerIsCharging && (alwaysShowOtherPlayersCharge || IsBeingSpectated()))
            // Remote player needs to calculate charge progression locally
            UpdateRemoteChargeBar();
        else if (jumpChargeBarGameObject.activeSelf && !_showingStoredCharge)
            HideChargeBar();
    }

    /// Updates charge bar for local player
    private void UpdateLocalChargeBar()
    {
        var isCharging = _playerController.JumpState != PlayerController.JumpStateEnum.Idle;
        var hasStoredCharge = _playerController.HasBufferedChargeInAir && showStoredChargeBar;

        if (isCharging)
        {
            if (!jumpChargeBarGameObject.activeSelf)
                jumpChargeBarGameObject.SetActive(true);

            if (_showingStoredCharge)
            {
                _showingStoredCharge = false;
                jumpChargeBar.color = _originalChargeColor;
            }

            jumpChargeBar.fillAmount = _chargeProgress;

            if (_cameraController)
                _cameraController.UpdateChargingJumpFOV(_chargeProgress);
        }
        else if (hasStoredCharge)
        {
            if (!jumpChargeBarGameObject.activeSelf)
                jumpChargeBarGameObject.SetActive(true);

            if (!_showingStoredCharge)
            {
                _showingStoredCharge = true;
                jumpChargeBar.color = storedChargeColor;

                var transparent = jumpChargeBar.color;
                transparent.a = storedChargeAlpha;
                jumpChargeBar.color = transparent;
            }

            jumpChargeBar.fillAmount = _playerController.StoredChargeProgress / _playerController.maxChargeTime;
        }
        else if (jumpChargeBarGameObject.activeSelf)
        {
            HideChargeBar();
        }
    }

    /// Updates charge bar for remote player
    private void UpdateRemoteChargeBar()
    {
        var elapsedTime = Time.time - _remoteJumpStartTime;
        _chargeProgress = Mathf.Clamp01(elapsedTime / _remoteMaxChargeTime);

        jumpChargeBar.fillAmount = _chargeProgress;

        if (!jumpChargeBarGameObject.activeSelf)
            jumpChargeBarGameObject.SetActive(true);
    }

    /// Sets the charging state and progress (for local player)
    internal void SetChargingState(bool isCharging, float progress, bool fullyCharged)
    {
        if (!photonView.IsMine) return;

        _isCharging = isCharging;
        _chargeProgress = progress;
    }

    /// Hides the charge bar
    private void HideChargeBar()
    {
        jumpChargeBarGameObject.SetActive(false);
        _showingStoredCharge = false;
        jumpChargeBar.color = _originalChargeColor;

        if (!photonView.IsMine)
        {
            _remotePlayerIsCharging = false;
            _chargeProgress = 0f;
        }
        else if (_isCharging)
        {
            _isCharging = false;
            _chargeProgress = 0f;
        }
    }

    /// Forces UI to match jump state - can be called from PlayerController
    internal void ForceUIStateSync()
    {
        if (!photonView.IsMine) return;

        UpdateLocalChargeBar();
    }

    /// Checks if this player is being spectated
    private bool IsBeingSpectated()
    {
        var spectatorManager = SpectatorModeManager.GetInstance();
        return spectatorManager && SpectatorModeManager.IsPlayerBeingSpectated(photonView.ViewID);
    }

    /// RPC to start jump charging
    [PunRPC]
    internal void RPC_StartJumpCharge(int jumpStateInt, float maxChargeTimeForState)
    {
        if (photonView.IsMine) return;

        _remotePlayerIsCharging = true;
        _remoteJumpStartTime = Time.time;
        _remoteMaxChargeTime = maxChargeTimeForState;
        _chargeProgress = 0f;

        if ((alwaysShowOtherPlayersCharge || IsBeingSpectated()) && !jumpChargeBarGameObject.activeSelf)
            jumpChargeBarGameObject.SetActive(true);
    }

    /// RPC to end jump charging
    [PunRPC]
    internal void RPC_EndJumpCharge()
    {
        if (photonView.IsMine) return;

        _remotePlayerIsCharging = false;

        if (jumpChargeBarGameObject.activeSelf)
            HideChargeBar();
    }

    /// Sets this player as being spectated or not
    internal void SetSpectatedState()
    {
        if (!photonView.IsMine && _remotePlayerIsCharging && (alwaysShowOtherPlayersCharge || IsBeingSpectated()))
            jumpChargeBarGameObject.SetActive(true);
        else if (!alwaysShowOtherPlayersCharge && !IsBeingSpectated() && !photonView.IsMine)
            HideChargeBar();
    }
}
