using UnityEngine;
using static UnityEngine.Vector2;
using Photon.Pun;

public class CheckpointManager : MonoBehaviour
{
    [SerializeField] private string checkpointTag = "Checkpoint"; // tag checkpoint objects, add 2d collider
    internal static Vector2 LastCheckpointPosition { get; private set; }
    private PhotonView _photonView;

    private void Start()
    {
        LastCheckpointPosition = transform.position;
        _photonView = GetComponent<PhotonView>();
    }

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
