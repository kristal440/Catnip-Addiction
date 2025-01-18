using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RoomListEntry : MonoBehaviour
{
    public TMP_Text roomNameText;
    public TMP_Text playerCountText;
    public Button joinButton;
    public GameObject roomPrefab;

    public void SetRoomInfo(string roomName, int currentPlayers, int maxPlayers)
    {
        roomNameText.text = roomName;
        playerCountText.text = $"{currentPlayers}/{maxPlayers}";
    }
}
