using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class LightingManager : MonoBehaviour
{
    [Header("Global Light Settings")]
    [SerializeField] private Light2D globalLight;
    [SerializeField] [Range(0f, 1f)] private float globalLightIntensity = 0.7f;
    [SerializeField] private Color globalLightColor = Color.white;

    [Header("Shadow Settings")]
    [SerializeField] [Range(0f, 1f)] private float shadowIntensity = 0.5f;
    [SerializeField] [Range(0f, 1f)] private float shadowSmoothing;

    [Header("Performance")]
    [SerializeField] private LightQuality lightQuality = LightQuality.Medium;

    private enum LightQuality
    {
        Low,
        Medium,
        High
    }

    private void Awake()
    {
        // Set up global light if not assigned
        if (globalLight == null)
        {
            var lightObj = new GameObject("Global Light")
            {
                transform =
                {
                    parent = transform
                }
            };
            globalLight = lightObj.AddComponent<Light2D>();
            globalLight.lightType = Light2D.LightType.Global;
        }

        ApplyLightSettings();
    }

    private void ApplyLightSettings()
    {
        globalLight.intensity = globalLightIntensity;
        globalLight.color = globalLightColor;

        globalLight.shadowIntensity = shadowIntensity;
        globalLight.shadowsEnabled = true;

        ApplyQualitySettings();
    }

    private void ApplyQualitySettings()
    {
        switch (lightQuality)
        {
            case LightQuality.Low:
                globalLight.shadowVolumeIntensity = 0.5f;
                globalLight.shadowsEnabled = true;
                globalLight.shadowIntensity = 0.5f;
                break;
            case LightQuality.Medium:
                globalLight.shadowVolumeIntensity = 0.7f;
                globalLight.shadowsEnabled = true;
                globalLight.shadowIntensity = 0.7f;
                break;
            case LightQuality.High:
                globalLight.shadowVolumeIntensity = 0.9f;
                globalLight.shadowsEnabled = true;
                globalLight.shadowIntensity = 0.9f;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    #if UNITY_EDITOR
    private void OnValidate()
    {
        if (globalLight != null)
            ApplyLightSettings();
    }
    #endif
}
