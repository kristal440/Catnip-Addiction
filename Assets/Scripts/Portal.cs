using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using static UnityEngine.Mathf;

/// <inheritdoc />
/// <summary>
/// Teleports the player to a target position with optional effects and canvas exclusions.
/// </summary>
public class Portal : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] [Tooltip("Target position for teleportation")] private Vector3 destinationPosition;
    [SerializeField] [Tooltip("Speed of teleportation")] private float teleportSpeed = 5f;
    [SerializeField] [Tooltip("Animation curve for teleportation movement")] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] [Tooltip("Delay after teleportation in seconds")] private float postTeleportDelay = 0.5f;

    [Header("Velocity Settings")]
    [SerializeField] [Tooltip("Keep player's velocity after teleportation")] private bool preserveVelocity;
    [SerializeField] [Tooltip("Velocity multiplier after teleportation")] private float velocityMultiplier = 1.0f;
    [SerializeField] [Tooltip("Control how velocity direction is handled")] private VelocityDirectionMode velocityDirectionMode = VelocityDirectionMode.Preserve;
    [SerializeField] [Tooltip("Custom direction to set velocity (if using Custom mode)")] private Vector2 customVelocityDirection = Vector2.right;

    [Header("Visual Effects")]
    [SerializeField] [Tooltip("Enable or disable particle effects during teleportation")] private bool useParticleEffects = true;

    [Header("Canvas Settings")]
    [SerializeField] [Tooltip("Names or tags of canvases to exclude from visibility changes")] private string[] canvasExclusions = Array.Empty<string>();
    [SerializeField] [Tooltip("If true, use canvas names for exclusions; if false, use tags")] private bool useCanvasNames = true;

    [Header("Debug")]
    [SerializeField] [Tooltip("Show debug gizmos in the editor")] private bool showDebugGizmos = true;

    /// Enum to define velocity direction behavior
    private enum VelocityDirectionMode
    {
        Preserve,
        Reverse,
        Mirror,
        Custom
    }

    /// Detects player collision and starts teleportation
    private void OnTriggerEnter2D(Collider2D other)
    {
        var playerController = other.GetComponent<PlayerController>();
        if (playerController != null && playerController.photonView.IsMine)
            StartCoroutine(TeleportSequence(playerController));
    }

    /// Handles the teleportation sequence
    private IEnumerator TeleportSequence(PlayerController player)
    {
        player.SetMovement(false);

        var originalVelocity = Vector2.zero;
        var playerRb = player.GetComponent<Rigidbody2D>();
        if (playerRb)
            originalVelocity = playerRb.linearVelocity;

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

        if (!preserveVelocity || !playerRb) yield break;

        var newVelocity = originalVelocity;

        switch (velocityDirectionMode)
        {
            case VelocityDirectionMode.Preserve:
                break;
            case VelocityDirectionMode.Reverse:
                newVelocity = -newVelocity;
                break;
            case VelocityDirectionMode.Mirror:
                newVelocity = new Vector2(-newVelocity.x, newVelocity.y);
                break;
            case VelocityDirectionMode.Custom:
                if (customVelocityDirection.sqrMagnitude > 0)
                    newVelocity = customVelocityDirection.normalized * newVelocity.magnitude;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        newVelocity *= velocityMultiplier;

        playerRb.linearVelocity = newVelocity;
    }

    /// Toggles player visibility and manages canvas exclusions
    private void SetPlayerVisibility(Component player, bool isVisible)
    {
        var renderers = player.GetComponentsInChildren<Renderer>();
        foreach (var renderer1 in renderers.Where(static renderer1 => !renderer1.GetComponentInParent<MeowController>()))
            renderer1.enabled = isVisible;

        var canvases = player.GetComponentsInChildren<Canvas>();
        foreach (var canvas in canvases)
        {
            var shouldExclude = false;
            if (canvasExclusions.Length > 0)
                shouldExclude = useCanvasNames ? Array.Exists(canvasExclusions, exclusion => canvas.name == exclusion) : Array.Exists(canvasExclusions, exclusion => canvas.CompareTag(exclusion));

            if (!shouldExclude)
                canvas.enabled = isVisible;
        }
    }

    /// Draws debug gizmos in the editor
    private void OnDrawGizmos()
    {
        if (!showDebugGizmos) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, destinationPosition);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(destinationPosition, 1f);
    }
}
