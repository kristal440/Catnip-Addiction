using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Unity.Netcode;
using TMPro; // If you are using TextMeshPro for better text rendering

public class MultiplayerMenuManager : MonoBehaviour
{
    public TMP_InputField roomNameInputField;
    public Button createRoomButton;
    public Transform roomListContent;
    public GameObject roomButtonPrefab;
    public Button backButton;

    void Start()
    {
        createRoomButton.onClick.AddListener(CreateRoom);
        backButton.onClick.AddListener(GoBackToMainMenu);
        // In a real scenario, you'd fetch the list of available rooms from a server.
        // For now, we'll simulate room creation and listing.
    }

    // Simulate adding a room to the list
    public void AddRoomToList(string roomName)
    {
        GameObject roomButtonGO = Instantiate(roomButtonPrefab, roomListContent);
        TextMeshProUGUI buttonText = roomButtonGO.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = roomName;
        }
        Button roomButton = roomButtonGO.GetComponent<Button>();
        if (roomButton != null)
        {
            roomButton.onClick.AddListener(() => JoinRoom(roomName));
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(roomListContent.GetComponent<RectTransform>());
    }

    public void CreateRoom()
    {
        if (!string.IsNullOrEmpty(roomNameInputField.text))
        {
            Debug.Log("Creating room: " + roomNameInputField.text);
            //NetworkManager.Singleton.StartHost(); // Start as host
            //SceneManager.LoadScene("GameScene");
            if (RoomManager.Instance != null) // Check if the instance exists
            {
                RoomManager.Instance.CreateRoomServerRpc(roomNameInputField.text);
            }
            else
            {
                Debug.LogError("RoomManager instance not found!");
            }
            AddRoomToList(roomNameInputField.text); // Simulate adding to list
            roomNameInputField.text = "";
        }
    }

    public void JoinRoom(string roomName)
    {
        Debug.Log("Joining room: " + roomName);
        NetworkManager.Singleton.StartClient();
        SceneManager.LoadScene("GameScene");
    }

    public void GoBackToMainMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
