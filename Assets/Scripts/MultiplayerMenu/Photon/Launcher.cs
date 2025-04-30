using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <inheritdoc />
/// <summary>
/// Manages multiplayer connection, room creation/joining, and map selection through the Photon networking system.
/// </summary>
public class Launcher : MonoBehaviourPunCallbacks
{
    private bool _isConnecting;
    private List<string> _roomLst;

    [Header("Error popup")]
    [SerializeField] [Tooltip("Panel displayed when errors occur")] private GameObject errorPanel;
    [SerializeField] [Tooltip("Text component that displays error messages")] private TextMeshProUGUI errorText;

    [Header("Loading Panel")]
    [SerializeField] [Tooltip("Panel shown during connection process")] private GameObject loadingPanel;
    [SerializeField] [Tooltip("Text showing connection status")] private TMP_Text loadingText;

    [Header("Main UI")]
    [SerializeField] [Tooltip("Main panel containing room creation interface")] private GameObject mainPanel;
    [SerializeField] [Tooltip("Input field for player name")] private TMP_InputField playerNameInputField;
    [SerializeField] [Tooltip("Input field for room name")] private TMP_InputField roomNameInputField;
    [SerializeField] [Tooltip("Slider controlling maximum player count")] private Slider slider;

    [Header("Map Selection")]
    [SerializeField] [Tooltip("Name of the default map")] private string selectedMapName = "GameScene_Map1_Multi";
    [SerializeField] [Tooltip("Panel for selecting maps")] private GameObject mapListPanel;
    [SerializeField] [Tooltip("Manager handling map selection functionality")] private MapSelectionManager mapSelectionManager;
    [SerializeField] [Tooltip("UI elements to hide when map selector is open")] private List<GameObject> objectsToDisableWhenMapSelectorOpen = new();

    [Header("Room List")]
    [SerializeField] [Tooltip("Panel showing available rooms")] private GameObject roomListPanel;
    [SerializeField] [Tooltip("Prefab for room list entries")] private GameObject roomPrefab;
    [SerializeField] [Tooltip("Container for instantiated room list items")] private Transform roomsContainer;
    [SerializeField] [Tooltip("Color for full rooms")] private Color fullRoomColor = new(1f, 0.239f, 0.239f);
    [SerializeField] [Tooltip("Color for available rooms")] private Color availableRoomColor = new(0.475f, 1f, 0.498f);

    /// Configures automatic scene synchronization
    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    /// Initializes UI state and begins connection to Photon network
    private void Start()
    {
        roomListPanel.SetActive(false);
        mainPanel.SetActive(false);
        loadingPanel.SetActive(true);

        if (PhotonNetwork.IsConnected)
        {
            OnJoinedLobby();
            return;
        }

        _isConnecting = true;
        loadingText.text = "Connecting to Server...";
        PhotonNetwork.ConnectUsingSettings();
        if (mapSelectionManager != null)
            mapSelectionManager.OnMapSelected += OnMapSelected;
    }

    /// Removes event listeners when destroyed
    private void OnDestroy()
    {
        if (mapSelectionManager != null)
            mapSelectionManager.OnMapSelected -= OnMapSelected;
    }

    /// <inheritdoc />
    /// Called when successfully connected to Photon master server
    public override void OnConnectedToMaster()
    {
        if (!_isConnecting) return;

        try
        {
            PhotonNetwork.JoinLobby();
        }
        catch
        {
            SceneManager.LoadScene("MainMenu");
        }

        _isConnecting = false;
        loadingText.text = "Connected to Server :3";
    }

    /// <inheritdoc />
    /// Called when successfully joined the Photon lobby
    public override void OnJoinedLobby()
    {
        StartCoroutine(ShowRoomListWithDelay());
    }

    /// <inheritdoc />
    /// Called when room creation fails
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        errorText.text = $"Room creation failed ({returnCode}): {message}";
        errorPanel.SetActive(true);
    }

    /// <inheritdoc />
    /// Called when successfully joined a room, loads the selected map
    public override void OnJoinedRoom()
    {
        SetNickname();

        var mapToLoad = selectedMapName;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("map", out var mapName))
            mapToLoad = (string)mapName;

        PhotonNetwork.LoadLevel(mapToLoad);
    }

    /// <inheritdoc />
    /// Called when joining a room fails
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Join room failed: {message} ({returnCode})");
    }

    /// <inheritdoc />
    /// Updates the UI list of available rooms when the list changes
    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        _roomLst = roomList.ConvertAll(static x => x.Name);
        foreach (Transform transform1 in roomsContainer)
            Destroy(transform1.gameObject);

        foreach (var roomInfo in roomList)
        {
            if (roomInfo.RemovedFromList) continue;

            var roomObject = Instantiate(roomPrefab, roomsContainer);

            var roomNameText = roomObject.transform.Find("RoomNameTxt").GetComponent<TextMeshProUGUI>();
            roomNameText.text = roomInfo.Name;

            var playerCountText = roomObject.transform.Find("playerCountGroup/RoomPlayerCountTxt").GetComponent<TextMeshProUGUI>();
            playerCountText.text = $"{roomInfo.PlayerCount}/{roomInfo.MaxPlayers}";
            playerCountText.color = roomInfo.PlayerCount >= roomInfo.MaxPlayers ?
                fullRoomColor : availableRoomColor;

            var isGameInProgress = false;
            if (roomInfo.CustomProperties.TryGetValue("gameStarted", out var gameStarted))
                isGameInProgress = (bool)gameStarted;

            var button = roomObject.GetComponent<Button>();
            button.interactable = roomInfo.PlayerCount < roomInfo.MaxPlayers && !isGameInProgress;
            button.onClick.AddListener(() => JoinRoom(roomInfo.Name));
        }
    }

    /// Shows the room list UI after a short delay
    private IEnumerator ShowRoomListWithDelay()
    {
        yield return new WaitForSeconds(1);

        loadingPanel.SetActive(false);
        mainPanel.SetActive(true);
        roomListPanel.SetActive(true);
    }

    /// Creates a new multiplayer room after validating inputs
    public void CreateRoom()
    {
        if (playerNameInputField.text.Length < 3)
        {
            errorText.text = "Player name must be at least 3 characters long!";
            errorPanel.SetActive(true);
            return;
        }

        if (string.IsNullOrEmpty(roomNameInputField.text))
        {
            errorText.text = "Room name can't be empty";
            errorPanel.SetActive(true);
            return;
        }

        if (!IsRoomNameValid(roomNameInputField.text))
        {
            errorText.text = "Room name contains invalid characters or is too long";
            errorPanel.SetActive(true);
            return;
        }

        if (_roomLst.Contains(roomNameInputField.text))
        {
            errorText.text = "Room with this name already exists";
            errorPanel.SetActive(true);
            return;
        }

        var customRoomProperties = new Hashtable
        {
            { "map", selectedMapName },
            { "gameStarted", false }
        };

        var roomOptions = new RoomOptions
        {
            MaxPlayers = (int)slider.value,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = customRoomProperties,
            CustomRoomPropertiesForLobby = new[] { "map", "gameStarted" }
        };

        SetNickname();
        PhotonNetwork.CreateRoom(roomNameInputField.text, roomOptions);
    }

    /// Validates if a room name meets requirements
    private static bool IsRoomNameValid(string roomName)
    {
        return roomName.Length is >= 3 and <= 20 &&
               roomName.All(static c => char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ');
    }

    /// Joins an existing room after validating player name
    private void JoinRoom(string roomName)
    {
        if (playerNameInputField.text.Length < 3)
        {
            errorText.text = "Player name must be at least 3 characters long!";
            errorPanel.SetActive(true);
            return;
        }
        SetNickname();
        PhotonNetwork.JoinRoom(roomName);
    }

    /// Sets the player nickname based on input field
    private void SetNickname()
    {
        PhotonNetwork.NickName = playerNameInputField.text;
    }

    /// Opens the map selection interface
    public void InitializeMaps()
    {
        DisableObjectsDuringMapSelection();
        mapListPanel.SetActive(true);
        mapSelectionManager.Initialize(selectedMapName);
    }

    /// Handles map selection event from the map selection manager
    private void OnMapSelected(string mapSceneName, string mapDisplayName)
    {
        selectedMapName = mapSceneName;
        Debug.Log($"Selected map: {mapSceneName} ({mapDisplayName})");
    }

    /// Disables certain UI elements during map selection
    private void DisableObjectsDuringMapSelection()
    {
        foreach (var obj in objectsToDisableWhenMapSelectorOpen.Where(static obj => obj != null))
            obj.SetActive(false);
    }

    /// Re-enables UI elements after map selection
    public void EnableObjectsAfterMapSelection()
    {
        foreach (var obj in objectsToDisableWhenMapSelectorOpen.Where(static obj => obj != null))
            obj.SetActive(true);
    }
}
