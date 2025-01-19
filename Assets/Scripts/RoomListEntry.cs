using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoomListEntry : MonoBehaviour
{
    public TMP_Text roomNameText;
    public TMP_Text playerCountText;
    public Button joinButton;

    public void SetRoomInfo(string roomName, int currentPlayers, int maxPlayers)
    {
        roomNameText.text = roomName;
        playerCountText.text = $"{currentPlayers}/{maxPlayers}";
    }
}