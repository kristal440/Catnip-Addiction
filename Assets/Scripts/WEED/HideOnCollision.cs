using System.Collections;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Renderer))]
public class HideOnCollision : MonoBehaviour
{
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

        if (!playerPhotonView.IsMine) return;

        _isEffectActive = true;

        _renderer.enabled = false;
        _collider.enabled = false;

        playerC.HasCatnip = true;

        playerPhotonView.RPC(nameof(playerC.RPC_SetCatnipEffectActive), RpcTarget.Others, true);

        StartCoroutine(DeactivateEffectAfterDelay(effectDuration, playerC, playerPhotonView));
    }

    private IEnumerator DeactivateEffectAfterDelay(float delay, PlayerController playerC, PhotonView playerPhotonView)
    {
        yield return new WaitForSeconds(delay);

        if (playerC && playerPhotonView)
        {
            if (playerPhotonView.IsMine)
                playerC.HasCatnip = false;

            playerPhotonView.RPC(nameof(playerC.RPC_SetCatnipEffectActive), RpcTarget.Others, false);
        }

        if (!this || !gameObject) yield break;

        _renderer.enabled = true;
        _collider.enabled = true;
        _isEffectActive = false;
    }
}
