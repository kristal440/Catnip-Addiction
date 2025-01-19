using Unity.Netcode;
using UnityEngine;

public class ServerBootstrap : MonoBehaviour
{
    [SerializeField] private bool autoStartServer = true;
    [SerializeField] private bool autoLoadGameScene = false;

    private void Start()
    {
        if (!autoStartServer) return;

        // Start the server using the ServerNetworkManager's NetworkManager
        if (ServerNetworkManager.Instance != null && ServerNetworkManager.Instance.NetworkManager.StartServer())
        {
            Debug.Log("Server started.");
        }
        else
        {
            Debug.LogError("Failed to start the server.");
        }

        if (autoLoadGameScene)
        {
            ServerNetworkManager.Instance.SwitchToGameScene();
        }
    }
}