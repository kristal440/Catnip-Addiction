using UnityEngine;
using static UnityEngine.Vector2;

public class CheckpointManager : MonoBehaviour
{
    [SerializeField] private string checkpointTag = "Checkpoint"; // tag checkpoint objects, add 2d collider
    public static Vector2 LastCheckpointPosition { get; private set; }

    private void Start()
    {
        LastCheckpointPosition = transform.position;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(checkpointTag)) return;

        var newCheckpointPosition = other.transform.position;

        if (!(Distance(newCheckpointPosition, LastCheckpointPosition) > 0.01f)) return;
        LastCheckpointPosition = newCheckpointPosition;
        Debug.Log($"Checkpoint set at {LastCheckpointPosition}");
    }
}
