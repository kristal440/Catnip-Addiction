using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Manages the flipping of multiple game objects for consistent facing direction with network synchronization
/// </summary>
public class SpriteFlipManager : MonoBehaviourPunCallbacks
{
    [SerializeField] [Tooltip("List of sprite renderers to flip using flipX")] private List<SpriteRenderer> spriteRenderers = new();
    [SerializeField] [Tooltip("List of transforms to flip using negative scale")] private List<Transform> transformsToFlip = new();
    [SerializeField] [Tooltip("List of transforms to flip by multiplying X position by -1")] private List<Transform> positionFlipTransforms = new();
    [SerializeField] [Tooltip("Initial facing direction (true = right, false = left)")] private bool isFacingRight = true;

    private readonly Dictionary<Transform, Vector3> _originalScales = new();
    private readonly Dictionary<Transform, Vector3> _originalPositions = new();
    private PhotonView _photonView;

    private void Awake()
    {
        CacheOriginalValues();
        _photonView = GetComponent<PhotonView>();
    }

    private void Start()
    {
        // Ensure initial state is correct for all players
        if (_photonView.IsMine)
            _photonView.RPC(nameof(RPC_SyncFlipDirection), RpcTarget.OthersBuffered, isFacingRight);
    }

    /// Stores original scale and position values for all transforms
    private void CacheOriginalValues()
    {
        foreach (var key in transformsToFlip.Where(static transform => transform != null))
            _originalScales[key] = key.localScale;

        foreach (var key in positionFlipTransforms.Where(static transform => transform != null))
            _originalPositions[key] = key.localPosition;
    }

    #region Public Methods
    /// Returns the current facing direction
    internal bool IsFacingRight()
    {
        return isFacingRight;
    }

    /// Flips all objects to face the specified direction
    public void FlipToDirection(bool facingRight)
    {
        if (isFacingRight == facingRight) return;

        FlipAll();
    }

    /// Flips all objects to the opposite direction with network synchronization
    internal void FlipAll()
    {
        isFacingRight = !isFacingRight;

        // Perform local flip operations
        ApplyFlipVisuals();

        // Send RPC to sync with other players if we own this object
        if (_photonView && _photonView.IsMine)
            _photonView.RPC(nameof(RPC_SyncFlipDirection), RpcTarget.Others, isFacingRight);
    }
    #endregion

    #region Network Sync
    /// Applies the visual flip effects based on current facing direction
    private void ApplyFlipVisuals()
    {
        // Flip all sprite renderers
        foreach (var spriteRenderer in spriteRenderers.Where(static spriteRenderer => spriteRenderer))
            spriteRenderer.flipX = !isFacingRight;

        // Flip all transforms by scale
        foreach (var key in transformsToFlip)
        {
            if (!key || !_originalScales.ContainsKey(key)) continue;

            var scale = key.localScale;
            scale.x = _originalScales[key].x * (isFacingRight ? 1 : -1);
            key.localScale = scale;
        }

        // Flip transforms by position
        foreach (var key in positionFlipTransforms)
        {
            if (!key || !_originalPositions.ContainsKey(key)) continue;

            var position = key.localPosition;
            position.x = _originalPositions[key].x * (isFacingRight ? 1 : -1);
            key.localPosition = position;
        }
    }

    /// RPC method to synchronize flip direction across the network
    [PunRPC]
    private void RPC_SyncFlipDirection(bool facingRight)
    {
        // Only apply if this is not our player or if we're in spectator mode
        if (_photonView.IsMine) return;

        isFacingRight = facingRight;
        ApplyFlipVisuals();
    }

    /// <inheritdoc />
    /// Called when player properties are updated, useful for late-joining players
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        // If this is our player object and properties have been updated, sync our state
        if (targetPlayer.IsLocal && _photonView.Owner.Equals(targetPlayer))
            _photonView.RPC(nameof(RPC_SyncFlipDirection), RpcTarget.OthersBuffered, isFacingRight);
    }
    #endregion
}
