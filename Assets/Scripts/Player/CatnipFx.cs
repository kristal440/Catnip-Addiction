using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

public class CatnipFx : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject catnipFxParent;
    [SerializeField] private Image screenMask;
    [SerializeField] private Light2D light2D;

    [Header("Effect Settings")]
    [SerializeField] private float transitionDuration = 1.0f;
    [SerializeField] private float targetLightIntensity = 1.0f;
    [SerializeField] [Range(0, 255)] private int targetMaskTransparency = 75;

    private Coroutine _activeEffectCoroutine;

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
