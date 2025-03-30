using JetBrains.Annotations;
using UnityEngine;

public class AnimatorEvents : MonoBehaviour
{
    [CanBeNull] public PlayerController playerController;

    public void PauseJump()
    {
        // Only pause if still in the air
        if (playerController == null || playerController.IsGrounded) return;

        playerController.animator.speed = 0f;
        playerController.IsJumpPaused = true;
    }

    // gets called when the player is standing
    public void OnStandingAnimationComplete()
    {
        if (playerController == null) return;

        playerController.IsStanding = true;
    }

    // gets called when the player is laying
    public void OnLayingAnimationComplete()
    {
        if (playerController == null) return;

        playerController.IsStanding = false;
    }
}
