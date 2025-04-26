using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using static UnityEngine.Mathf;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Central manager controlling game flow, player synchronization, and multiplayer race functionality.
/// </summary>
/// <inheritdoc />
public class GameManager : MonoBehaviourPunCallbacks
{
    internal static GameManager Instance { get; private set; }

    private static readonly int IsLaying = Animator.StringToHash("IsLaying");

    [Header("Game State")]
    [SerializeField] [Tooltip("Whether the game is currently running")] public bool gameStarted;
    [SerializeField] [Tooltip("Time when the game started")] public float startTime;

    private bool _countdownStarted;
    private bool _localPlayerFinished;
    private readonly Hashtable _finishTimes = new();
    private Coroutine _countdownCoroutine;

    [Header("Game Settings")]
    [SerializeField] [Tooltip("Duration of the pre-game countdown")] public float countdownDuration = 5f;
    [SerializeField] [Tooltip("Delay before loading the leaderboard scene")] public float leaderboardLoadDelay = 1.5f;
    [SerializeField] [Tooltip("Array of spawn positions for players")] public Transform[] spawnPoints;

    [Header("UI Elements")]
    [SerializeField] [Tooltip("UI element showing the countdown")] public GameObject countdownUI;
    [SerializeField] [Tooltip("Text component displaying countdown values")] public TMP_Text countdownText;
    [SerializeField] [Tooltip("Color to set the countdown text when countdown starts")] public Color countdownColor = new(1f, 0.35f, 0.35f);
    [SerializeField] [Tooltip("Reference to the finish line object")] public GameObject finishLine;
    [SerializeField] [Tooltip("Text component displaying the current game time")] public TMP_Text gameTimerText;

    [Header("In-Game Leaderboard")]
    [SerializeField] [Tooltip("Parent object containing the leaderboard UI")] public GameObject inGameLeaderboardParent;
    [SerializeField] [Tooltip("Transform where leaderboard entries are created")] public Transform inGameLeaderboardContainer;
    [SerializeField] [Tooltip("Prefab used for each leaderboard entry")] public GameObject inGameLeaderboardEntryPrefab;
    private readonly Dictionary<int, GameObject> _leaderboardEntries = new();

    // Sets up the singleton pattern
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    // Initializes game state and UI
    private void Start()
    {
        if (photonView == null)
            gameObject.AddComponent<PhotonView>();

        finishLine.GetComponent<BoxCollider2D>().enabled = false;
        gameTimerText.enabled = false;

        if (inGameLeaderboardParent != null)
            inGameLeaderboardParent.SetActive(false);
    }

    // Cleans up resources when destroyed
    private void OnDestroy()
    {
        if (_countdownCoroutine != null)
            StopCoroutine(_countdownCoroutine);

        ClearInGameLeaderboard();
    }

    // Handles timer updates and countdown initiation
    private void Update()
    {
        if (gameStarted)
            UpdateTimer();

        if (!PhotonNetwork.IsMasterClient || gameStarted || _countdownStarted)
            return;

        if (PhotonNetwork.CurrentRoom == null ||
            PhotonNetwork.CurrentRoom.PlayerCount != PhotonNetwork.CurrentRoom.MaxPlayers) return;

        _countdownStarted = true;
        photonView.RPC(nameof(StartCountdown), RpcTarget.All, PhotonNetwork.ServerTimestamp);
    }

    #region UI Methods
    // Updates the game timer display
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

    // Formats and displays the time on UI
    private void DisplayTime(float timeToDisplay)
    {
        var minutes = FloorToInt(timeToDisplay / 60);
        var seconds = FloorToInt(timeToDisplay % 60);

        gameTimerText.text = $"{minutes:00}:{seconds:00}";
    }

    // Updates the in-game leaderboard with current player positions
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

        if (_finishTimes.Count > 0 && inGameLeaderboardParent && !inGameLeaderboardParent.activeSelf)
            inGameLeaderboardParent.SetActive(true);

        var sortedLeaderboard = leaderboardData.OrderBy(static pair => pair.Value.finishTime).ToList();

        var position = 1;
        foreach (var entry in sortedLeaderboard)
        {
            AddLeaderboardEntry(position, entry.Value.playerName, entry.Value.finishTime, entry.Key);
            position++;
        }
    }

    // Removes all existing leaderboard entries
    private void ClearInGameLeaderboard()
    {
        foreach (var entry in _leaderboardEntries.Values.Where(static entry => entry))
            Destroy(entry);

        _leaderboardEntries.Clear();
    }

    // Adds a single entry to the leaderboard
    private void AddLeaderboardEntry(int position, string playerName, float finishTime, int playerId)
    {
        var entryInstance = Instantiate(inGameLeaderboardEntryPrefab, inGameLeaderboardContainer);
        if (!entryInstance) return;

        _leaderboardEntries[playerId] = entryInstance;

        var texts = entryInstance.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length >= 2)
        {
            texts[0].text = $"{position}. {ShortenName(playerName)}";
            texts[1].text = finishTime.ToString("F2") + "s";
        }

        entryInstance.SetActive(true);
    }

    // Truncates player names that are too long
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
    // Manages the countdown sequence before game start
    private IEnumerator CountdownCoroutine(int serverStartTime)
    {
        countdownUI.SetActive(true);

        var originalTextColor = countdownText.color;

        while (true)
        {
            if (PhotonNetwork.CurrentRoom == null ||
                PhotonNetwork.CurrentRoom.PlayerCount < PhotonNetwork.CurrentRoom.MaxPlayers)
            {
                countdownText.text = "waiting for players...";
                countdownText.color = originalTextColor;
                _countdownStarted = false;

                while (PhotonNetwork.CurrentRoom == null ||
                       PhotonNetwork.CurrentRoom.PlayerCount < PhotonNetwork.CurrentRoom.MaxPlayers)
                    yield return null;

                if (PhotonNetwork.IsMasterClient)
                    yield break;
            }

            var elapsedTime = (PhotonNetwork.ServerTimestamp - serverStartTime) / 1000f;
            var remainingTime = countdownDuration - elapsedTime;

            if (remainingTime <= countdownDuration)
                countdownText.color = countdownColor;

            countdownText.text = "game starts in: " + CeilToInt(remainingTime);

            if (CeilToInt(remainingTime) == 2 && photonView)
                photonView.RPC(nameof(StandUp), RpcTarget.All);

            if (remainingTime <= 0)
                break;

            yield return null;
        }

        countdownText.color = originalTextColor;
        countdownUI.SetActive(false);
        if (photonView)
            photonView.RPC(nameof(StartGame), RpcTarget.All);
    }

    // Handles when a player reaches the finish line
    internal void PlayerFinished(int playerId, float finishTime)
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

    // Handles when a player leaves the room
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);

        if (PhotonNetwork.CurrentRoom.PlayerCount != 1 || !gameStarted) return;

        var localPlayerId = PhotonNetwork.LocalPlayer.ActorNumber;
        if (!_finishTimes.ContainsKey(localPlayerId)) return;
        if (_finishTimes[localPlayerId] is not Hashtable playerData) return;

        if (playerData["finishTime"] is float finishTime)
            UpdateLeaderboard(localPlayerId, finishTime);
    }
    #endregion

    #region RPCs
    // Initiates the countdown across all clients
    [PunRPC]
    private void StartCountdown(int serverStartTime)
    {
        _countdownStarted = true;
        if (_countdownCoroutine != null)
            StopCoroutine(_countdownCoroutine);

        _countdownCoroutine = StartCoroutine(CountdownCoroutine(serverStartTime));
    }

    // Starts the game across all clients
    [PunRPC]
    private void StartGame()
    {
        gameStarted = true;
        var gameProps = new Hashtable { { "gameStarted", true } };
        PhotonNetwork.CurrentRoom.SetCustomProperties(gameProps);

        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
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

    // Updates leaderboard data across all clients
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

        StartCoroutine(LoadLeaderboardWithDelay(leaderboardLoadDelay));
    }

    // Loads leaderboard scene after a delay
    private static IEnumerator LoadLeaderboardWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        PhotonNetwork.LoadLevel("Leaderboard");
    }

    // Makes all players stand up before race start
    [PunRPC]
    private void StandUp()
    {
        var players = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
        foreach (var player in players.Where(static player => player != null && player.animator != null))
            player.animator.SetBool(IsLaying, false);
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
