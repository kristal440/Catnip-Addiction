using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI;
    private PlayerController _playerController;

    private void Start()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.LocalPlayer != null)
        {
            var playerControllers = FindObjectsByType<PlayerController>(sortMode: FindObjectsSortMode.None);
            foreach (var controller in playerControllers)
            {
                if (!controller.photonView.IsMine) continue;
                _playerController = controller;
                break;
            }
        }
        pauseMenuUI.SetActive(false);
    }

    public void PauseGame()
    {
        pauseMenuUI.SetActive(true);
        if (_playerController)
        {
            _playerController.IsPaused = true;
        }
    }

    public void ResumeGame()
    {
        pauseMenuUI.SetActive(false);
        if (_playerController)
        {
            _playerController.IsPaused = false;
        }
    }

    public void LeaveGame()
    {
        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.LeaveRoom();
            PhotonNetwork.Disconnect();
        }
        SceneManager.LoadScene("MainMenu");
    }
}