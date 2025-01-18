using System.Collections.Generic;

/// <summary>
/// Simple data container representing a single room.
/// </summary>

[System.Serializable]
public class RoomData
{
    public string roomId;
    public string roomName;
    public int maxPlayers;
    public List<ulong> connectedPlayers;
    public bool isInProgress;

    // Constructors
    public RoomData(string id, string name, int max)
    {
        roomId = id;
        roomName = name;
        maxPlayers = max;
        connectedPlayers = new List<ulong>();
        isInProgress = false;
    }

    /// <summary>
    /// Helper method to check if room is full.
    /// </summary>
    public bool IsFull()
    {
        return connectedPlayers.Count >= maxPlayers;
    }

    /// <summary>
    /// Adds a player to the connectedPlayers list (if not full).
    /// </summary>
    public void AddPlayer(ulong playerId)
    {
        if (!IsFull())
        {
            connectedPlayers.Add(playerId);
        }
    }

    /// <summary>
    /// (Optional) Removes a player from the room.
    /// </summary>
    public void RemovePlayer(ulong playerId)
    {
        if (connectedPlayers.Contains(playerId))
        {
            connectedPlayers.Remove(playerId);
        }
    }
}