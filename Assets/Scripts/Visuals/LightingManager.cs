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
    [SerializeField] [Range(0f, 1f)] private float shadowSmoothing; // Keep at 0 for pixel art

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
        // Configure global light
        globalLight.intensity = globalLightIntensity;
        globalLight.color = globalLightColor;

        // Set shadow parameters
        globalLight.shadowIntensity = shadowIntensity;
        globalLight.shadowsEnabled = true;

        // Apply quality settings
        ApplyQualitySettings();
    }

    private void ApplyQualitySettings()
    {
        switch (lightQuality)
        {
            case LightQuality.Low:
                globalLight.shadowVolumeIntensity = 0.5f;
                // Set quality based on available properties
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
        }
    }

    // Call this when you need to synchronize lighting for new players over Photon
    public void SyncLightingSettings()
    {
        // You can implement Photon RPC calls here to sync lighting settings
        // For example, send globalLightIntensity, globalLightColor, etc.
    }

    #if UNITY_EDITOR
    // Helper method for adjusting lighting in real-time during development
    private void OnValidate()
    {
        if (globalLight != null)
            ApplyLightSettings();
    }
    #endif
}
