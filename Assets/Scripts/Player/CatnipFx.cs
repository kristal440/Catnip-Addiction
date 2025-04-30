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
    [SerializeField] [Tooltip("UI image used for displaying remaining catnip effect time")] private Image chargeBarFill;

    [Header("Effect Settings")]
    [SerializeField] [Tooltip("Duration of effect transition in seconds")] private float transitionDuration = 1.0f;
    [SerializeField] [Tooltip("Maximum light intensity during catnip effect")] private float targetLightIntensity = 1.0f;
    [SerializeField] [Range(0, 255)] [Tooltip("Maximum transparency of screen mask during effect (0-255)")] private int targetMaskTransparency = 75;

    private Coroutine _activeEffectCoroutine;
    private Coroutine _chargeFillCoroutine;

    /// Makes sure to disable all catnip fx
    private void Awake()
    {
        catnipFxParent.SetActive(false);
        chargeBarFill.fillAmount = 0f;
    }

    /// Starts the catnip effect transition
    internal void ActivateCatnipEffect()
    {
        if (_activeEffectCoroutine != null)
            StopCoroutine(_activeEffectCoroutine);

        if (_chargeFillCoroutine != null)
            StopCoroutine(_chargeFillCoroutine);

        var maskColor = screenMask.color;
        maskColor.a = 0f;
        screenMask.color = maskColor;
        light2D.intensity = 0f;
        chargeBarFill.fillAmount = 1f;

        catnipFxParent.SetActive(true);

        _activeEffectCoroutine = StartCoroutine(TransitionCatnipEffect(true));
    }

    /// Fades out the catnip effect
    internal void DeactivateCatnipEffect()
    {
        if (_activeEffectCoroutine != null)
            StopCoroutine(_activeEffectCoroutine);

        if (_chargeFillCoroutine != null)
            StopCoroutine(_chargeFillCoroutine);

        _activeEffectCoroutine = StartCoroutine(TransitionCatnipEffect(false));
    }

    /// Updates the charge bar fill based on the remaining effect time
    internal void UpdateCatnipRemainingTime(float totalDuration, float remainingTime)
    {
        if (!chargeBarFill)
            return;

        chargeBarFill.fillAmount = Mathf.Clamp01(remainingTime / totalDuration);

        // Start fading the screen mask 1 second before effect ends
        if (!(remainingTime <= 1.0f) || !(screenMask.color.a > 0)) return;

        if (_chargeFillCoroutine != null)
            StopCoroutine(_chargeFillCoroutine);

        _chargeFillCoroutine = StartCoroutine(FadeScreenMask(remainingTime));
    }

    /// Fades the screen mask based on the remaining time (last second)
    private IEnumerator FadeScreenMask(float remainingTime)
    {
        var startAlpha = screenMask.color.a;
        var elapsedTime = 0f;

        while (elapsedTime < remainingTime)
        {
            elapsedTime += Time.deltaTime;
            var t = Mathf.Clamp01(elapsedTime / remainingTime);

            var maskColor = screenMask.color;
            maskColor.a = Mathf.Lerp(startAlpha, 0f, t);
            screenMask.color = maskColor;

            yield return null;
        }

        var finalMaskColor = screenMask.color;
        finalMaskColor.a = 0f;
        screenMask.color = finalMaskColor;

        _chargeFillCoroutine = null;
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
        {
            catnipFxParent.SetActive(false);
            chargeBarFill.fillAmount = 0f;
        }

        _activeEffectCoroutine = null;
    }
}
