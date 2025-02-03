using UnityEngine;
using Photon.Pun;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public GameObject pauseMenuUI;
    private bool _isPaused;
    private PlayerController _playerController;

    void Start()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.LocalPlayer != null)
        {
            _playerController = FindFirstObjectByType<PlayerController>();
        }
        pauseMenuUI.SetActive(false);
    }

    public void PauseGame()
    {
        _isPaused = true;
        pauseMenuUI.SetActive(true);
        Time.timeScale = 0f;
        if (_playerController)
        {
            _playerController.IsPaused = true;
        }
    }

    public void ResumeGame()
    {
        _isPaused = false;
        pauseMenuUI.SetActive(false);
        Time.timeScale = 1f;
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
        }
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}