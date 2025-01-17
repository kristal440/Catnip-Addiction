using UnityEngine;
using Unity.Netcode;

public class ServerCommandLine : MonoBehaviour
{
    private void Start()
    {
        if (!Application.isBatchMode) return;
        Debug.Log("Running in batch mode. Starting server now.");
        StartDedicatedServer();
    }

    private static void StartDedicatedServer()
    {
        if (!NetworkManager.Singleton.IsServer && !NetworkManager.Singleton.IsClient)
        {
            NetworkManager.Singleton.StartServer();
            Debug.Log("Server started ig.");
        }
        else
        {
            Debug.LogWarning("Server already started.");
        }
    }
}