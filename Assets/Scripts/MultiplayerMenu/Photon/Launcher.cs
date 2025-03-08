using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Launcher : MonoBehaviourPunCallbacks
{
    [Header("Main UI")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private Slider slider;

    [Header("Loading Panel")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TMP_Text loadingText;

    [Header("Room List")]
    [SerializeField] private GameObject roomListPanel;
    [SerializeField] private GameObject roomPrefab;
    [SerializeField] private Transform roomsContainer;

    [Header("Error popup")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;

    [Header("Map Selection")]
    [Tooltip("UI panel that contains the entire map selection interface")]
    [SerializeField] private GameObject mapListPanel;
    [Tooltip("Transform parent where map selection buttons will be instantiated")]
    [SerializeField] private Transform mapsContainer;
    [Tooltip("Parent GameObject containing child objects that represent available maps - each active child will become a selectable map")]
    [SerializeField] private GameObject mapListParent;

    private string _selectedMapName = "GameScene_Map1_Multi"; // Default map
    private readonly Dictionary<string, string> _availableMaps = new(); // Scene name -> Display name

    private bool _isConnecting;
    private List<string> _roomLst;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

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
        PhotonNetwork.GameVersion = "1";
        loadingText.text = "Connecting to Server...";
        PhotonNetwork.ConnectUsingSettings();
    }

    public override void OnConnectedToMaster()
    {
        if (!_isConnecting) return;
        PhotonNetwork.JoinLobby();
        _isConnecting = false;
        loadingText.text = "Connected to Server :3";
    }

    public override void OnJoinedLobby()
    {
        StartCoroutine(ShowRoomListWithDelay());
    }

    private IEnumerator ShowRoomListWithDelay()
    {
        yield return new WaitForSeconds(1);
        loadingPanel.SetActive(false);
        mainPanel.SetActive(true);
        roomListPanel.SetActive(true);
    }

    public void CreateRoom()
    {
        if (string.IsNullOrEmpty(roomNameInputField.text))
        {
            errorText.text = "Room name can't be empty";
            errorPanel.SetActive(true);
            return;
        }

        if (_roomLst.Contains(roomNameInputField.text))
        {
            errorText.text = "Room with this name already exists";
            errorPanel.SetActive(true);
            return;
        }

        // Store map in custom room properties
        var customRoomProperties = new ExitGames.Client.Photon.Hashtable
        {
            { "map", _selectedMapName }
        };

        var roomOptions = new RoomOptions
        {
            MaxPlayers = (int)slider.value,
            IsVisible = true,
            IsOpen = true,
            CustomRoomProperties = customRoomProperties,
            CustomRoomPropertiesForLobby = new[] { "map" }
        };

        SetNickname();
        PhotonNetwork.CreateRoom(roomNameInputField.text, roomOptions);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Room creation failed: {message} ({returnCode})");
    }

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

    public override void OnJoinedRoom()
    {
        SetNickname();

        // Get map from room properties
        var mapToLoad = _selectedMapName;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("map", out var mapName))
        {
            mapToLoad = (string)mapName;
        }

        PhotonNetwork.LoadLevel(mapToLoad);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Join room failed: {message} ({returnCode})");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        _roomLst = roomList.ConvertAll(x => x.Name);
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
            playerCountText.color = roomInfo.PlayerCount >= roomInfo.MaxPlayers ? new Color(1f, 0.239f, 0.239f) : new Color(0.475f, 1f, 0.498f);

            roomObject.GetComponent<Button>().onClick.AddListener(() => JoinRoom(roomInfo.Name));
        }
    }

    private void SetNickname()
    {
        PhotonNetwork.NickName = playerNameInputField.text;
    }

    public void InitializeMaps()
    {
        mapListPanel.SetActive(true);
        _availableMaps.Clear();

        // Find all enabled child objects of the map list parent
        foreach (Transform child in mapListParent.transform)
        {
            if (!child.gameObject.activeSelf) continue;
            var sceneName = child.gameObject.name;

            // Get display name from TextMeshProUGUI component if it exists,
            // otherwise use the scene name as display name
            var displayName = sceneName;
            var displayText = child.GetComponentInChildren<TextMeshProUGUI>();
            if (displayText)
            {
                displayName = displayText.text;
            }

            _availableMaps.Add(sceneName, displayName);
            Debug.Log($"Added map: {sceneName} -> {displayName}");
        }

        // Set default selected map (first one in the list)
        if (!_availableMaps.ContainsKey(_selectedMapName) && _availableMaps.Count > 0)
        {
            _selectedMapName = _availableMaps.Keys.GetEnumerator().Current;
        }

        // Populate the UI
        CreateMapSelectionButtons();
    }

    private void CreateMapSelectionButtons()
    {
        // Get all existing map button objects in the container
        var existingButtons = (from Transform child in mapsContainer select child.gameObject).ToList();

        var buttonIndex = 0;

        // Configure each button for the available maps
        foreach (var (mapSceneName, value) in _availableMaps)
        {
            // If we've run out of buttons, stop
            if (buttonIndex >= existingButtons.Count)
                break;

            var mapButton = existingButtons[buttonIndex];
            mapButton.SetActive(true);

            // Set map name text
            var mapNameText = mapButton.transform.Find("MapNameTxt").GetComponent<TextMeshProUGUI>();
            if (mapNameText)
                mapNameText.text = value;

            // Add click listener - first remove any existing listeners
            var button = mapButton.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectMap(mapSceneName));

            // Reset button color
            button.GetComponent<Image>().color = Color.white;

            // Mark default selected map
            if (mapSceneName == _selectedMapName)
            {
                // Highlight the selected map button
                HighlightSelectedMapButton(button);
            }

            buttonIndex++;
        }

        // Disable any unused buttons
        for (var i = buttonIndex; i < existingButtons.Count; i++)
        {
            existingButtons[i].SetActive(false);
        }
    }

    private void SelectMap(string mapName)
    {
        _selectedMapName = mapName;
        Debug.Log($"Selected map: {_selectedMapName}");

        // Update UI to highlight selected map
        foreach (Transform child in mapsContainer)
        {
            var button = child.GetComponent<Button>();
            if (!button) continue;

            // Reset all buttons
            button.GetComponent<Image>().color = Color.white;

            // Check if this is the selected button using our helper component
            var mapData = child.GetComponent<MapButtonData>();
            if (mapData && mapData.MapName == mapName)
            {
                HighlightSelectedMapButton(button);
            }
        }
    }

    private static void HighlightSelectedMapButton(Button button)
    {
        // Visual indication of selection
        button.GetComponent<Image>().color = new Color(0.7f, 1f, 0.7f);
    }

    // Helper component to store map data on buttons
    private class MapButtonData : MonoBehaviour
    {
        public string MapName { get; set; }
    }
}
