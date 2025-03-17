using Photon.Pun;
using System.Linq;
using UnityEngine;
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
        {
            PhotonNetwork.LeaveRoom();
            PhotonNetwork.Disconnect();
        }
        SceneManager.LoadScene("MainMenu");
    }
}
