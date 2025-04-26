using System.Collections;
using UnityEngine;

/// <inheritdoc />
/// <summary>
/// Controls the menu background elements including particle effects and transitions.
/// </summary>
public class MenuBackgroundController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] [Tooltip("Reference to the scroll controller that handles background movement")] private BackgroundScrollController backgroundScrollController;

    [Header("Ambient Effects")]
    [SerializeField] [Tooltip("Particle systems to adjust based on screen size and intensity")] private ParticleSystem[] particleSystems;
    [SerializeField] [Tooltip("Multiplier that affects particle speed and intensity")] private float particleIntensity = 1f;

    [Header("Transitions")]
    [SerializeField] [Tooltip("Speed at which menu transitions occur")] private float transitionSpeed = 1.5f;
    [SerializeField] [Tooltip("Canvas group to fade during transitions")] private CanvasGroup menuCanvasGroup;

    private Camera _mainCamera;

    /// Initialize references and adjust particles for current screen
    private void Awake()
    {
        _mainCamera = Camera.main;

        if (backgroundScrollController && !backgroundScrollController.cameraTransform)
            if (_mainCamera != null)
                backgroundScrollController.cameraTransform = _mainCamera.transform;

        AdjustParticles();
    }

    /// Scale particle effects based on screen size and intensity setting
    private void AdjustParticles()
    {
        foreach (var system in particleSystems)
        {
            if (!system) continue;

            var main = system.main;
            main.startSpeedMultiplier *= particleIntensity;

            if (!system.TryGetComponent<ParticleSystemRenderer>(out var component)) continue;

            var screenAspect = (float)Screen.width / Screen.height;
            component.pivot = new Vector3(screenAspect * 0.5f, 0.5f, 0);
        }
    }

    /// Handle menu transition with fade effect
    internal void TransitionToMenu(string menuName)
    {
        if (menuCanvasGroup)
            StartCoroutine(FadeCanvasGroup(menuCanvasGroup, 0f, 1f, transitionSpeed));
    }

    /// Smoothly fade a canvas group between alpha values
    private static IEnumerator FadeCanvasGroup(CanvasGroup group, float start, float end, float duration)
    {
        float elapsed = 0;
        while (elapsed < duration)
        {
            group.alpha = Mathf.Lerp(start, end, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        group.alpha = end;
    }
}
