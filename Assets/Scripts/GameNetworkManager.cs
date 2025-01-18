using Unity.Netcode;
using UnityEngine;
using static SwitchScene;

/// <summary>
/// Manages network operations (hosting, joining as client) and persists across scenes.
/// </summary>

public class GameNetworkManager : NetworkManager
{
    public static GameNetworkManager Instance { get; private set; }

    [Header("References")]
    [Tooltip("Assign a RoomManager (prefab or in-scene).")]
    public RoomManager roomManager;

    [Tooltip("Optional Player Prefab assigned from Inspector (for spawn).")]
    public GameObject playerPrefab;

    private void Awake()
    {
        // Singleton pattern: Only allow one instance of this manager.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        OnServerStarted += HandleServerStarted;
    }

    private void OnDisable()
    {
        OnServerStarted -= HandleServerStarted;
    }


    /// <summary>
    /// Called when the user clicks "Host" in your UI or code triggers hosting.
    /// </summary>
    public void StartHosting()
    {
        // Set the player prefab if needed:
        if (playerPrefab != null)
        {
            NetworkConfig.PlayerPrefab = playerPrefab;
        }

        StartHost();
    }

    /// <summary>
    /// Called when the user clicks "Join" in your UI or code triggers joining as client.
    /// </summary>
    public void StartClientConnection()
    {
        // Set the player prefab if needed:
        if (playerPrefab != null)
        {
            NetworkConfig.PlayerPrefab = playerPrefab;
        }

        // Start client connection
        StartClient();
    }

    /// <summary>
    /// Netcode callback after the server has started.
    /// </summary>
    private void HandleServerStarted()
    {
        if (IsServer)
        {
            // Ensure the RoomManager is ready.
            // (You could also instantiate a RoomManager here if not assigned.)
            if (roomManager != null)
            {
                Debug.Log("Server started. RoomManager is set up.");
                // Any additional setup logic.
            }
            else
            {
                Debug.LogWarning("RoomManager not assigned to GameNetworkManager!");
            }
        }
    }

    /// <summary>
    /// Example for switching to the Lobby scene (called by host/server).
    /// </summary>
    [ContextMenu("SwitchToLobbyScene")]
    public void SwitchToLobbyScene()
    {
        if (IsServer)
        {
            LoadScene("MultiplayerMenu");
        }
    }

    /// <summary>
    /// Example for switching to the Game scene (called by host/server).
    /// </summary>
    [ContextMenu("SwitchToGameScene")]
    public void SwitchToGameScene()
    {
        if (IsServer)
        {
            LoadScene("GameScene");
        }
    }
}