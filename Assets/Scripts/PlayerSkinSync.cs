using UnityEngine;
using Photon.Pun;

[RequireComponent(typeof(Animator))]
public class PlayerSkinSync : MonoBehaviourPunCallbacks
{
    private static readonly int Skin = Animator.StringToHash("Skin");
    public Animator animator;
    private static string _currentSkin;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (photonView.IsMine && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Skin", out var localSkin))
            _currentSkin = localSkin.ToString();
    }

    private void Start()
    {
        if (photonView.IsMine)
            UpdateSkin(_currentSkin);
    }

    private void UpdateSkin(string skinName)
    {
        if (string.IsNullOrEmpty(skinName)) return;

        _currentSkin = skinName;
        animator.SetInteger(Skin, GetSkinIndex(skinName));
        Debug.Log($"[PlayerSkinSync] Skin applied: {skinName}\nIs Mine: {photonView.IsMine}");
    }

    private static int GetSkinIndex(string skinName)
    {
        return skinName switch
        {
            "Cat-1" => 0,
            "Cat-2" => 1,
            "Cat-6" => 2,
            _ => 0
        };
    }
}