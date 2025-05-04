using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Handles animation events sent from the player animator.
/// </summary>
/// <inheritdoc />
public class AnimatorEvents : MonoBehaviour
{
    [CanBeNull] [SerializeField] [Tooltip("Reference to the player controller component")] public PlayerController playerController;

    /// Called by animation event when standing animation completes
    public void OnStandingAnimationComplete()
    {
        if (playerController == null) return;

        playerController.IsStanding = true;
    }

    /// Called by animation event when laying animation completes
    public void OnLayingAnimationComplete()
    {
        if (playerController == null) return;

        playerController.IsStanding = false;
    }
}
