using Photon.Pun;
using TMPro;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Displays the player's nickname above their character in multiplayer.
/// </summary>
public class PlayerNameTag : MonoBehaviourPun
{
    [SerializeField] [Tooltip("Text component for displaying the player's name")] private TextMeshProUGUI nameTagText;

    /// Sets the name tag to the player's network nickname
    private void Start()
    {
        nameTagText.text = photonView.Owner.NickName;
    }
}
