using System.Collections;
using UnityEngine;
using Photon.Pun;
using TMPro;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance;
    private static readonly int isLaying = Animator.StringToHash("IsLaying");

    [Header("Settings")]
    public float countdownDuration = 5f;
    public Transform[] spawnPoints;
    public GameObject countdownUI;
    public TMP_Text countdownText;
    public GameObject finishLine;

    [Header("Game State")]
    public bool gameStarted;
    public float startTime;
    private readonly ExitGames.Client.Photon.Hashtable _finishTimes = new(); // Explicitly use ExitGames.Client.Photon.Hashtable

    private void Awake() => Instance = this;

    private void Start()
    {
        if (photonView != null) return;
        gameObject.AddComponent<PhotonView>();
        finishLine.GetComponent<BoxCollider2D>().enabled = false;
        countdownUI.SetActive(false);
    }

    private void Update()
    {
        if (!PhotonNetwork.IsMasterClient || gameStarted) return;
        if (PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            photonView.RPC(nameof(StartCountdown), RpcTarget.All);
        }
    }

    [PunRPC]
    private void StartCountdown()
    {
        gameStarted = true;
        StartCoroutine(CountdownCoroutine());
    }

    private IEnumerator CountdownCoroutine()
    {
        countdownUI.SetActive(true);
        var remainingTime = countdownDuration;

        while (remainingTime > 0)
        {
            countdownText.text = "game starts in: " + Mathf.CeilToInt(remainingTime);
            if (Mathf.CeilToInt(remainingTime) == 2)
            {
                photonView.RPC(nameof(StandUp), RpcTarget.All);
            }
            remainingTime -= Time.deltaTime;
            yield return null;
        }

        countdownUI.SetActive(false);
        photonView.RPC(nameof(StartGame), RpcTarget.All);
    }

    [PunRPC]
    private void StartGame()
    {
        var players = FindObjectsByType<PlayerController>(sortMode: FindObjectsSortMode.None);
        foreach (var p in players)
        {
            var spawnIndex = p.photonView.Owner.ActorNumber % spawnPoints.Length;
            p.Teleport(spawnPoints[spawnIndex].position);
            p.SetMovement(true);
        }
        finishLine.GetComponent<BoxCollider2D>().enabled = true;
        startTime = Time.timeSinceLevelLoad; // Record start time when the game actually starts
    }

    public void PlayerFinished(int playerId, float finishTime)
    {
        Debug.Log($"PlayerFinished called for playerId: {playerId}, finishTime: {finishTime}");
        Debug.Log($"Type of playerId: {playerId.GetType()}, Type of finishTime: {finishTime.GetType()}");
        Debug.Log($"Current finishTimes Keys Type: {_finishTimes.Keys.GetType()}");

        var alreadyFinished = false;
        foreach (var key in _finishTimes.Keys)
        {
            Debug.Log($"Key in finishTimes (before cast): {key}, Type of Key: {key.GetType()}"); // Log key before cast
            try
            {
                var keyInt = (int)key; // Explicit cast here
                Debug.Log($"Key after cast to int: {keyInt}, Type after cast: {keyInt.GetType()}"); // Log key after cast
                if (keyInt != playerId) continue;
                alreadyFinished = true;
                break;
            }
            catch (System.InvalidCastException e)
            {
                Debug.LogError($"InvalidCastException during key cast: {e.Message}");
                Debug.LogError($"Type of key that caused exception: {key.GetType()}");
                alreadyFinished = true; // To prevent further errors in this loop, but ideally we should fix the root cause
                break; // Exit loop after logging error
            }
        }
        if (!alreadyFinished)
        {
            photonView.RPC(nameof(UpdateLeaderboard), RpcTarget.All, playerId, finishTime); // Line 85
        }
    }

    [PunRPC]
    private void UpdateLeaderboard(int playerId, float finishTime)
    {
        Debug.Log($"{PhotonNetwork.CurrentRoom.GetPlayer(playerId).NickName} finished in {finishTime}s");
        Debug.Log($"Leaderboard keys: {_finishTimes.Keys}\nLeaderboard values: {_finishTimes.Values}");
        var playerName = PhotonNetwork.CurrentRoom.GetPlayer(playerId).NickName;

        var playerDataHash = new ExitGames.Client.Photon.Hashtable
        {
            { "playerName", playerName },
            { "finishTime", finishTime }
        };

        _finishTimes.Add(playerId, playerDataHash); // Store nested Hashtable

        if (_finishTimes.Count != PhotonNetwork.CurrentRoom.PlayerCount) return;
        // Prepare leaderboard data to be sent to the Leaderboard scene
        var roomProps = new ExitGames.Client.Photon.Hashtable { { "LeaderboardData", _finishTimes } }; // Explicitly use ExitGames.Client.Photon.Hashtable
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

        PhotonNetwork.LoadLevel("Leaderboard"); // Load the leaderboard scene
    }

    [PunRPC]
    private void StandUp()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            player.animator.SetBool(isLaying, false);
        }
    }
}

[System.Serializable] // Make it serializable so Photon can transfer it (Although not directly used for Photon serialization anymore)
public struct PlayerResultData
{
    public string playerName;
    public float finishTime;

    public PlayerResultData(string name, float time)
    {
        playerName = name;
        finishTime = time;
    }
}