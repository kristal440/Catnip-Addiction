using Photon.Pun;
using Photon.Realtime;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviourPunCallbacks
{
    public GameObject pauseMenuUI;
    private PlayerController _playerController;

    private void Start()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.LocalPlayer != null)
        {
            var playerControllers = FindObjectsByType<PlayerController>(sortMode: FindObjectsSortMode.None);
            _playerController = playerControllers.FirstOrDefault(controller => controller.photonView.IsMine);
        }
        pauseMenuUI.SetActive(false);
    }

    public void PauseGame()
    {
        pauseMenuUI.SetActive(true);
        if (_playerController)
            _playerController.IsPaused = true;
    }

    public void ResumeGame()
    {
        pauseMenuUI.SetActive(false);
        if (_playerController)
            _playerController.IsPaused = false;
    }

    public void LeaveGame()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.LeaveRoom();
        else
            SceneManager.LoadScene("MainMenu");
    }

    public override void OnLeftRoom()
    {
        PhotonNetwork.Disconnect();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        SceneManager.LoadScene("MainMenu");
    }
}
