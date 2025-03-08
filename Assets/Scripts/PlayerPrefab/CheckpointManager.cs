using UnityEngine;

public class CheckpointManager : MonoBehaviour
{
    [SerializeField] private string checkpointTag = "Checkpoint"; // tag checkpoint objects, add 2d collider
    public static Vector3 LastCheckpointPosition { get; private set; }

    private void Start()
    {
        LastCheckpointPosition = transform.position;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag(checkpointTag)) return;
        LastCheckpointPosition = other.transform.position;
        Debug.Log($"Checkpoint set at {LastCheckpointPosition}");
    }
}