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
    #region Variables
    private bool _isConnecting;
    private List<string> _roomLst;

    private readonly Dictionary<string, string> _availableMaps = new(); // Scene name -> Display name
    private string _selectedMapName = "GameScene_Map1_Multi"; // Default map

    [Header("Error popup")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorText;

    [Header("Loading Panel")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private TMP_Text loadingText;

    [Header("Main UI")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private TMP_InputField playerNameInputField;
    [SerializeField] private TMP_InputField roomNameInputField;
    [SerializeField] private Slider slider;

    [Header("Map Selection")]
    [SerializeField] private GameObject mapListPanel;
    [SerializeField] private GameObject mapListParent;
    [SerializeField] private Transform mapsContainer;
    [SerializeField] private ScrollListSelectionHandler mapSelectionHandler;

    [Header("Room List")]
    [SerializeField] private GameObject roomListPanel;
    [SerializeField] private GameObject roomPrefab;
    [SerializeField] private Transform roomsContainer;
    #endregion

    #region Unity Lifecycle
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
        if (mapSelectionHandler != null)
            mapSelectionHandler.OnItemSelected += OnMapSelected;
    }

    private void OnDestroy()
    {
        if (mapSelectionHandler != null)
            mapSelectionHandler.OnItemSelected -= OnMapSelected;
    }
    #endregion

    #region Photon Callbacks
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

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError($"Room creation failed: {message} ({returnCode})");
    }

    public override void OnJoinedRoom()
    {
        SetNickname();

        var mapToLoad = _selectedMapName;
        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("map", out var mapName))
            mapToLoad = (string)mapName;

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
            playerCountText.color = roomInfo.PlayerCount >= roomInfo.MaxPlayers ?
                new Color(1f, 0.239f, 0.239f) : new Color(0.475f, 1f, 0.498f);

            roomObject.GetComponent<Button>().onClick.AddListener(() => JoinRoom(roomInfo.Name));
        }
    }
    #endregion

    #region UI Methods
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

    private void SetNickname()
    {
        PhotonNetwork.NickName = playerNameInputField.text;
    }
    #endregion

    #region Map Selection
    public void InitializeMaps()
    {
        mapListPanel.SetActive(true);
        _availableMaps.Clear();

        foreach (Transform child in mapListParent.transform)
        {
            if (!child.gameObject.activeSelf) continue;
            var sceneName = child.gameObject.name;

            var displayName = sceneName;
            var displayText = child.GetComponentInChildren<TextMeshProUGUI>();
            if (displayText)
                displayName = displayText.text;

            _availableMaps.Add(sceneName, displayName);
            Debug.Log($"Added map: {sceneName} -> {displayName}");
        }

        if (!_availableMaps.ContainsKey(_selectedMapName) && _availableMaps.Count > 0)
            _selectedMapName = _availableMaps.Keys.GetEnumerator().Current;

        CreateMapSelectionButtons();
    }

    private void OnMapSelected(int index)
    {
        if (index < 0 || index >= _availableMaps.Count) return;
        _selectedMapName = _availableMaps.Keys.ElementAt(index);
        Debug.Log($"Selected map from handler: {_selectedMapName}");
    }

    private void CreateMapSelectionButtons()
    {
        var existingButtons = (from Transform child in mapsContainer select child.gameObject).ToList();
        var buttonIndex = 0;

        foreach (var (mapSceneName, value) in _availableMaps)
        {
            if (buttonIndex >= existingButtons.Count)
                break;

            var mapButton = existingButtons[buttonIndex];
            mapButton.SetActive(true);

            var mapNameText = mapButton.transform.Find("MapNameTxt").GetComponent<TextMeshProUGUI>();
            if (mapNameText)
                mapNameText.text = value;

            // Add or get the MapButtonData component and set the map name
            var mapData = mapButton.GetComponent<MapButtonData>() ??
                          mapButton.AddComponent<MapButtonData>();
            mapData.MapName = mapSceneName;

            var button = mapButton.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectMap(mapSceneName));

            if (mapSceneName == _selectedMapName)
                button.Select();

            buttonIndex++;
        }

        for (var i = buttonIndex; i < existingButtons.Count; i++)
            existingButtons[i].SetActive(false);
    }

    private void SelectMap(string mapName)
    {
        _selectedMapName = mapName;
        Debug.Log($"Selected map: {_selectedMapName}");

        foreach (Transform child in mapsContainer)
        {
            var button = child.GetComponent<Button>();
            if (!button) continue;

            var mapData = child.GetComponent<MapButtonData>();
            if (mapData && mapData.MapName == mapName)
                button.Select();
        }
    }

    // Helper component to store map data on buttons
    public class MapButtonData : MonoBehaviour
    {
        public string MapName { get; set; }
    }
    #endregion
}
