using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages creation, tracking, and removal of rooms on the server side.
/// </summary>

public class RoomManager : MonoBehaviour
{
    // In a real system, you'd likely store room data on the server (host).
    private Dictionary<string, RoomData> rooms = new Dictionary<string, RoomData>();

    /// <summary>
    /// Creates a new room with the given name and max player count.
    /// Returns the created RoomData or perhaps just the roomId.
    /// </summary>
    public RoomData CreateRoom(string roomName, int maxPlayers = 4)
    {
        string roomId = System.Guid.NewGuid().ToString(); // Generate a unique ID
        var newRoom = new RoomData(roomId, roomName, maxPlayers);
        rooms.Add(roomId, newRoom);

        Debug.Log($"Created Room: {roomName} (ID: {roomId}, MaxPlayers: {maxPlayers})");
        return newRoom;
    }

    /// <summary>
    /// Joins the specified room with the given playerId (Netcode ClientID).
    /// Returns true if join was successful, false otherwise.
    /// </summary>
    public bool JoinRoom(string roomId, ulong playerId)
    {
        if (!rooms.ContainsKey(roomId))
        {
            Debug.LogWarning($"JoinRoom failed. RoomId {roomId} not found.");
            return false;
        }

        RoomData room = rooms[roomId];
        if (room.IsFull())
        {
            Debug.LogWarning($"JoinRoom failed. RoomId {roomId} is full.");
            return false;
        }

        // Add player to the room
        room.AddPlayer(playerId);
        Debug.Log($"Player {playerId} joined room {roomId}.");

        // If the room is now full, we could signal to start the game, etc.
        if (room.IsFull())
        {
            Debug.Log($"Room {roomId} is now full. Starting game...");
            // For example:
            // GameNetworkManager.Instance.SwitchToGameScene();
        }

        return true;
    }

    /// <summary>
    /// Returns a list of current rooms (for display in a lobby).
    /// </summary>
    public List<RoomData> GetRoomList()
    {
        return new List<RoomData>(rooms.Values);
    }

    // Additional methods such as removing a player from a room,
    // or completely removing a room, can be added here.
}