using System.Linq;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;

public class RoomManager : MonoBehaviourPunCallbacks
{
    public GameObject playerPrefab;
    public Transform[] spawnPoints;
    public TMP_Text pingText;

    private void Start()
    {
        if (PhotonNetwork.PlayerList.Contains(PhotonNetwork.LocalPlayer) && PhotonNetwork.CurrentRoom.PlayerCount == 1)
            InstantiatePlayer();
    }

    public void Update()
    {
        pingText.text = $"{PhotonNetwork.GetPing()}ms";
    }

    public override void OnJoinedRoom()
    {
        InstantiatePlayer();
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
            Debug.Log($"Player instantiated at {spawnPosition} with ID: {PhotonNetwork.LocalPlayer.ActorNumber}");
        }
        else
        {
            Debug.LogError("Player prefab is null or not connected to Photon! Cannot instantiate player.");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log($"{newPlayer.NickName} entered the room.");
        Debug.Log("Current players in room:");
        foreach (var player in PhotonNetwork.PlayerList)
        {
            Debug.Log($"- {player.NickName}");
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        Debug.Log($"{otherPlayer.NickName} left the room.");
    }
}