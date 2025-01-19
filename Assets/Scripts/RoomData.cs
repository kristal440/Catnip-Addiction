using Unity.Netcode;
using System;
using System.Linq;

/// <summary>
/// Simple data container representing a single room.
/// </summary>

[Serializable]
public struct RoomData : INetworkSerializable
{
    public string roomId;
    public string roomName;
    public int maxPlayers;
    public ulong[] connectedPlayers; // Changed to ulong[]
    public bool isInProgress;

    // Constructors
    public RoomData(string id, string name, int max)
    {
        roomId = id;
        roomName = name;
        maxPlayers = max;
        connectedPlayers = Array.Empty<ulong>(); //Initialize with empty array
        isInProgress = false;
    }

    /// <summary>
    /// Helper method to check if room is full.
    /// </summary>
    public bool IsFull()
    {
        return connectedPlayers.Length >= maxPlayers;
    }

    /// <summary>
    /// Adds a player to the connectedPlayers list (if not full).
    /// </summary>
     public void AddPlayer(ulong playerId)
    {
         if (!IsFull())
         {
             ulong[] newArray = new ulong[connectedPlayers.Length + 1];
             connectedPlayers.CopyTo(newArray, 0);
             newArray[connectedPlayers.Length] = playerId;
             connectedPlayers = newArray;
         }
    }

    /// <summary>
    /// (Optional) Removes a player from the room.
    /// </summary>
    public void RemovePlayer(ulong playerId)
    {
        if(connectedPlayers.Contains(playerId))
            connectedPlayers = connectedPlayers.Where(x=> x != playerId).ToArray();
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref roomId);
        serializer.SerializeValue(ref roomName);
        serializer.SerializeValue(ref maxPlayers);
        // Serialize the length of the array first
        int length = connectedPlayers == null ? 0 : connectedPlayers.Length;
        serializer.SerializeValue(ref length);
        // Then serialize each element of the array
        if (serializer.IsReader)
        {
            connectedPlayers = new ulong[length];
        }

        for (int n = 0; n < length; n++)
        {
            serializer.SerializeValue(ref connectedPlayers[n]);
        }
        serializer.SerializeValue(ref isInProgress);
    }
}