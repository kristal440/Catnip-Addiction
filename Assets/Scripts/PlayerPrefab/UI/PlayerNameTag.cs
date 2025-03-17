using Photon.Pun;
using TMPro;
using UnityEngine;

public class PlayerNameTag : MonoBehaviourPun
{
    [SerializeField] private TextMeshProUGUI nameTagText;

    private void Start()
    {
        nameTagText.text = photonView.Owner.NickName;
    }
}
