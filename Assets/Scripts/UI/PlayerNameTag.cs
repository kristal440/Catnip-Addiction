using UnityEngine;
using Photon.Pun;
using TMPro; // Import the TextMeshPro namespace

public class PlayerNameTag : MonoBehaviourPun
{
    [SerializeField] private TextMeshProUGUI nameTagText; // Drag your TextMeshPro object here in the Inspector

    private void Start()
    {
        if (nameTagText == null)
        {
            Debug.LogError("NameTagText is not assigned in PlayerNameTag script on " + gameObject.name);
            return;
        }

        // Set the name tag text to the Photon Nickname of the player
        nameTagText.text = photonView.Owner.NickName;
        Debug.Log("Name tag set to: " + photonView.Owner.NickName);
    }
}