using Photon.Pun;
using TMPro;
using UnityEngine;

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
    }
}
