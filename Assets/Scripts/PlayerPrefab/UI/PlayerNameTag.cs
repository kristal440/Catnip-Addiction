using UnityEngine;
using Photon.Pun;
using TMPro;

public class PlayerNameTag : MonoBehaviourPun
{
    [SerializeField] private TextMeshProUGUI nameTagText;

    private void Start()
    {
        if (nameTagText == null)
        {
            Debug.LogError("NameTagText is not assigned in PlayerNameTag script on " + gameObject.name);
            return;
        }

        nameTagText.text = photonView.Owner.NickName;
        Debug.Log("Name tag set to: " + photonView.Owner.NickName);
    }
}
