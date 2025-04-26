using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Manages the global 2D lighting settings and quality for the scene.
/// </summary>
/// <inheritdoc />
public class LightingManager : MonoBehaviour
{
    [Header("Global Light Settings")]
    [SerializeField] [Tooltip("Reference to the global 2D light")] private Light2D globalLight;
    [SerializeField] [Range(0f, 1f)] [Tooltip("Controls the brightness of the global light")] private float globalLightIntensity = 0.7f;
    [SerializeField] [Tooltip("Color of the global light")] private Color globalLightColor = Color.white;

    [Header("Shadow Settings")]
    [SerializeField] [Range(0f, 1f)] [Tooltip("Controls the darkness of shadows")] private float shadowIntensity = 0.5f;
    [SerializeField] [Range(0f, 1f)] [Tooltip("Controls the softness of shadow edges")] private float shadowSmoothing;

    [Header("Performance")]
    [SerializeField] [Tooltip("Quality preset for lighting (affects shadow quality)")] private LightQuality lightQuality = LightQuality.Medium;

    private enum LightQuality
    {
        Low,
        Medium,
        High
    }

    // Initializes the global light if not already assigned
    private void Awake()
    {
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

    // Applies configured light settings to the global light
    private void ApplyLightSettings()
    {
        globalLight.intensity = globalLightIntensity;
        globalLight.color = globalLightColor;

        globalLight.shadowIntensity = shadowIntensity;
        globalLight.shadowsEnabled = true;

        ApplyQualitySettings();
    }

    // Sets lighting quality based on the selected preset
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
    // Updates lighting when properties are changed in the inspector
    private void OnValidate()
    {
        if (globalLight != null)
            ApplyLightSettings();
    }
    #endif
}
