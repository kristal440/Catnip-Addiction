using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Controls visual effects when player is under the influence of catnip.
/// </summary>
/// <inheritdoc />
public class CatnipFx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] [Tooltip("Parent object containing all catnip visual effects")] private GameObject catnipFxParent;
    [SerializeField] [Tooltip("UI image used for screen color overlay during catnip effect")] private Image screenMask;
    [SerializeField] [Tooltip("2D light for glow effects during catnip")] private Light2D light2D;

    [Header("Effect Settings")]
    [SerializeField] [Tooltip("Duration of effect transition in seconds")] private float transitionDuration = 1.0f;
    [SerializeField] [Tooltip("Maximum light intensity during catnip effect")] private float targetLightIntensity = 1.0f;
    [SerializeField] [Range(0, 255)] [Tooltip("Maximum transparency of screen mask during effect (0-255)")] private int targetMaskTransparency = 75;

    private Coroutine _activeEffectCoroutine;

    /// Validates and initializes required components
    private void Awake()
    {
        if (catnipFxParent == null)
            Debug.LogError("CatnipFx: CatnipFxParent is not assigned!");

        if (screenMask == null)
            Debug.LogError("CatnipFx: ScreenMask is not assigned!");

        if (light2D == null)
            Debug.LogError("CatnipFx: Light2D is not assigned!");

        if (catnipFxParent != null)
            catnipFxParent.SetActive(false);
    }

    /// Starts the catnip effect transition
    internal void ActivateCatnipEffect()
    {
        if (catnipFxParent == null || screenMask == null || light2D == null)
        {
            Debug.LogError("CatnipFx: Cannot activate effect - missing required components!");
            return;
        }

        if (_activeEffectCoroutine != null)
            StopCoroutine(_activeEffectCoroutine);

        var maskColor = screenMask.color;
        maskColor.a = 0f;
        screenMask.color = maskColor;
        light2D.intensity = 0f;

        catnipFxParent.SetActive(true);

        _activeEffectCoroutine = StartCoroutine(TransitionCatnipEffect(true));
    }

    /// Fades out the catnip effect
    internal void DeactivateCatnipEffect()
    {
        if (catnipFxParent == null || screenMask == null || light2D == null)
        {
            Debug.LogError("CatnipFx: Cannot deactivate effect - missing required components!");
            return;
        }

        if (_activeEffectCoroutine != null)
            StopCoroutine(_activeEffectCoroutine);

        _activeEffectCoroutine = StartCoroutine(TransitionCatnipEffect(false));
    }

    /// Smoothly transitions the catnip effect in or out
    private IEnumerator TransitionCatnipEffect(bool activating)
    {
        var elapsedTime = 0f;

        var startLightIntensity = light2D.intensity;
        var startMaskAlpha = screenMask.color.a;

        var endLightIntensity = activating ? targetLightIntensity : 0f;
        var endMaskAlpha = activating ? targetMaskTransparency / 255f : 0f;

        while (elapsedTime < transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            var t = Mathf.Clamp01(elapsedTime / transitionDuration);

            light2D.intensity = Mathf.Lerp(startLightIntensity, endLightIntensity, t);

            var maskColor = screenMask.color;
            maskColor.a = Mathf.Lerp(startMaskAlpha, endMaskAlpha, t);
            screenMask.color = maskColor;

            yield return null;
        }

        light2D.intensity = endLightIntensity;
        var finalMaskColor = screenMask.color;
        finalMaskColor.a = endMaskAlpha;
        screenMask.color = finalMaskColor;

        if (!activating)
            catnipFxParent.SetActive(false);

        _activeEffectCoroutine = null;
    }
}
