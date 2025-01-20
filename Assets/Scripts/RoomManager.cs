using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Linq;

public class RoomManager : NetworkBehaviour
{
    private List<RoomData> rooms = new List<RoomData>();

    public struct RoomDataChange : INetworkSerializable
    {
        public RoomData RoomData;
        public OperationType OperationType;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            RoomData.NetworkSerialize(serializer);
            serializer.SerializeValue(ref OperationType);
        }
    }

    public enum OperationType
    {
        Add,
        Update,
        Remove
    }

    // Event that is triggered when the room list changes
    public delegate void RoomListChangedEventHandler();
    public event RoomListChangedEventHandler OnRoomListChanged;

    // [ServerRpc] to create room on the server.
    [ServerRpc]
    public void CreateRoomServerRpc(string roomName, int maxPlayers, ServerRpcParams serverRpcParams = default)
    {
        string roomId = System.Guid.NewGuid().ToString(); // Generate a unique ID
        var newRoom = new RoomData(roomId, roomName, maxPlayers);
        rooms.Add(newRoom);

        Debug.Log($"[Server] Created Room: {roomName} (ID: {roomId}, MaxPlayers: {maxPlayers})");

        // Notify clients
        NotifyRoomListChangedClientRpc(new RoomDataChange
        {
            RoomData = newRoom,
            OperationType = OperationType.Add
        });

        //join the room for the player who created it.
        JoinRoomServerRpc(roomId, serverRpcParams.Receive.SenderClientId);
    }

    // [ServerRpc] to join the room on the server.
    [ServerRpc]
    public void JoinRoomServerRpc(string roomId, ulong playerId, ServerRpcParams serverRpcParams = default)
    {
        if (!TryGetRoomData(roomId, out RoomData room))
        {
            Debug.LogWarning($"[Server] JoinRoom failed. RoomId {roomId} not found.");
            return;
        }

        if (room.IsFull())
        {
            Debug.LogWarning($"[Server] JoinRoom failed. RoomId {roomId} is full.");
            return;
        }

        // Add player to the room
        room.AddPlayer(playerId);

        //update the room in the network list
        UpdateRoomInList(room);

        Debug.Log($"[Server] Player {playerId} joined room {roomId}.");

        //Notify clients about the change
        NotifyRoomListChangedClientRpc(new RoomDataChange
        {
            RoomData = room,
            OperationType = OperationType.Update
        });

        // If the room is now full, we could signal to start the game, etc.
        if (room.IsFull())
        {
            Debug.Log($"[Server] Room {roomId} is now full. Starting game in 2 seconds...");
            StartCoroutine(StartGameDelayed(room));
        }
    }

    private IEnumerator StartGameDelayed(RoomData room)
    {
        yield return new WaitForSeconds(2f);
        // Only start the game if the room is still full after the delay
        if (TryGetRoomData(room.roomId, out RoomData updatedRoom) && updatedRoom.IsFull())
        {
            updatedRoom.isInProgress = true;
            UpdateRoomInList(updatedRoom);
            //ServerNetworkManager.Singleton.SwitchToGameScene();
        }
    }

    private void UpdateRoomInList(RoomData roomData)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].roomId == roomData.roomId)
            {
                rooms[i] = roomData;
                return;
            }
        }
    }
    private bool TryGetRoomData(string roomId, out RoomData roomData)
    {
        for (int i = 0; i < rooms.Count; i++)
        {
            if (rooms[i].roomId == roomId)
            {
                roomData = rooms[i];
                return true;
            }
        }
        roomData = default;
        return false;
    }

    [ClientRpc]
    private void NotifyRoomListChangedClientRpc(RoomDataChange roomDataChange)
    {
        Debug.Log($"Client received notification of room change: {roomDataChange.OperationType} room: {roomDataChange.RoomData.roomName}");

        // Invoke the event to notify subscribers (e.g., MenuUI)
        OnRoomListChanged?.Invoke();
    }

    /// <summary>
    /// Returns a list of current rooms (for display in a lobby).
    /// </summary>
    public List<RoomData> GetRoomList()
    {
        return new List<RoomData>(rooms);
    }

    [ServerRpc]
    public void RequestRoomListServerRpc(ServerRpcParams serverRpcParams = default)
    {
        foreach (var room in rooms)
        {
            SendRoomListClientRpc(room, new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { serverRpcParams.Receive.SenderClientId }
                }
            });
        }
    }

    [ClientRpc]
    private void SendRoomListClientRpc(RoomData roomData, ClientRpcParams clientRpcParams = default)
    {
        var menuUI = MenuUI.Instance;

        if (menuUI)
        {
            menuUI.AddRoomData(roomData);
        }
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        Debug.Log($"[Server] Client disconnected: {clientId}");

        // Find the room the player was in
        RoomData roomToRemove = default;
        bool roomFound = false;

        foreach (var room in rooms)
        {
            if (room.connectedPlayers.Contains(clientId))
            {
                room.RemovePlayer(clientId);
                Debug.Log($"[Server] Player {clientId} removed from room {room.roomId}.");

                if (room.connectedPlayers.Length == 0 && !room.isInProgress)
                {
                    // Mark the room for removal if it's empty and not in progress
                    roomToRemove = room;
                    roomFound = true;
                }
                else
                {
                    // Update the room in the list
                    UpdateRoomInList(room);

                    // Notify clients about the change
                    NotifyRoomListChangedClientRpc(new RoomDataChange
                    {
                        RoomData = room,
                        OperationType = OperationType.Update
                    });
                }
                break;
            }
        }

        // Remove the room if it's empty
        if (roomFound)
        {
            rooms.Remove(roomToRemove);
            Debug.Log($"[Server] Room {roomToRemove.roomId} removed because it was empty.");

            // Notify clients about the room removal
            NotifyRoomListChangedClientRpc(new RoomDataChange
            {
                RoomData = roomToRemove,
                OperationType = OperationType.Remove
            });
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // Subscribe to the client disconnection event
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();

        if (IsServer)
        {
            // Unsubscribe from the client disconnection event
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }
    }

}