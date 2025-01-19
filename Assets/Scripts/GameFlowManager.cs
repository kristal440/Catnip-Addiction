using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameFlowManager : MonoBehaviour
{
    [Tooltip("Reference to a UI text or panel that shows waiting status.")]
    public GameObject waitingPanel;

    private bool gameStarted = false;

    private void Start()
    {
        if (waitingPanel != null)
        {
            waitingPanel.SetActive(true);
        }
    }

    private void Update()
    {
        if (!gameStarted && CheckAllPlayersReady())
        {
            StartGame();
        }
    }

    private bool CheckAllPlayersReady()
    {
        // Example: Check if at least 2 players are connected
        return NetworkManager.Singleton.ConnectedClients.Count >= 2;
    }

    private void StartGame()
    {
        gameStarted = true;
        if (waitingPanel != null)
        {
            waitingPanel.SetActive(false);
        }

        Debug.Log("GameFlowManager: Game started!");
    }

    public void EndGame()
    {
        Debug.Log("GameFlowManager: Game ended. Returning to lobby.");
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("MultiplayerMenu", LoadSceneMode.Single);
        }
    }
}