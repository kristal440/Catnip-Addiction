using UnityEngine;

public class MenuBackgroundController : MonoBehaviour
{
    [Header("References")]
    public BackgroundScrollController backgroundScrollController;

    [Header("Ambient Effects")]
    public ParticleSystem[] particleSystems;
    public float particleIntensity = 1f;

    [Header("Transitions")]
    public float transitionSpeed = 1.5f;
    public CanvasGroup menuCanvasGroup;

    private Camera _mainCamera;

    private void Awake()
    {
        _mainCamera = Camera.main;

        if (backgroundScrollController && !backgroundScrollController.cameraTransform)
            if (_mainCamera != null)
                backgroundScrollController.cameraTransform = _mainCamera.transform;

        AdjustParticles();
    }

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

    internal void TransitionToMenu(string menuName)
    {
        if (menuCanvasGroup)
            StartCoroutine(FadeCanvasGroup(menuCanvasGroup, 0f, 1f, transitionSpeed));
    }

    private static System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup group, float start, float end, float duration)
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
