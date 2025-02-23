using UnityEngine;
using UnityEngine.SceneManagement;
using Photon.Pun;

public class HomeButton : MonoBehaviour
{
    public void OnDisconnectButtonClicked()
    {
        // First disconnect from Photon
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        if (PhotonNetwork.InLobby)
            PhotonNetwork.LeaveLobby();
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();
        // Load the main menu scene
        SceneManager.LoadScene("MainMenu");
    }
}