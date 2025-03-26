using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using static UnityEngine.Mathf;
using Hashtable = ExitGames.Client.Photon.Hashtable;

public class GameManager : MonoBehaviourPunCallbacks
{
    public static GameManager Instance { get; private set; }
    private static readonly int isLaying = Animator.StringToHash("IsLaying");

    [Header("Game State")]
    public bool gameStarted;
    private readonly Hashtable _finishTimes = new();
    private bool _localPlayerFinished;
    public float startTime;
    private bool _countdownStarted;

    [Header("Game Settings")]
    public float countdownDuration = 5f;
    public GameObject countdownUI;
    public TMP_Text countdownText;
    public GameObject finishLine;
    public Transform[] spawnPoints;

    [Header("UI")]
    public TMP_Text gameTimerText;

    [Header("In-Game Leaderboard")]
    public GameObject inGameLeaderboardParent;
    public Transform inGameLeaderboardContainer;
    public GameObject inGameLeaderboardEntryPrefab;
    private readonly Dictionary<int, GameObject> _leaderboardEntries = new();

    private Coroutine _countdownCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (photonView == null)
            gameObject.AddComponent<PhotonView>();

        finishLine.GetComponent<BoxCollider2D>().enabled = false;
        gameTimerText.enabled = false;
        countdownUI.SetActive(false);

        if (inGameLeaderboardParent != null)
            inGameLeaderboardParent.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_countdownCoroutine != null)
            StopCoroutine(_countdownCoroutine);

        ClearInGameLeaderboard();
    }

    private void Update()
    {
        if (gameStarted)
            UpdateTimer();

        if (!PhotonNetwork.IsMasterClient || gameStarted || _countdownStarted)
            return;

        if (PhotonNetwork.CurrentRoom == null ||
            PhotonNetwork.CurrentRoom.PlayerCount != PhotonNetwork.CurrentRoom.MaxPlayers) return;
        _countdownStarted = true;
        photonView.RPC(nameof(StartCountdown), RpcTarget.All);
    }

    #region UI Methods
    private void UpdateTimer()
    {
        if (!gameStarted)
            return;

        if (_localPlayerFinished)
            return;

        if (Time.timeSinceLevelLoad <= startTime)
            return;

        var elapsedTime = Time.timeSinceLevelLoad - startTime;
        DisplayTime(elapsedTime);
    }

    private void DisplayTime(float timeToDisplay)
    {
        var minutes = FloorToInt(timeToDisplay / 60);
        var seconds = FloorToInt(timeToDisplay % 60);

        gameTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdateInGameLeaderboard()
    {
        ClearInGameLeaderboard();

        var leaderboardData = new Dictionary<int, PlayerResultData>();

        foreach (var entry in _finishTimes)
        {
            if (entry.Key is not int playerID) continue;
            if (entry.Value is not Hashtable playerDataHash) continue;

            var playerName = playerDataHash["playerName"] as string ?? "Unknown";
            var finishTime = playerDataHash["finishTime"] is float f ? f : 0f;

            leaderboardData.Add(playerID, new PlayerResultData(playerName, finishTime));
        }

        if (_finishTimes.Count > 0 && inGameLeaderboardParent != null && !inGameLeaderboardParent.activeSelf)
            inGameLeaderboardParent.SetActive(true);

        var sortedLeaderboard = leaderboardData.OrderBy(pair => pair.Value.finishTime).ToList();

        var position = 1;
        foreach (var entry in sortedLeaderboard)
        {
            AddLeaderboardEntry(position, entry.Value.playerName, entry.Value.finishTime, entry.Key);
            position++;
        }
    }

    private void ClearInGameLeaderboard()
    {
        foreach (var entry in _leaderboardEntries.Values.Where(entry => entry != null))
        {
            Destroy(entry);
        }

        _leaderboardEntries.Clear();
    }

    private void AddLeaderboardEntry(int position, string playerName, float finishTime, int playerId)
    {
        var entryInstance = Instantiate(inGameLeaderboardEntryPrefab, inGameLeaderboardContainer);
        if (entryInstance == null) return;

        _leaderboardEntries[playerId] = entryInstance;

        var texts = entryInstance.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length >= 2)
        {
            texts[0].text = $"{position}. {ShortenName(playerName)}";
            texts[1].text = finishTime.ToString("F2") + "s";
        }

        entryInstance.SetActive(true);
    }

    private static string ShortenName(string playerName)
    {
        if (string.IsNullOrEmpty(playerName))
            return "Unknown";

        if (playerName.Length <= 10)
            return playerName;

        return playerName[..9] + "..";
    }
    #endregion

    #region Game State Management
    public void PlayerFinished(int playerId, float finishTime)
    {
        if (playerId == PhotonNetwork.LocalPlayer.ActorNumber)
        {
            _localPlayerFinished = true;
            DisplayTime(finishTime);

            if (GetComponent<SpectatorModeManager>() != null)
                GetComponent<SpectatorModeManager>().OnPlayerFinish();
        }

        if (_finishTimes.ContainsKey(playerId))
            return;

        photonView.RPC(nameof(UpdateLeaderboard), RpcTarget.All, playerId, finishTime);
    }

    private IEnumerator CountdownCoroutine()
    {
        countdownUI.SetActive(true);
        var remainingTime = countdownDuration;

        while (remainingTime > 0)
        {
            countdownText.text = "game starts in: " + CeilToInt(remainingTime);
            if (CeilToInt(remainingTime) == 2 && photonView)
                photonView.RPC(nameof(StandUp), RpcTarget.All);

            remainingTime -= Time.deltaTime;
            yield return null;
        }

        countdownUI.SetActive(false);
        if (photonView)
            photonView.RPC(nameof(StartGame), RpcTarget.All);
    }
    #endregion

    #region RPCs
    [PunRPC]
    private void StartCountdown()
    {
        _countdownStarted = true;
        if (_countdownCoroutine != null)
            StopCoroutine(_countdownCoroutine);

        _countdownCoroutine = StartCoroutine(CountdownCoroutine());
    }

    [PunRPC]
    private void StartGame()
    {
        gameStarted = true;

        var players = FindObjectsByType<PlayerController>(sortMode: FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p == null || p.photonView == null || p.photonView.Owner == null) continue;

            var spawnIndex = p.photonView.Owner.ActorNumber % spawnPoints.Length;
            if (spawnPoints.Length > 0 && spawnIndex < spawnPoints.Length)
                p.Teleport(spawnPoints[spawnIndex].position);

            p.SetMovement(true);
        }

        finishLine.GetComponent<BoxCollider2D>().enabled = true;
        gameTimerText.enabled = true;
        startTime = Time.timeSinceLevelLoad;
    }

    [PunRPC]
    private void UpdateLeaderboard(int playerId, float finishTime)
    {
        if (PhotonNetwork.CurrentRoom == null) return;

        var player = PhotonNetwork.CurrentRoom.GetPlayer(playerId);
        if (player == null) return;

        var playerName = player.NickName;

        var playerDataHash = new Hashtable
        {
            { "playerName", playerName },
            { "finishTime", finishTime }
        };

        _finishTimes[playerId] = playerDataHash;

        UpdateInGameLeaderboard();

        if (_finishTimes.Count != PhotonNetwork.CurrentRoom.PlayerCount || !PhotonNetwork.IsMasterClient) return;
        var roomProps = new Hashtable { { "LeaderboardData", _finishTimes } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(roomProps);

        StartCoroutine(LoadLeaderboardWithDelay(0.5f));
    }

    private static IEnumerator LoadLeaderboardWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        PhotonNetwork.LoadLevel("Leaderboard");
    }

    [PunRPC]
    private void StandUp()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players)
        {
            if (player != null && player.animator != null)
                player.animator.SetBool(isLaying, false);
        }
    }
    #endregion
}

[Serializable]
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
