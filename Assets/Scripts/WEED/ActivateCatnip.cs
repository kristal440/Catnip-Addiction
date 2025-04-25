using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Renderer))]
public class ActivateCatnip : MonoBehaviour
{
    public float effectDuration = 5f;

    private Renderer _renderer;
    private Collider2D _collider;
    private bool _isEffectActive;
    private GameObject[] _childObjects;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider2D>();

        var childCount = transform.childCount;
        _childObjects = new GameObject[childCount];
        for (var i = 0; i < childCount; i++)
            _childObjects[i] = transform.GetChild(i).gameObject;

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

        foreach (var child in _childObjects.Where(static child => child != null))
            child.SetActive(false);

        playerC.HasCatnip = true;

        playerPhotonView.RPC(nameof(playerC.RPC_SetCatnipEffectActive), RpcTarget.All, true);

        StartCoroutine(DeactivateEffectAfterDelay(effectDuration, playerC, playerPhotonView));
    }

    private IEnumerator DeactivateEffectAfterDelay(float delay, PlayerController playerC, PhotonView playerPhotonView)
    {
        yield return new WaitForSeconds(delay);

        if (playerC && playerPhotonView)
        {
            if (playerPhotonView.IsMine)
                playerC.HasCatnip = false;

            playerPhotonView.RPC(nameof(playerC.RPC_SetCatnipEffectActive), RpcTarget.All, false);
        }

        if (!this || !gameObject) yield break;

        _renderer.enabled = true;
        _collider.enabled = true;

        foreach (var child in _childObjects.Where(static child => child))
            child.SetActive(true);

        _isEffectActive = false;
    }
}
