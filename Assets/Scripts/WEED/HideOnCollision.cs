using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Renderer))]
public class HideOnCollision : MonoBehaviour
{
    [Tooltip("How long the catnip effect lasts in seconds.")]
    public float effectDuration = 5f;

    private Renderer _renderer;
    private Collider2D _collider;
    private bool _isEffectActive;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider2D>();

        if (_collider.isTrigger) return;

        _collider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isEffectActive) return;

        var playerC = other.gameObject.GetComponent<PlayerController>();
        if (playerC == null) return;

        var playerPhotonView = other.gameObject.GetComponent<PhotonView>();
        if (playerPhotonView == null) return;

        // --- Only the player who collides locally processes the pickup ---
        if (!playerPhotonView.IsMine) return;

        _isEffectActive = true;

        // Disable pickup visuals/collision locally
        _renderer.enabled = false;
        _collider.enabled = false;

        // --- Set the flag ONLY on the local PlayerController ---
        playerC.HasCatnip = true;

        // --- RPC calls removed ---
        // playerPhotonView.RPC("RPC_SetCatnipEffectActive", RpcTarget.AllBuffered, true); // REMOVED

        // Start timer to deactivate the effect locally
        StartCoroutine(DeactivateEffectAfterDelay(effectDuration, playerC, playerPhotonView));
    }

    private IEnumerator DeactivateEffectAfterDelay(float delay, PlayerController playerC, PhotonView playerPhotonView)
    {
        yield return new WaitForSeconds(delay);

        // Check if player components are still valid
        if (playerC && playerPhotonView)
        {
            // --- Reset the flag ONLY on the local PlayerController ---
            if (playerPhotonView.IsMine)
                playerC.HasCatnip = false;

            // --- RPC call removed ---
            // playerPhotonView.RPC("RPC_SetCatnipEffectActive", RpcTarget.All, false); // REMOVED
        }

        // Re-enable the pickup object locally if it still exists
        if (!this || !gameObject) yield break;

        _renderer.enabled = true;
        _collider.enabled = true;
        _isEffectActive = false;
    }
}
