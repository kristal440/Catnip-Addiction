using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SpectatorModeManager : MonoBehaviourPunCallbacks
{
    internal bool IsSpectating { get; private set; }
    private static SpectatorModeManager Instance { get; set; }

    [Header("UI Elements")]
    [SerializeField] private GameObject spectatorModeUI;
    [SerializeField] private Button previousPlayerButton;
    [SerializeField] private Button nextPlayerButton;
    [SerializeField] private TextMeshProUGUI spectatingPlayerText;
    [SerializeField] private GameObject onScreenControlsParent;

    [Header("Settings")]
    [SerializeField] private float delayBeforeSpectating = 3f;

    private readonly List<PlayerController> _activePlayers = new();
    private int _currentPlayerIndex = -1;
    private PlayerController _localPlayer;
    private Camera _mainCamera;
    private Coroutine _spectatorCoroutine;

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

    private void Start()
    {
        FindLocalPlayer();
    }

    private void FindLocalPlayer()
    {
        var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var player in allPlayers.Where(static player => player.photonView.IsMine))
        {
            _localPlayer = player;
            break;
        }
    }

    internal void OnPlayerFinish()
    {
        if (!PhotonNetwork.IsConnected) return;

        if (_localPlayer == null) FindLocalPlayer();
        if (_localPlayer == null) return;

        if (_spectatorCoroutine != null)
            StopCoroutine(_spectatorCoroutine);

        _spectatorCoroutine = StartCoroutine(EnterSpectatorModeAfterDelay());
    }

    private IEnumerator EnterSpectatorModeAfterDelay()
    {
        yield return new WaitForSeconds(delayBeforeSpectating);

        EnterSpectatorMode();
    }

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

    private void RefreshPlayerList()
    {
        _activePlayers.Clear();

        var allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);

        foreach (var player in allPlayers)
            _activePlayers.Add(player);
    }

    private void SpectatorNextPlayer()
    {
        if (_activePlayers.Count <= 1) return;

        _currentPlayerIndex = (_currentPlayerIndex + 1) % _activePlayers.Count;

        if (_activePlayers[_currentPlayerIndex] == _localPlayer)
            _currentPlayerIndex = (_currentPlayerIndex + 1) % _activePlayers.Count;

        SwitchToPlayer(_currentPlayerIndex);
    }

    private void SpectatorPreviousPlayer()
    {
        if (_activePlayers.Count <= 1) return;

        _currentPlayerIndex = (_currentPlayerIndex - 1 + _activePlayers.Count) % _activePlayers.Count;

        if (_activePlayers[_currentPlayerIndex] == _localPlayer)
            _currentPlayerIndex = (_currentPlayerIndex - 1 + _activePlayers.Count) % _activePlayers.Count;

        SwitchToPlayer(_currentPlayerIndex);
    }

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
