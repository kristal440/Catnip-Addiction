using System.Collections;
using Photon.Pun;
using UnityEngine;

public class HideOnCollision : MonoBehaviour
{
    public float timeHidden = 10f;

    private SpectatorModeManager _spectatorModeManager;
    private Renderer _renderer;

    private void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _spectatorModeManager = FindFirstObjectByType<SpectatorModeManager>();
    }

    private void OnTriggerEnter2D(Collider2D player)
    {
        var playerC = player.gameObject.GetComponent<PlayerController>();

        if (player.gameObject.GetComponent<PhotonView>().IsMine)
        {
            _renderer.enabled = false;
            playerC.HasCatnip = true;

            StartCoroutine(ShowObjectAfterDelay(timeHidden, playerC));
        }

        if (!_spectatorModeManager.IsSpectating) return;

        var material = _renderer.material;
        var color = material.color;
        color.a = 0.5f;
        material.color = color;
        StartCoroutine(ResetOpacity(timeHidden, playerC));
    }

    private IEnumerator ShowObjectAfterDelay(float delay, PlayerController playerC)
    {
        yield return new WaitForSeconds(delay);

        _renderer.enabled = true;
        playerC.HasCatnip = false;
    }

    private IEnumerator ResetOpacity(float delay, PlayerController playerC)
    {
        yield return new WaitForSeconds(delay);

        var material = _renderer.material;
        var color = material.color;
        color.a = 1f;
        material.color = color;
        playerC.HasCatnip = false;
    }
}
