using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Manages multiplayer room state, player spawning and the player list UI display.
/// </summary>
public class RoomManager : MonoBehaviourPunCallbacks
{
    [SerializeField] [Tooltip("Prefab to instantiate for each player joining the game")] public GameObject playerPrefab;

    [Header("Player List")]
    [SerializeField] [Tooltip("Container for player list UI elements")] public Transform playerListContainer;
    [SerializeField] [Tooltip("Prefab for each player entry in the player list")] public GameObject playerListItemPrefab;
    private readonly Dictionary<int, GameObject> _playerListItems = new();

    [Header("Player Count Display")]
    [SerializeField] [Tooltip("Text component showing current player count")] public TMP_Text playerCountText;
    [SerializeField] [Tooltip("Color for normal player count display")] public Color normalColor = Color.white;
    [SerializeField] [Tooltip("Color when room is at maximum capacity")] public Color fullRoomColor = Color.green;

    /// Initializes the room and updates player displays
    private void Start()
    {
        if (PhotonNetwork.PlayerList.Contains(PhotonNetwork.LocalPlayer) && PhotonNetwork.CurrentRoom.PlayerCount == 1)
            InstantiatePlayer();

        UpdatePlayerList();
        UpdatePlayerCountDisplay();
    }

    /// <inheritdoc />
    /// Called when the local player joins a room
    public override void OnJoinedRoom()
    {
        InstantiatePlayer();
        UpdatePlayerList();
        UpdatePlayerCountDisplay();
    }

    /// <inheritdoc />
    /// Called when a new player enters the room
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerList();
        UpdatePlayerCountDisplay();
    }

    /// <inheritdoc />
    /// Called when a player leaves the room
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerList();
        UpdatePlayerCountDisplay();
    }

    /// Spawns the player character for the local player
    private void InstantiatePlayer()
    {
        if (playerPrefab && PhotonNetwork.IsConnected)
        {
            var spawnPosition = Vector3.zero;
            var spawnRotation = Quaternion.identity;

            PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, spawnRotation);
        }
        else
        {
            Debug.LogError("Player prefab is null or not connected to Photon! Cannot instantiate player.");
        }
    }

    /// Removes all player entries from the player list UI
    private void ClearPlayerList()
    {
        foreach (var item in _playerListItems.Values)
            Destroy(item);
        _playerListItems.Clear();
    }

    /// Refreshes the player list UI with current players
    private void UpdatePlayerList()
    {
        Debug.Log($"Updating player list. Player count: {PhotonNetwork.PlayerList.Length}");
        ClearPlayerList();

        foreach (var player in PhotonNetwork.PlayerList)
        {
            Debug.Log($"Adding player to list: {player.NickName} (Actor: {player.ActorNumber})");
            AddPlayerToList(player);
        }
    }

    /// Updates the player count display and colors
    private void UpdatePlayerCountDisplay()
    {
        if (!playerCountText || PhotonNetwork.CurrentRoom == null) return;

        var currentPlayers = PhotonNetwork.CurrentRoom.PlayerCount;
        var maxPlayers = PhotonNetwork.CurrentRoom.MaxPlayers;

        playerCountText.text = $"{currentPlayers}/{maxPlayers}";

        playerCountText.color = (currentPlayers == maxPlayers) ? fullRoomColor : normalColor;
    }

    /// Creates a UI entry for a player in the player list
    private void AddPlayerToList(Player player)
    {
        var listItem = Instantiate(playerListItemPrefab, playerListContainer);

        listItem.SetActive(true);

        Debug.Log($"Created list item for player: {player.NickName}");

        var nameText = listItem.GetComponentInChildren<TMP_Text>();
        if (nameText)
        {
            nameText.text = player.NickName;
            Debug.Log($"Set name text to: {player.NickName}");

            if (player.IsLocal)
                nameText.color = Color.green;
        }
        else
        {
            Debug.LogError("TMP_Text component not found in player list item prefab!");
        }

        _playerListItems[player.ActorNumber] = listItem;
    }
}
