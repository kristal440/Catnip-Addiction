using UnityEngine;
using Photon.Pun;

public class FinishLine : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!GameManager.Instance.gameStarted) return;
        if (!other.CompareTag("Player")) return; // Ensure only players trigger this
        if (!PhotonView.Get(other).IsMine) return; // Ensure only the local player triggers this
        Debug.Log("Player has crossed the finish line!");

        var player = other.GetComponent<PlayerController>();

        if (player == null || !PhotonNetwork.IsConnected || !PhotonNetwork.LocalPlayer.IsLocal) return;
        var finishTime = Time.timeSinceLevelLoad; // Get the time it took the player to finish
        var playerID = PhotonNetwork.LocalPlayer.ActorNumber;

        // Inform GameManager that this player has finished
        GameManager.Instance.PlayerFinished(playerID, finishTime-GameManager.Instance.startTime);

        // Disable player movement upon finishing
        player.HasFinished = true;
        player.SetSpectatorMode(true);

        Debug.Log($"Player {playerID} finished the race in {finishTime:F2} seconds!");
    }
}