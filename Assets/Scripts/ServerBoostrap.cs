using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Attached to a GameObject in the ServerScene to start the server automatically.
/// </summary>
public class ServerBootstrap : MonoBehaviour
{
    private void Start()
    {
        // Confirm we have a NetworkManager in the scene (our custom GameNetworkManager).
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("No NetworkManager found in ServerScene! Server cannot start.");
            return;
        }

        // Start the server
        NetworkManager.Singleton.StartServer();
        Debug.Log("Server started in ServerScene.");

        // (Optional) If you want to load a different scene for gameplay
        // automatically from the server, you could do:
        // NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
    }
}