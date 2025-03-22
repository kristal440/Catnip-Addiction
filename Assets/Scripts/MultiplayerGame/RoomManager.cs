using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public GameObject playerPrefab;
    public Transform[] spawnPoints;
    public TMP_Text pingText;

    [Header("Player List")]
    public Transform playerListContainer; // Parent object for the list items
    public GameObject playerListItemPrefab; // Prefab with TMP_Text for player name
    private Dictionary<int, GameObject> _playerListItems = new();

    private void Start()
    {
        if (PhotonNetwork.PlayerList.Contains(PhotonNetwork.LocalPlayer) && PhotonNetwork.CurrentRoom.PlayerCount == 1)
            InstantiatePlayer();

        UpdatePlayerList();
    }

    public void Update()
    {
        pingText.text = $"{PhotonNetwork.GetPing()}ms";
    }

    public override void OnJoinedRoom()
    {
        InstantiatePlayer();
        UpdatePlayerList();
    }

    private void InstantiatePlayer()
    {
        if (playerPrefab is not null && PhotonNetwork.IsConnected)
        {
            var spawnIndex = PhotonNetwork.LocalPlayer.ActorNumber % spawnPoints.Length;
            var spawn = spawnPoints.Length > 0 ? spawnPoints[spawnIndex] : null;

            var spawnPosition = spawn?.position ?? Vector3.zero;
            var spawnRotation = spawn?.rotation ?? Quaternion.identity;

            PhotonNetwork.Instantiate(playerPrefab.name, spawnPosition, spawnRotation);
        }
        else
            Debug.LogError("Player prefab is null or not connected to Photon! Cannot instantiate player.");
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        UpdatePlayerList();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        UpdatePlayerList();
    }

    private void ClearPlayerList()
    {
        foreach (var item in _playerListItems.Values)
        {
            Destroy(item);
        }
        _playerListItems.Clear();
    }

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
            {
                nameText.color = Color.green;
            }
        }
        else
        {
            Debug.LogError("TMP_Text component not found in player list item prefab!");
        }

        _playerListItems[player.ActorNumber] = listItem;
    }
}
