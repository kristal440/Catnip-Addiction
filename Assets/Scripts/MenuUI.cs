using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class MenuUI : NetworkBehaviour
{
    public static MenuUI Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("UI element showing the list of rooms (e.g., a scroll rect or container).")]
    public Transform roomListContainer;

    [Tooltip("Prefab for displaying each room entry in the list.")]
    public GameObject roomListEntryPrefab;

    [Tooltip("Input field for entering a new room name.")]
    public TMP_InputField roomNameInput;

    [Tooltip("Button to create a new room.")]
    public Button createRoomButton;

    [Tooltip("Button to start the client.")]
    public Button startClientButton;

    private List<RoomData> currentRooms = new List<RoomData>();

    private void Awake()
    {
        // Singleton pattern: Only allow one instance of this manager.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        if (IsClient)
        {
            // Introduce a delay before subscribing to the event
            Invoke(nameof(SubscribeToRoomManagerEvent), 0.5f); // Delay of 0.5 seconds
        }
    }

    private void SubscribeToRoomManagerEvent()
    {
        // Subscribe to the OnRoomManagerSpawned event
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnRoomManagerSpawned += HandleRoomManagerSpawned;
        }
        else
        {
            Debug.LogError("ServerNetworkManager.Instance is null in MenuUI!");
        }
    }

    private void OnEnable()
    {
        // Example usage: Add listeners to your UI buttons
        if (createRoomButton != null)
        {
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        }
        if (startClientButton != null)
        {
            startClientButton.onClick.AddListener(OnStartClientClicked);
        }
    }

    private void OnDisable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);

        if (startClientButton != null)
            startClientButton.onClick.RemoveListener(OnStartClientClicked);

        // Unsubscribe from events when the MenuUI is disabled
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.OnRoomManagerSpawned -= HandleRoomManagerSpawned;

            if (ServerNetworkManager.Instance.roomManager != null)
            {
                ServerNetworkManager.Instance.roomManager.OnRoomListChanged -= HandleRoomListChanged;
            }
        }
    }

    /// <summary>
    /// Invoked when the "Create Room" button is clicked.
    /// </summary>
    private void OnCreateRoomClicked()
    {
        Debug.Log("Create Room button clicked!");
        string roomName = roomNameInput != null ? roomNameInput.text : "DefaultRoom";

        if (ServerNetworkManager.Instance && ServerNetworkManager.Instance.roomManager != null)
        {
            Debug.Log($"Calling CreateRoomServerRpc with room name: {roomName}");
            ServerNetworkManager.Instance.roomManager.CreateRoomServerRpc(roomName, 4);
        }
        else
        {
            Debug.LogError("ServerNetworkManager.Instance or ServerNetworkManager.Instance.roomManager is null!");
        }
    }

    private void OnStartClientClicked()
    {
        Debug.Log("Start Client button clicked!");
        if (ClientNetworkManager.Instance != null)
        {
            ClientNetworkManager.Instance.NetworkManager.StartClient();
        }
        else
        {
            Debug.LogError("ClientNetworkManager.Instance is null!");
        }
    }

    /// <summary>
    /// Refreshes the UI list of rooms from the RoomManager.
    /// </summary>
    public void RefreshRoomList()
    {
        if (!IsClient) return;

        // Clear old entries
        foreach (Transform child in roomListContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var room in currentRooms)
        {
            // Instantiate a UI element for each room
            GameObject entryObj = Instantiate(roomListEntryPrefab, roomListContainer);
            // Suppose this prefab has a text field and a "Join" button
            RoomListEntry entry = entryObj.GetComponent<RoomListEntry>();
            if (entry != null)
            {
                entry.SetRoomInfo(room.roomName, room.connectedPlayers.Length, room.maxPlayers);
                // Hook up the join button
                entry.joinButton.onClick.AddListener(() =>
                {
                    // Attempt to join the room
                    if (ServerNetworkManager.Instance && ServerNetworkManager.Instance.roomManager)
                        ServerNetworkManager.Instance.roomManager.JoinRoomServerRpc(room.roomId, NetworkManager.Singleton.LocalClientId);
                });
            }
        }
    }

    private void RequestRoomList()
    {
        if (ServerNetworkManager.Instance && ServerNetworkManager.Instance.roomManager != null)
        {
            ServerNetworkManager.Instance.roomManager.RequestRoomListServerRpc();
        }
    }

    public void AddRoomData(RoomData roomData)
    {
        if (!currentRooms.Contains(roomData))
        {
            currentRooms.Add(roomData);
            RefreshRoomList();
        }
    }

    private void HandleRoomManagerSpawned(RoomManager roomManager)
    {
        Debug.Log("[Client] HandleRoomManagerSpawned called!"); // Add this line

        // Now we have a reference to the RoomManager
        if (ServerNetworkManager.Instance != null)
        {
            ServerNetworkManager.Instance.roomManager.OnRoomListChanged += HandleRoomListChanged;
        }
        RequestRoomList();
    }

    private void HandleRoomListChanged()
    {
        // Refresh the room list when it changes
        currentRooms = ServerNetworkManager.Instance.roomManager.GetRoomList();
        RefreshRoomList();
    }
}