using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Handles the pause menu functionality and player disconnection from multiplayer games.
/// </summary>
/// <inheritdoc />
public class PauseMenu : MonoBehaviourPunCallbacks
{
    [SerializeField] [Tooltip("Reference to the pause menu UI GameObject")] public GameObject pauseMenuUI;
    [SerializeField] [Tooltip("Button to pause the game")] private Button pauseButton;
    [SerializeField] [Tooltip("Button to resume the game")] private Button resumeButton;
    [SerializeField] [Tooltip("Button to leave the game")] private Button leaveButton;

    private PlayerController _playerController;

    /// Initializes references and sets initial state
    private void Start()
    {
        if (PhotonNetwork.IsConnected && PhotonNetwork.LocalPlayer != null)
        {
            var playerControllers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            _playerController = playerControllers.FirstOrDefault(static controller => controller.photonView.IsMine);
        }
        pauseMenuUI.SetActive(false);

        if (pauseButton != null)
            pauseButton.onClick.AddListener(PauseGame);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(LeaveGame);
    }

    /// Activates the pause menu and pauses player movement
    public void PauseGame()
    {
        pauseMenuUI.SetActive(true);
        if (_playerController)
            _playerController.IsPaused = true;
    }

    /// Deactivates the pause menu and resumes player movement
    public void ResumeGame()
    {
        pauseMenuUI.SetActive(false);
        if (_playerController)
            _playerController.IsPaused = false;
    }

    /// Handles exiting from the current game
    public void LeaveGame()
    {
        if (PhotonNetwork.IsConnected)
            PhotonNetwork.LeaveRoom();
        else
            SceneManager.LoadScene("MainMenu");
    }

    /// <inheritdoc />
    /// Called when the local player leaves the current room
    public override void OnLeftRoom()
    {
        PhotonNetwork.Disconnect();
    }

    /// <inheritdoc />
    /// Called when disconnected from Photon network
    public override void OnDisconnected(DisconnectCause cause)
    {
        SceneManager.LoadScene("MainMenu");
    }
}
