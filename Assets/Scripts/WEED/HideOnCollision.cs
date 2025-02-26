using Photon.Pun;
using UnityEngine;
using System.Collections;

public class HideOnCollision : MonoBehaviour
{
    public float timeHidden = 10f;

    private Renderer _renderer;

    private void Start()
    {
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D player)
    {
        if (player.gameObject.GetComponent<PhotonView>().IsMine == false) return;
        var playerC = player.gameObject.GetComponent<PlayerController>();

        _renderer.enabled = false;
        playerC.HasCatnip = true;

        StartCoroutine(ShowObjectAfterDelay(timeHidden, playerC));
    }

    private IEnumerator ShowObjectAfterDelay(float delay, PlayerController playerC)
    {
        yield return new WaitForSeconds(delay);
        _renderer.enabled = true;
        playerC.HasCatnip = false;
    }
}