using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using UnityEngine;
using static UnityEngine.Mathf;

/// <inheritdoc />
/// <summary>
/// Teleports the player to a target position with optional effects and canvas exclusions.
/// </summary>
public class Portal : MonoBehaviour
{
    // Tracking of active teleportations
    private readonly List<(PlayerController player, Coroutine routine, GameObject vfx)> _activeTeleportations = new();

    [Header("Teleport Settings")]
    [SerializeField] [Tooltip("Target position for teleportation")] private Vector3 destinationPosition;
    [SerializeField] [Tooltip("Speed of teleportation")] private float teleportSpeed = 5f;
    [SerializeField] [Tooltip("Animation curve for teleportation movement")] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] [Tooltip("Delay after teleportation in seconds")] private float postTeleportDelay = 0.5f;

    [Header("Visual Effects")]
    [SerializeField] [Tooltip("Enable or disable particle effects during teleportation")] private bool useParticleEffects = true;

    [Header("Canvas Settings")]
    [SerializeField] [Tooltip("Names or tags of canvases to exclude from visibility changes")] private string[] canvasExclusions = Array.Empty<string>();
    [SerializeField] [Tooltip("If true, use canvas names for exclusions; if false, use tags")] private bool useCanvasNames = true;

    [Header("Debug")]
    [SerializeField] [Tooltip("Show debug gizmos in the editor")] private bool showDebugGizmos = true;

    /// Detects player collision and starts teleportation
    private void OnTriggerEnter2D(Collider2D other)
    {
        var photonView = other.GetComponent<PhotonView>();
        if (photonView == null) return;

        var playerController = other.GetComponent<PlayerController>();
        if (playerController == null) return;

        // Cancel any active teleportation for this player on this portal
        CancelPlayerTeleportation(playerController);

        var routine = StartCoroutine(playerController.photonView.IsMine
            ? TeleportSequence(playerController)
            : RemotePlayerTeleportSequence(playerController));

        // Don't track teleportation if it's a remote player
        if (!playerController.photonView.IsMine) return;

        // Add to active teleportations
        _activeTeleportations.Add((playerController, routine, null));
    }

    /// Handles the teleportation sequence for the local player
    private IEnumerator TeleportSequence(PlayerController player)
    {
        player.SetMovement(false);

        var playerRb = player.GetComponent<Rigidbody2D>();
        playerRb.linearVelocity = Vector2.zero;

        player.DisableRigidbody();

        var startPosition = player.transform.position;
        var distance = Vector3.Distance(startPosition, destinationPosition);
        var teleportDuration = Max(0.1f, distance / teleportSpeed);

        SetPlayerVisibility(player, false);

        GameObject vfxObj = null;
        if (useParticleEffects)
        {
            vfxObj = new GameObject("TeleportVFX");
            var vfx = vfxObj.AddComponent<TeleportParticles>();
            vfx.AnimateTeleport(startPosition, destinationPosition, teleportDuration, movementCurve);

            // Update the VFX reference in the active teleportations list
            for (int i = 0; i < _activeTeleportations.Count; i++)
            {
                var (tPlayer, tRoutine, _) = _activeTeleportations[i];
                if (tPlayer == player)
                {
                    _activeTeleportations[i] = (tPlayer, tRoutine, vfxObj);
                    break;
                }
            }
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

        var cameraController = player.GetComponentInChildren<DynamicCameraController>();

        player.TeleportWithoutCameraReset(destinationPosition);

        if (cameraController != null)
            StartCoroutine(cameraController.HandleTeleportTransition(destinationPosition));

        player.SetMovement(true);
        player.EnableRigidbody();
        SetPlayerVisibility(player, true);

        // Remove from active teleportations
        RemovePlayerFromActiveTeleportations(player);
    }

    /// Handles the teleportation sequence for remote players
    private IEnumerator RemotePlayerTeleportSequence(Component player)
    {
        var startPosition = player.transform.position;
        var distance = Vector3.Distance(startPosition, destinationPosition);
        var teleportDuration = Max(0.1f, distance / teleportSpeed);

        SetRemotePlayerVisibility(player, false);

        GameObject vfxObj = null;
        if (useParticleEffects)
        {
            vfxObj = new GameObject("RemoteTeleportVFX");
            var vfx = vfxObj.AddComponent<TeleportParticles>();
            vfx.AnimateTeleport(startPosition, destinationPosition, teleportDuration, movementCurve);
        }

        yield return new WaitForSeconds(teleportDuration + postTeleportDelay);

        SetRemotePlayerVisibility(player, true);

        // Clean up VFX if not already destroyed
        if (vfxObj != null && vfxObj.activeSelf)
            Destroy(vfxObj);
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

    /// Toggles remote player visibility while keeping particles visible
    private void SetRemotePlayerVisibility(Component player, bool isVisible)
    {
        var renderers = player.GetComponentsInChildren<Renderer>();
        foreach (var renderer1 in renderers.Where(static renderer => !renderer.GetComponentInParent<ParticleSystem>()).Where(static renderer => !renderer.GetComponentInParent<MeowController>()))
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

    /// Cancels teleportation for a specific player
    private void CancelPlayerTeleportation(PlayerController player)
    {
        for (int i = _activeTeleportations.Count - 1; i >= 0; i--)
        {
            var (tPlayer, tRoutine, tVfx) = _activeTeleportations[i];

            if (tPlayer != player) continue;

            // Stop the coroutine
            if (tRoutine != null)
                StopCoroutine(tRoutine);

            // Clean up VFX
            if (tVfx != null)
                Destroy(tVfx);

            // Restore player state
            tPlayer.SetMovement(true);
            tPlayer.EnableRigidbody();
            SetPlayerVisibility(tPlayer, true);

            // Remove from list
            _activeTeleportations.RemoveAt(i);
        }
    }

    /// Removes a player from active teleportations list
    private void RemovePlayerFromActiveTeleportations(PlayerController player)
    {
        for (int i = _activeTeleportations.Count - 1; i >= 0; i--)
        {
            if (_activeTeleportations[i].player == player)
                _activeTeleportations.RemoveAt(i);
        }
    }

    /// Cancels all active teleportations for this portal
    public void CancelAllActivePortalTeleportations()
    {
        for (int i = _activeTeleportations.Count - 1; i >= 0; i--)
        {
            var (tPlayer, tRoutine, tVfx) = _activeTeleportations[i];

            // Stop the coroutine
            if (tRoutine != null)
                StopCoroutine(tRoutine);

            // Clean up VFX
            if (tVfx != null)
                Destroy(tVfx);

            // Restore player state
            tPlayer.SetMovement(true);
            tPlayer.EnableRigidbody();
            SetPlayerVisibility(tPlayer, true);
        }

        // Clear the list
        _activeTeleportations.Clear();
    }
}
