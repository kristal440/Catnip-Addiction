using UnityEngine;

namespace MyGameNamespace
{
    /// <summary>
    /// Manages the game logic/state once in the GameScene.
    /// Shows "waiting for players" until conditions are met, then starts gameplay.
    /// </summary>
    public class GameFlowManager : MonoBehaviour
    {
        [Tooltip("Reference to a UI text or panel that shows waiting status.")]
        public GameObject waitingPanel;

        private bool gameStarted = false;

        private void Start()
        {
            // For example, you might check the RoomData associated with this game
            // and see how many players have loaded in.

            // If using Netcode, you might track connected client count,
            // or rely on the RoomManager's data.

            // Example:
            if (waitingPanel != null)
            {
                waitingPanel.SetActive(true);
            }
        }

        private void Update()
        {
            // If the game hasn't started, check if the condition to start is met
            if (!gameStarted)
            {
                // Suppose we require the room to be full,
                // or a certain number of players are connected.
                if (CheckAllPlayersReady())
                {
                    StartGame();
                }
            }
        }

        /// <summary>
        /// Placeholder check for whether all players are ready or room is full.
        /// You can adapt based on your logic in RoomManager/RoomData.
        /// </summary>
        private bool CheckAllPlayersReady()
        {
            // For example, if we just require 4 players (or your desired logic):
            // return (GameNetworkManager.Instance.ConnectedClients.Count == 4);
            // Or if the RoomData for this match is full.
            return true; // Always return true for demonstration
        }

        /// <summary>
        /// Called when the game actually begins.
        /// </summary>
        private void StartGame()
        {
            gameStarted = true;
            if (waitingPanel != null)
            {
                waitingPanel.SetActive(false);
            }

            Debug.Log("GameFlowManager: Game started!");
            // Here you can enable gameplay systems, spawn NPCs, etc.
        }

        /// <summary>
        /// Example method to end the game and go back to the lobby or main menu.
        /// </summary>
        public void EndGame()
        {
            // Return players to the lobby or main menu
            Debug.Log("GameFlowManager: Game ended. Returning to lobby.");
            // e.g., GameNetworkManager.Instance.SwitchToLobbyScene();
        }
    }
}
