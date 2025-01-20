using Unity.Netcode;
using UnityEngine;

public class ServerBootstrap : MonoBehaviour
{
    [SerializeField] private bool autoStartServer = true;

    private void Start()
    {
        if (!autoStartServer) return;

        // Check if running in batch mode (dedicated server)
        if (!Application.isBatchMode) return;
        // Dedicated server logic
        if (NetworkManager.Singleton.StartServer())
        {
            Debug.Log("Dedicated server started (batch mode).");
        }
        else
        {
            Debug.LogError("Failed to start dedicated server.");
        }
    }
}