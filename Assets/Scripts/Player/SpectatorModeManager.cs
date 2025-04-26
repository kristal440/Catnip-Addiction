using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        SpectatorNextPlayer();
    }

    /// Disables spectator mode and resets camera to player
    private void ExitSpectatorMode()
    {
        if (_localPlayer == null) return;

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

        var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
            _activePlayers.Add(player);
    }

    /// Switches to the next player in the list for spectating
    private void SpectatorNextPlayer()
    {
        if (_activePlayers.Count <= 1) return;

        _currentPlayerIndex = (_currentPlayerIndex + 1) % _activePlayers.Count;

        if (_activePlayers[_currentPlayerIndex] == _localPlayer)
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _activePlayers.Count;

        SwitchToPlayer(_currentPlayerIndex);
    }

    /// Switches to the previous player in the list for spectating
    private void SpectatorPreviousPlayer()
    {
        if (_activePlayers.Count <= 1) return;

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

        spectatingPlayerText.text = $"Spectating: {targetPlayer.photonView.Owner.NickName}";

        Transform transform1;
        (transform1 = _mainCamera.transform).SetParent(targetPlayer.transform);
        transform1.localPosition = new Vector3(0, 0, -10);
    }
}
