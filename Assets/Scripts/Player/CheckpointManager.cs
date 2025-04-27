using Photon.Pun;
using UnityEngine;
using static UnityEngine.Vector3;

/// <summary>
/// Manages checkpoint registration and tracking for player respawning.
/// </summary>
/// <inheritdoc />
public class CheckpointManager : MonoBehaviourPun
{
    [SerializeField] [Tooltip("Tag used to identify checkpoint objects in the scene")] private string checkpointTag = "Checkpoint";
    internal static Vector3 LastCheckpointPosition { get; private set; }

    internal static bool IsRespawning { get; set; }

    /// Initializes checkpoint position
    private void Start()
    {
        LastCheckpointPosition = transform.position;
    }

    /// Registers new checkpoints when player enters their trigger area
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(checkpointTag)) return;

        if (!photonView.IsMine) return;

        if (IsRespawning)
        {
            IsRespawning = false;
            return;
        }

        var newCheckpointPosition = other.transform.position;
        var checkpointParticles = other.GetComponent<CheckpointParticles>();

        if (!(Distance(newCheckpointPosition, LastCheckpointPosition) > 0.01f)) return;

        var previousCheckpoint = LastCheckpointPosition;
        LastCheckpointPosition = newCheckpointPosition;
        Debug.Log($"Checkpoint set at {LastCheckpointPosition}");

        if (PhotonNetwork.IsConnected)
            photonView.RPC(nameof(RPC_TriggerCheckpointParticles), RpcTarget.All,
                newCheckpointPosition,
                previousCheckpoint);
        else
            checkpointParticles.TriggerFireworksLocally(previousCheckpoint);
    }

    /// RPC method that triggers checkpoint particles on all clients
    [PunRPC]
    private void RPC_TriggerCheckpointParticles(Vector3 checkpointPosition, Vector3 previousCheckpointPosition)
    {
        var allCheckpoints = GameObject.FindGameObjectsWithTag(checkpointTag);
        GameObject closestCheckpoint = null;
        var closestDistance = float.MaxValue;

        foreach (var checkpoint in allCheckpoints)
        {
            var distance = Distance(checkpoint.transform.position, checkpointPosition);
            if (!(distance < closestDistance)) continue;

            closestDistance = distance;
            closestCheckpoint = checkpoint;
        }

        if (closestCheckpoint != null && closestDistance < 0.1f)
        {
            var checkpointParticles = closestCheckpoint.GetComponent<CheckpointParticles>();
            if (checkpointParticles != null)
                checkpointParticles.TriggerFireworksLocally(previousCheckpointPosition);
        }
        else
        {
            Debug.LogWarning($"Could not find checkpoint at position {checkpointPosition} to trigger particles");
        }
    }
}
