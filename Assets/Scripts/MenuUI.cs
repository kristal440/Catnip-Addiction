using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls the UI in the MultiplayerLobby scene.
/// Displays existing rooms, allows creating/joining rooms.
/// </summary>

public class MenuUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("UI element showing the list of rooms (e.g., a scroll rect or container).")]
    public Transform roomListContainer;

    [Tooltip("Prefab for displaying each room entry in the list.")]
    public GameObject roomListEntryPrefab;

    [Tooltip("Input field for entering a new room name.")]
    public TMP_InputField roomNameInput;

    [Tooltip("Button to create a new room.")]
    public Button createRoomButton;

    // Assuming you have the Netcode/Network manager running in a persistent scene.
    private void Start()
    {
        // Example usage: Add listeners to your UI buttons
        if (createRoomButton != null)
        {
            createRoomButton.onClick.AddListener(OnCreateRoomClicked);
        }

        // Optionally fetch the list of rooms immediately.
        RefreshRoomList();
    }

    /// <summary>
    /// Invoked when the "Create Room" button is clicked.
    /// </summary>
    private void OnCreateRoomClicked()
    {
        string roomName = roomNameInput != null ? roomNameInput.text : "DefaultRoom";
        Console.WriteLine("roomName: " + roomName);
        var rm = GameNetworkManager.Instance.roomManager;

        if (rm != null)
        {
            // Create a new room via the RoomManager
            var createdRoom = rm.CreateRoom(roomName, 4); // example maxPlayers = 4
            // You could auto-join or simply refresh the room list
            rm.JoinRoom(createdRoom.roomId, GameNetworkManager.Instance.LocalClientId);
        }

        RefreshRoomList();
    }

    /// <summary>
    /// Refreshes the UI list of rooms from the RoomManager.
    /// </summary>
    public void RefreshRoomList()
    {
        // Clear old entries
        foreach (Transform child in roomListContainer)
        {
            Destroy(child.gameObject);
        }

        var rm = GameNetworkManager.Instance.roomManager;
        if (rm == null) return;

        List<RoomData> rooms = rm.GetRoomList();
        foreach (var room in rooms)
        {
            // Instantiate a UI element for each room
            GameObject entryObj = Instantiate(roomListEntryPrefab, roomListContainer);
            // Suppose this prefab has a text field and a "Join" button
            RoomListEntry entry = entryObj.GetComponent<RoomListEntry>();
            if (entry != null)
            {
                entry.SetRoomInfo(room.roomName, room.connectedPlayers.Count, room.maxPlayers);
                // Hook up the join button
                entry.joinButton.onClick.AddListener(() =>
                {
                    // Attempt to join the room
                    bool joined = rm.JoinRoom(room.roomId, GameNetworkManager.Instance.LocalClientId);
                    if (joined)
                    {
                        // Optionally switch scenes or wait for host to switch
                        GameNetworkManager.Instance.SwitchToGameScene();
                    }
                });
            }
        }
    }
}