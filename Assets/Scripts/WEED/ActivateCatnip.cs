using System.Collections;
using System.Linq;
using Photon.Pun;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Manages catnip items that apply temporary effects to players upon collection.
/// </summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(Renderer))]
public class ActivateCatnip : MonoBehaviour
{
    [SerializeField] [Tooltip("Duration of catnip effect in seconds")] public float effectDuration = 5f;

    private Renderer _renderer;
    private Collider2D _collider;
    private bool _isEffectActive;
    private GameObject[] _childObjects;
    private Coroutine _updateChargeBarCoroutine;

    /// Initializes components and prepares collider
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

    /// Activates catnip effect when player collides with the item
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

        var catnipFx = other.gameObject.GetComponent<CatnipFx>();
        if (_updateChargeBarCoroutine != null)
            StopCoroutine(_updateChargeBarCoroutine);

        _updateChargeBarCoroutine = StartCoroutine(UpdateChargeBar(catnipFx, effectDuration));

        StartCoroutine(DeactivateEffectAfterDelay(effectDuration, playerC, playerPhotonView, catnipFx));
    }

    /// Updates the charge bar fill regularly during the catnip effect
    private IEnumerator UpdateChargeBar(CatnipFx catnipFx, float duration)
    {
        var startTime = Time.time;
        var endTime = startTime + duration;

        while (Time.time < endTime)
        {
            var remainingTime = endTime - Time.time;
            catnipFx.UpdateCatnipRemainingTime(duration, remainingTime);
            yield return null;
        }

        _updateChargeBarCoroutine = null;
    }

    /// Deactivates catnip effect after specified duration
    private IEnumerator DeactivateEffectAfterDelay(float delay, PlayerController playerC, PhotonView playerPhotonView, CatnipFx catnipFx)
    {
        yield return new WaitForSeconds(delay);

        if (_updateChargeBarCoroutine != null)
        {
            StopCoroutine(_updateChargeBarCoroutine);
            _updateChargeBarCoroutine = null;
        }

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
