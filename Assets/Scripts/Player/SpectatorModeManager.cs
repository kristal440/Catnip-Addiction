using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <inheritdoc />
/// <summary>
/// Manages spectator mode functionality when a player finishes the level, allowing them to watch other active players
/// </summary>
public class SpectatorModeManager : MonoBehaviourPunCallbacks
{
    internal bool IsSpectating { get; private set; }
    private static SpectatorModeManager Instance { get; set; }

    [Header("UI Elements")]
    [SerializeField] [Tooltip("Root UI container for spectator mode interface")] private GameObject spectatorModeUI;
    [SerializeField] [Tooltip("Button to cycle to the previous player")] private Button previousPlayerButton;
    [SerializeField] [Tooltip("Button to cycle to the next player")] private Button nextPlayerButton;
    [SerializeField] [Tooltip("Text displaying the name of the player being spectated")] private TextMeshProUGUI spectatingPlayerText;
    [SerializeField] [Tooltip("Container for on-screen controls that should be hidden during spectating")] private GameObject onScreenControlsParent;

    [Header("Settings")]
    [SerializeField] [Tooltip("Time in seconds before switching to spectator mode after finishing")] private float delayBeforeSpectating = 3f;

    private readonly List<PlayerController> _activePlayers = new();
    private int _currentPlayerIndex = -1;
    private PlayerController _localPlayer;
    private Camera _mainCamera;
    private Coroutine _spectatorCoroutine;
    private int _currentlySpectatedPlayerViewId = -1;
    private readonly Dictionary<int, PlayerController> _playerControllerCache = new();

    /// Initialize singleton and UI elements
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        _mainCamera = Camera.main;
        spectatorModeUI.SetActive(false);

        previousPlayerButton.onClick.AddListener(SpectatorPreviousPlayer);
        nextPlayerButton.onClick.AddListener(SpectatorNextPlayer);
    }

    /// Find local player on start
    private void Start()
    {
        FindLocalPlayer();
        RefreshPlayerList();
    }

    /// Locate the player that belongs to this client
    private void FindLocalPlayer()
    {
        var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var player in allPlayers.Where(static player => player.photonView.IsMine))
        {
            _localPlayer = player;
            break;
        }
    }

    /// Handles player finishing the level by triggering spectator mode
    internal void OnPlayerFinish()
    {
        if (!PhotonNetwork.IsConnected) return;

        if (_localPlayer == null) FindLocalPlayer();
        if (_localPlayer == null) return;

        if (_spectatorCoroutine != null)
            StopCoroutine(_spectatorCoroutine);

        _spectatorCoroutine = StartCoroutine(EnterSpectatorModeAfterDelay());
    }

    /// Waits for specified delay before entering spectator mode
    private IEnumerator EnterSpectatorModeAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeSpectating);

        EnterSpectatorMode();
    }

    /// Activates spectator mode and prepares UI and camera
    private void EnterSpectatorMode()
    {
        RefreshPlayerList();

        if (_activePlayers.Count <= 1) return;

        _localPlayer.SetSpectatorMode(true);
        IsSpectating = true;

        if (onScreenControlsParent)
            onScreenControlsParent.SetActive(false);

        spectatorModeUI.SetActive(true);

        // Set custom properties to track spectator state
        SetSpectatorPropertiesInPhoton(true, -1);

        SpectatorNextPlayer();
    }

    /// Disables spectator mode and resets camera to player
    private void ExitSpectatorMode()
    {
        if (_localPlayer == null) return;

        // Notify the currently spectated player that they're no longer being spectated
        NotifyPlayerBeingSpectated(_currentlySpectatedPlayerViewId, false);
        _currentlySpectatedPlayerViewId = -1;

        // Clear spectator properties
        SetSpectatorPropertiesInPhoton(false, -1);

        spectatorModeUI.SetActive(false);
        _localPlayer.SetSpectatorMode(false);
        IsSpectating = false;

        if (onScreenControlsParent != null)
            onScreenControlsParent.SetActive(true);

        if (_mainCamera == null) return;

        Transform transform1;
        (transform1 = _mainCamera.transform).SetParent(_localPlayer.transform);
        transform1.localPosition = new Vector3(0, 0, -10);
    }

    /// Updates the list of active players
    private void RefreshPlayerList()
    {
        _activePlayers.Clear();
        _playerControllerCache.Clear();

        var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
        {
            _activePlayers.Add(player);
            _playerControllerCache[player.photonView.ViewID] = player;
        }
    }

    /// Switches to the next player in the list for spectating
    private void SpectatorNextPlayer()
    {
        if (_activePlayers.Count <= 1) return;

        // Notify the previously spectated player
        NotifyPlayerBeingSpectated(_currentlySpectatedPlayerViewId, false);

        _currentPlayerIndex = (_currentPlayerIndex + 1) % _activePlayers.Count;

        if (_activePlayers[_currentPlayerIndex] == _localPlayer)
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _activePlayers.Count;

        SwitchToPlayer(_currentPlayerIndex);
    }

    /// Switches to the previous player in the list for spectating
    private void SpectatorPreviousPlayer()
    {
        if (_activePlayers.Count <= 1) return;

        // Notify the previously spectated player
        NotifyPlayerBeingSpectated(_currentlySpectatedPlayerViewId, false);

        _currentPlayerIndex = (_currentPlayerIndex - 1 + _activePlayers.Count) % _activePlayers.Count;

        if (_activePlayers[_currentPlayerIndex] == _localPlayer)
            _currentPlayerIndex = (_currentPlayerIndex - 1 + _activePlayers.Count) % _activePlayers.Count;

        SwitchToPlayer(_currentPlayerIndex);
    }

    /// Attaches camera to the selected player and updates UI
    private void SwitchToPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= _activePlayers.Count) return;
        if (!_mainCamera) return;

        var targetPlayer = _activePlayers[playerIndex];
        _currentlySpectatedPlayerViewId = targetPlayer.photonView.ViewID;

        // Update custom properties to indicate who we're spectating
        SetSpectatorPropertiesInPhoton(true, _currentlySpectatedPlayerViewId);

        // Notify the player that they're being spectated
        NotifyPlayerBeingSpectated(_currentlySpectatedPlayerViewId, true);

        spectatingPlayerText.text = $"Spectating: {targetPlayer.photonView.Owner.NickName}";

        Transform transform1;
        (transform1 = _mainCamera.transform).SetParent(targetPlayer.transform);
        transform1.localPosition = new Vector3(0, 0, -10);
    }

    /// Sets custom properties to track spectator state
    private static void SetSpectatorPropertiesInPhoton(bool isSpectating, int spectatedPlayerId)
    {
        var props = new Hashtable
        {
            { "IsSpectating", isSpectating },
            { "SpectatingPlayer", spectatedPlayerId }
        };

        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    /// Notifies a player that they are being spectated or not
    private void NotifyPlayerBeingSpectated(int playerViewId, bool isBeingSpectated)
    {
        if (playerViewId == -1) return;

        // Make sure our cache is up to date
        if (!_playerControllerCache.ContainsKey(playerViewId))
        {
            RefreshPlayerList();
            if (!_playerControllerCache.ContainsKey(playerViewId)) return;
        }

        var controller = _playerControllerCache[playerViewId];

        // Set spectated state
        controller.SetSpectatedState();

        // If starting to spectate, request initial jump charge state
        if (isBeingSpectated)
            controller.OnStartSpectating();
    }

    /// Returns the currently spectated player's view ID
    internal int GetCurrentlySpectatedPlayerViewId()
    {
        return _currentlySpectatedPlayerViewId;
    }

    /// Checks if a specific player is being spectated by anyone
    internal static bool IsPlayerBeingSpectated(int playerViewId)
    {
        // Check if any player in the room is spectating this player
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (!player.CustomProperties.TryGetValue("IsSpectating", out var isSpectating) ||
                !(bool)isSpectating) continue;

            if (player.CustomProperties.TryGetValue("SpectatingPlayer", out var spectatedPlayerId) &&
                (int)spectatedPlayerId == playerViewId)
                return true;
        }

        return false;
    }

    /// Provides access to the singleton instance
    internal static SpectatorModeManager GetInstance()
    {
        return Instance;
    }

    /// <inheritdoc />
    /// Called when player properties are updated
    public override void OnPlayerPropertiesUpdate(Player targetPlayer, Hashtable changedProps)
    {
        base.OnPlayerPropertiesUpdate(targetPlayer, changedProps);

        // If a player's spectator properties change, refresh our player list
        if (changedProps.ContainsKey("IsSpectating") || changedProps.ContainsKey("SpectatingPlayer"))
            RefreshPlayerList();
    }

    /// <inheritdoc />
    /// Called when a player leaves the room
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        base.OnPlayerLeftRoom(otherPlayer);

        // Refresh our player list
        RefreshPlayerList();

        // If we were spectating this player, find a new one
        if (_currentlySpectatedPlayerViewId == -1) return;

        var foundPlayer = _playerControllerCache.Values.Any(controller => controller.photonView.ViewID == _currentlySpectatedPlayerViewId);

        if (!foundPlayer && IsSpectating)
            SpectatorNextPlayer();
    }
}
