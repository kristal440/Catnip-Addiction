using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <inheritdoc />
/// <summary>
/// Handles returning to the main menu from other game scenes.
/// </summary>
public class HomeButton : MonoBehaviour
{
    // Disconnects from Photon network and loads the main menu scene
    public void OnDisconnectButtonClicked()
    {
        if (PhotonNetwork.InRoom)
            PhotonNetwork.LeaveRoom();
        if (PhotonNetwork.InLobby)
            PhotonNetwork.LeaveLobby();
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.Disconnect();

        SceneManager.LoadScene("MainMenu");
    }
}
