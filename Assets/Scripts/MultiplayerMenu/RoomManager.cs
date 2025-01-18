using System;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class RoomManager : NetworkBehaviour
{
    public static RoomManager Instance { get; private set; }

    // NetworkList to store active rooms on the server
    private NetworkList<RoomData> _rooms;

    // Dictionary to store players in each room (RoomID -> List of ClientIDs)
    private Dictionary<FixedString64Bytes, NetworkList<ulong>> _roomPlayers = new Dictionary<FixedString64Bytes, NetworkList<ulong>>();

    // Prefab for the player
    public GameObject playerPrefab;

    // UI elements
    public GameObject roomListPanel;
    public GameObject roomEntryPrefab;
    public TMP_InputField createRoomInput;
    public Button createRoomButton;

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            _rooms = new NetworkList<RoomData>();
        }

        if (IsClient)
        {
            _rooms = new NetworkList<RoomData>();
            _rooms.OnListChanged += UpdateRoomListUI;
            UpdateRoomListUI(new NetworkListEvent<RoomData>()); // Initial update
        }

        if (IsOwner)
        {
            createRoomButton.onClick.AddListener(CreateRoom);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Struct to represent room data (without the player list)
    public struct RoomData : INetworkSerializable, IEquatable<RoomData>
    {
        public FixedString64Bytes RoomID;
        public ulong HostClientID;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref RoomID);
            serializer.SerializeValue(ref HostClientID);
        }

        public bool Equals(RoomData other)
        {
            return RoomID.Equals(other.RoomID);
        }

        public override int GetHashCode()
        {
            return RoomID.GetHashCode();
        }
    }

    #region Room Management

    [ServerRpc(RequireOwnership = false)]
    public void CreateRoomServerRpc(string roomID, ServerRpcParams rpcParams = default)
    {
        if (string.IsNullOrEmpty(roomID)) return;

        FixedString64Bytes fixedRoomID = roomID;

        // Check for duplicate room ID
        foreach (var room in _rooms)
        {
            if (room.RoomID.Value == fixedRoomID.Value)
            {
                Debug.LogWarning($"Room with ID '{roomID}' already exists.");
                // Optionally send a ClientRpc to inform the client
                return;
            }
        }

        RoomData newRoom = new RoomData
        {
            RoomID = fixedRoomID,
            HostClientID = rpcParams.Receive.SenderClientId // Access SenderClientId through rpcParams.Receive
        };
        _rooms.Add(newRoom);

        // Initialize the player list for the room
        _roomPlayers.Add(fixedRoomID, new NetworkList<ulong>());
        _roomPlayers[fixedRoomID].Add(rpcParams.Receive.SenderClientId); // Access SenderClientId through rpcParams.Receive

        // Update the player's room association and host status
        SetPlayerRoom(rpcParams.Receive.SenderClientId, roomID); // Access SenderClientId through rpcParams.Receive
        SetPlayerHostStatus(rpcParams.Receive.SenderClientId, true); // Access SenderClientId through rpcParams.Receive

        Debug.Log($"Room '{roomID}' created by client {rpcParams.Receive.SenderClientId}."); // Access SenderClientId through rpcParams.Receive
    }

    [ServerRpc(RequireOwnership = false)]
    public void DeleteRoomServerRpc(string roomID, ServerRpcParams rpcParams = default)
    {
        FixedString64Bytes fixedRoomID = roomID;
        RoomData? roomToRemove = null;
        foreach (var room in _rooms)
        {
            if (room.RoomID.Value == fixedRoomID.Value)
            {
                // Only the host can delete the room
                if (room.HostClientID == rpcParams.Receive.SenderClientId) // Access SenderClientId through rpcParams.Receive
                {
                    roomToRemove = room;
                    break;
                }
                else
                {
                    Debug.LogWarning($"Client {rpcParams.Receive.SenderClientId} is not the host of room '{roomID}' and cannot delete it."); // Access SenderClientId through rpcParams.Receive
                    return;
                }
            }
        }

        if (roomToRemove.HasValue)
        {
            // Remove all players from the room before deleting it
            if (_roomPlayers.ContainsKey(fixedRoomID))
            {
                foreach (var clientId in _roomPlayers[fixedRoomID])
                {
                    SetPlayerRoom(clientId, ""); // Move players back to the main list (no room)
                    SetPlayerHostStatus(clientId, false); // Remove host status
                }
                _roomPlayers.Remove(fixedRoomID);
            }

            _rooms.Remove(roomToRemove.Value);
            Debug.Log($"Room '{roomID}' deleted by host {rpcParams.Receive.SenderClientId}."); // Access SenderClientId through rpcParams.Receive
        }
        else
        {
            Debug.LogWarning($"Room with ID '{roomID}' not found.");
        }
    }

    #endregion

    #region Player Management

    [ServerRpc(RequireOwnership = false)]
    public void JoinRoomServerRpc(string roomID, ServerRpcParams rpcParams = default)
    {
        FixedString64Bytes fixedRoomID = roomID;
        RoomData? targetRoom = null;
        foreach (var room in _rooms)
        {
            if (room.RoomID.Value == fixedRoomID.Value)
            {
                targetRoom = room;
                break;
            }
        }

        if (targetRoom.HasValue)
        {
            // Check if the player is already in a room
            NetworkManager.Singleton.ConnectedClients[rpcParams.Receive.SenderClientId].PlayerObject.TryGetComponent<PlayerNetwork>(out var playerNetwork);
            if (playerNetwork != null && !string.IsNullOrEmpty(playerNetwork.currentRoomID.Value.ToString()))
            {
                Debug.LogWarning($"Client {rpcParams.Receive.SenderClientId} is already in room '{playerNetwork.currentRoomID.Value}'.");
                return;
            }

            // Add player to the room's player list
            if (_roomPlayers.ContainsKey(fixedRoomID))
            {
                _roomPlayers[fixedRoomID].Add(rpcParams.Receive.SenderClientId); // Access SenderClientId through rpcParams.Receive

                // Update the player's room association
                SetPlayerRoom(rpcParams.Receive.SenderClientId, roomID); // Access SenderClientId through rpcParams.Receive

                Debug.Log($"Client {rpcParams.Receive.SenderClientId} joined room '{roomID}'."); // Access SenderClientId through rpcParams.Receive
            }
            else
            {
                Debug.LogError($"Room player list not found for room '{roomID}'.");
            }
        }
        else
        {
            Debug.LogWarning($"Room with ID '{roomID}' not found.");
            // Optionally send a ClientRpc to inform the client
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void LeaveRoomServerRpc(ServerRpcParams rpcParams = default)
    {
        NetworkManager.Singleton.ConnectedClients[rpcParams.Receive.SenderClientId].PlayerObject.TryGetComponent<PlayerNetwork>(out var playerNetwork);
        if (playerNetwork != null && !string.IsNullOrEmpty(playerNetwork.currentRoomID.Value.ToString()))
        {
            FixedString64Bytes currentRoomID = playerNetwork.currentRoomID.Value;
            RoomData? targetRoom = null;
            foreach (var room in _rooms)
            {
                if (room.RoomID.Value == currentRoomID.Value)
                {
                    targetRoom = room;
                    break;
                }
            }

            if (targetRoom.HasValue && _roomPlayers.ContainsKey(currentRoomID))
            {
                // Remove player from the room's player list
                _roomPlayers[currentRoomID].Remove(rpcParams.Receive.SenderClientId); // Access SenderClientId through rpcParams.Receive

                // Update the player's room association and host status
                SetPlayerRoom(rpcParams.Receive.SenderClientId, ""); // Access SenderClientId through rpcParams.Receive
                SetPlayerHostStatus(rpcParams.Receive.SenderClientId, false); // Access SenderClientId through rpcParams.Receive

                // Check if the leaving player was the host, and assign a new one if needed
                if (targetRoom.Value.HostClientID == rpcParams.Receive.SenderClientId && _roomPlayers[currentRoomID].Count > 0) // Access SenderClientId through rpcParams.Receive
                {
                    AssignNewHostServerRpc(currentRoomID.Value, _roomPlayers[currentRoomID][0]);
                }

                Debug.Log($"Client {rpcParams.Receive.SenderClientId} left room '{currentRoomID}'."); // Access SenderClientId through rpcParams.Receive
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    public void AssignNewHostServerRpc(string roomID, ulong newHostClientID)
    {
        FixedString64Bytes fixedRoomID = roomID;
        RoomData? targetRoom = null;
        int roomIndex = -1;
        for (int i = 0; i < _rooms.Count; i++)
        {
            if (_rooms[i].RoomID.Value == fixedRoomID.Value)
            {
                targetRoom = _rooms[i];
                roomIndex = i;
                break;
            }
        }

        if (targetRoom.HasValue)
        {
            RoomData updatedRoom = targetRoom.Value;
            updatedRoom.HostClientID = newHostClientID;
            _rooms[roomIndex] = updatedRoom;
            SetPlayerHostStatus(newHostClientID, true);
            Debug.Log($"Room '{roomID}': New host assigned - Client {newHostClientID}");
        }
    }

    private void SetPlayerRoom(ulong clientId, string roomID)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var networkClient) &&
            networkClient.PlayerObject.TryGetComponent<PlayerNetwork>(out var playerNetwork))
        {
            playerNetwork.SetCurrentRoomID(roomID);
        }
    }

    private void SetPlayerHostStatus(ulong clientId, bool isHost)
    {
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var networkClient) &&
            networkClient.PlayerObject.TryGetComponent<PlayerNetwork>(out var playerNetwork))
        {
            playerNetwork.SetIsRoomHost(isHost);
        }
    }

    #endregion

    #region UI

    private void UpdateRoomListUI(NetworkListEvent<RoomData> changeEvent)
    {
        if (!IsClient) return;

        // Clear existing room entries
        foreach (Transform child in roomListPanel.transform)
        {
            Destroy(child.gameObject);
        }

        // Instantiate and populate room entries
        foreach (var room in _rooms)
        {
            GameObject roomEntry = Instantiate(roomEntryPrefab, roomListPanel.transform);
            TextMeshProUGUI roomNameText = roomEntry.GetComponentInChildren<TextMeshProUGUI>();
            Button joinButton = roomEntry.GetComponentInChildren<Button>();
            TextMeshProUGUI playerCountText = roomEntry.transform.Find("PlayerCount").GetComponent<TextMeshProUGUI>(); // Assuming you have a Text element for player count

            roomNameText.text = room.RoomID.Value;

            if (_roomPlayers.ContainsKey(room.RoomID))
            {
                playerCountText.text = _roomPlayers[room.RoomID].Count.ToString();
            }
            else
            {
                playerCountText.text = "0";
            }

            joinButton.onClick.RemoveAllListeners();
            string localRoomID = room.RoomID.Value; // Capture the loop variable
            joinButton.onClick.AddListener(() => JoinRoom(localRoomID));

            // Add delete button if the local player is the host
            if (room.HostClientID == NetworkManager.Singleton.LocalClientId)
            {
                GameObject deleteButtonGo = new GameObject("DeleteButton", typeof(RectTransform));
                deleteButtonGo.transform.SetParent(roomEntry.transform);
                deleteButtonGo.AddComponent<CanvasRenderer>();
                Image deleteButtonImage = deleteButtonGo.AddComponent<Image>();
                deleteButtonImage.color = Color.red;

                Button deleteButton = deleteButtonGo.AddComponent<Button>();
                TextMeshProUGUI deleteButtonText = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                deleteButtonText.transform.SetParent(deleteButtonGo.transform);
                deleteButtonText.text = "Delete";
                deleteButtonText.alignment = TextAlignmentOptions.Center;
                deleteButtonText.color = Color.white;

                RectTransform rt = deleteButtonGo.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(1, 0);
                rt.anchorMax = new Vector2(1, 1);
                rt.pivot = new Vector2(1, 0.5f);
                rt.sizeDelta = new Vector2(80, 0);
                rt.anchoredPosition = new Vector2(-5, 0);

                deleteButton.onClick.AddListener(() => DeleteRoom(localRoomID));
            }
        }
    }

    public void CreateRoom()
    {
        CreateRoomServerRpc(createRoomInput.text);
        createRoomInput.text = "";
    }

    public void JoinRoom(string roomID)
    {
        JoinRoomServerRpc(roomID);
    }

    public void DeleteRoom(string roomID)
    {
        DeleteRoomServerRpc(roomID);
    }

    #endregion
}