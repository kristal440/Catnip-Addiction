using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

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
        Debug.Log("OnConnectedToMaster() was called by PUN.");

        if (!_isConnecting) return;
        PhotonNetwork.JoinLobby();
        _isConnecting = false;
        loadingText.text = "Connected to Server :3";
    }

    public override void OnJoinedLobby()
    {
        Debug.Log("OnJoinedLobby() was called by PUN.");
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

        var roomOptions = new RoomOptions
        {
            MaxPlayers = (int)slider.value,
            IsVisible = true,
            IsOpen = true
        };

        SetNickname();
        PhotonNetwork.CreateRoom(roomNameInputField.text, roomOptions);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.Log($"Room creation failed: {message} ({returnCode})");
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
        PhotonNetwork.LoadLevel("GameScene_Map1_Multi");
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log($"Join room failed: {message} ({returnCode})");
    }

    public override void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        _roomLst = roomList.ConvertAll(x => x.Name);
        foreach (Transform trans in roomsContainer)
        {
            Destroy(trans.gameObject);
        }

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
}
