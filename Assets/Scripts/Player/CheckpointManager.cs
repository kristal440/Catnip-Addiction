using UnityEngine;
using static UnityEngine.Vector2;
using Photon.Pun;

/// <summary>
/// Manages checkpoint registration and tracking for player respawning.
/// </summary>
/// <inheritdoc />
public class CheckpointManager : MonoBehaviour
{
    [SerializeField] [Tooltip("Tag used to identify checkpoint objects in the scene")] private string checkpointTag = "Checkpoint";
    internal static Vector2 LastCheckpointPosition { get; private set; }
    private PhotonView _photonView;

    // Initializes checkpoint position and gets PhotonView reference
    private void Start()
    {
        LastCheckpointPosition = transform.position;
        _photonView = GetComponent<PhotonView>();
    }

    // Registers new checkpoints when player enters their trigger area
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(checkpointTag)) return;
        if (!_photonView.IsMine) return;

        var newCheckpointPosition = other.transform.position;

        if (!(Distance(newCheckpointPosition, LastCheckpointPosition) > 0.01f)) return;

        LastCheckpointPosition = newCheckpointPosition;
        Debug.Log($"Checkpoint set at {LastCheckpointPosition}");
    }
}
