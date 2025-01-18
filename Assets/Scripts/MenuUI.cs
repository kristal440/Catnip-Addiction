using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

/// <summary>
/// Controls the UI in the MultiplayerLobby scene.
/// Displays existing rooms, allows creating/joining rooms.
/// </summary>

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

    private void OnEnable()
    {
        // Example usage: Add listeners to your UI buttons
        if (createRoomButton != null)
        {
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        }

        if (IsClient)
            RequestRoomList();
    }

    private void OnDisable()
    {
        if (createRoomButton != null)
            createRoomButton.onClick.RemoveListener(OnCreateRoomClicked);
    }

    /// <summary>
    /// Invoked when the "Create Room" button is clicked.
    /// </summary>
    private void OnCreateRoomClicked()
    {
        string roomName = roomNameInput != null ? roomNameInput.text : "DefaultRoom";

        var rm = GameNetworkManager.Instance.roomManager;

        if (rm != null)
        {
            // Call the server to create the room
            rm.CreateRoomServerRpc(roomName, 4);
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
                    var rm = GameNetworkManager.Instance.roomManager;
                   if(rm)
                        rm.JoinRoomServerRpc(room.roomId, NetworkManager.Singleton.LocalClientId);
                });
            }
        }
    }

    private void RequestRoomList()
    {
        var rm = GameNetworkManager.Instance.roomManager;
        if (rm != null)
        {
            rm.RequestRoomListServerRpc();
        }
    }

     public void AddRoomData(RoomData roomData)
    {
        currentRooms.Add(roomData);
        RefreshRoomList();
    }
}