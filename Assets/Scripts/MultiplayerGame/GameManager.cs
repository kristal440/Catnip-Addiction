using System.Collections;
using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using TMPro;

public class GameManager : MonoBehaviourPunCallbacks
{
    // TODO: spectator mode
    // TODO: leaderboard
    public static GameManager Instance;

    [Header("Settings")]
    public float countdownDuration = 5f;
    public Transform[] spawnPoints;
    public GameObject countdownUI;
    public TMP_Text countdownText;

    [Header("Game State")]
    public bool gameStarted;
    public float startTime;
    private Dictionary<int, float> finishTimes = new();

    private void Awake() => Instance = this;

    void Start()
    {
        // Ensure PhotonView is properly initialized
        if (photonView == null)
        {
            gameObject.AddComponent<PhotonView>();
        }
    }

    void Update()
    {
        if (PhotonNetwork.IsMasterClient && !gameStarted)
        {
            // Start countdown when room fills
            if (PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers)
            {
                photonView.RPC("StartCountdown", RpcTarget.All);
            }
        }
    }

    [PunRPC]
    void StartCountdown()
    {
        gameStarted = true;
        StartCoroutine(CountdownCoroutine());
    }

    IEnumerator CountdownCoroutine()
    {
        countdownUI.SetActive(true);
        var remainingTime = countdownDuration;

        while (remainingTime > 0)
        {
            countdownText.text = Mathf.CeilToInt(remainingTime).ToString();
            remainingTime -= Time.deltaTime;
            yield return null;
        }

        countdownUI.SetActive(false);
        photonView.RPC("StartGame", RpcTarget.All);
    }

    [PunRPC]
    void StartGame()
    {
        // Teleport players to spawn points
        var players = FindObjectsByType<PlayerController>(sortMode: FindObjectsSortMode.None);
        foreach (var p in players)
        {
            var spawnIndex = p.photonView.Owner.ActorNumber % spawnPoints.Length;
            p.Teleport(spawnPoints[spawnIndex].position);
            p.SetMovement(true);
        }
    }

    public void PlayerFinished(int playerId, float finishTime)
    {
        if (!finishTimes.ContainsKey(playerId))
        {
            finishTimes.Add(playerId, finishTime);
            photonView.RPC("UpdateLeaderboard", RpcTarget.All, playerId, finishTime);
        }
    }

    [PunRPC]
    void UpdateLeaderboard(int playerId, float finishTime)
    {
        // Add your UI update logic here
        Debug.Log($"{PhotonNetwork.CurrentRoom.GetPlayer(playerId).NickName} finished in {finishTime}s");

        if (finishTimes.Count == PhotonNetwork.CurrentRoom.PlayerCount)
        {
            PhotonNetwork.LoadLevel("LeaderboardScene");
        }
    }
}