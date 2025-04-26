using Photon.Pun;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Synchronizes player skin selections across the network using Photon.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerSkinSync : MonoBehaviourPunCallbacks
{
    private static readonly int Skin = Animator.StringToHash("Skin");
    [SerializeField] [Tooltip("Reference to the player's animator component")] public Animator animator;
    private static string _currentSkin;

    /// Retrieves the player's skin from network properties on initialization
    private void Awake()
    {
        if (photonView.IsMine && PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue("Skin", out var localSkin))
            _currentSkin = localSkin.ToString();
    }

    /// Updates the skin on local player at start
    private void Start()
    {
        if (photonView.IsMine)
            UpdateSkin(_currentSkin);
    }

    /// Sets the animator parameter based on skin selection
    private void UpdateSkin(string skinName)
    {
        if (string.IsNullOrEmpty(skinName)) return;

        _currentSkin = skinName;
        animator.SetInteger(Skin, GetSkinIndex(skinName));
    }

    /// Converts skin name to integer index for animator parameter
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
