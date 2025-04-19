using System.Collections;
using System.Linq;
using UnityEngine;
using static UnityEngine.Mathf;

public class Portal : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private Vector3 destinationPosition;
    [SerializeField] private float teleportSpeed = 5f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float postTeleportDelay = 0.5f;

    [Header("Visual Effects")]
    [SerializeField] private bool useParticleEffects = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;

    private void OnTriggerEnter2D(Collider2D other)
    {
        var playerController = other.GetComponent<PlayerController>();
        if (playerController != null && playerController.photonView.IsMine)
            StartCoroutine(TeleportSequence(playerController));
    }

    private IEnumerator TeleportSequence(PlayerController player)
    {
        player.SetMovement(false);
        player.DisableRigidbody();

        var startPosition = player.transform.position;

        var distance = Vector3.Distance(startPosition, destinationPosition);
        var teleportDuration = Max(0.1f, distance / teleportSpeed);

        SetPlayerVisibility(player, false);

        if (useParticleEffects)
        {
            var vfxObj = new GameObject("TeleportVFX");
            var vfx = vfxObj.AddComponent<TeleportParticles>();
            vfx.AnimateTeleport(startPosition, destinationPosition, teleportDuration, movementCurve);
        }

        var elapsedTime = 0f;
        while (elapsedTime < teleportDuration)
        {
            var normalizedTime = elapsedTime / teleportDuration;
            var curvedProgress = movementCurve.Evaluate(normalizedTime);

            var currentPosition = Vector3.Lerp(startPosition, destinationPosition, curvedProgress);
            player.transform.position = currentPosition;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        yield return new WaitForSeconds(postTeleportDelay);

        player.Teleport(destinationPosition);
        player.SetMovement(true);
        player.EnableRigidbody();
        SetPlayerVisibility(player, true);
    }

    private static void SetPlayerVisibility(Component player, bool isVisible)
    {
        var renderers = player.GetComponentsInChildren<Renderer>();
        foreach (var renderer1 in renderers.Where(static renderer1 => renderer1.GetComponentInParent<MeowController>() == null))
            renderer1.enabled = isVisible;

        var canvases = player.GetComponentsInChildren<Canvas>();
        foreach (var canvas in canvases)
            canvas.enabled = isVisible;
    }

    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destinationPosition);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(destinationPosition, 1f);
    }
}
