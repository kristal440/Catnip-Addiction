using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <inheritdoc />
/// <summary>
/// Controls frame rate settings and vsync options through the UI.
/// </summary>
public class FPSSettingsController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] [Tooltip("Slider that controls the target frame rate")] private Slider fpsSlider;
    [SerializeField] [Tooltip("Toggle for enabling/disabling vertical sync")] private Toggle vsyncToggle;
    [SerializeField] [Tooltip("Text element that displays the current FPS setting")] private TMP_Text fpsValueText;

    [Header("FPS Settings")]
    [SerializeField] [Tooltip("Minimum allowed frame rate")] private int minFps = 30;
    [SerializeField] [Tooltip("Maximum frame rate when unlimited")] private int maxUnlimitedFps = 1000;

    private const string VsyncEnabledKey = "VSyncEnabled";
    private const string TargetFPSKey = "TargetFPS";

    private int _screenRefreshRate;

    // Initializes UI components and loads saved settings
    private void Start()
    {
        _screenRefreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.numerator /
                                              Screen.currentResolution.refreshRateRatio.denominator);

        vsyncToggle.isOn = QualitySettings.vSyncCount > 0;

        fpsSlider.minValue = minFps;
        UpdateSliderMaxValue();
        fpsSlider.interactable = !vsyncToggle.isOn;

        fpsSlider.value = Mathf.Clamp(Application.targetFrameRate, minFps, fpsSlider.maxValue);
        UpdateFPSText();

        vsyncToggle.onValueChanged.AddListener(OnVSyncToggleChanged);
        fpsSlider.onValueChanged.AddListener(OnFPSSliderChanged);
    }

    // Handles vsync toggle changes and updates related settings
    private void OnVSyncToggleChanged(bool isVSyncOn)
    {
        QualitySettings.vSyncCount = isVSyncOn ? 1 : 0;
        PlayerPrefs.SetInt(VsyncEnabledKey, isVSyncOn ? 1 : 0);
        PlayerPrefs.Save();

        fpsSlider.interactable = !isVSyncOn;
        UpdateSliderMaxValue();
        ApplyFPSSetting();
    }

    // Handles FPS slider value changes
    private void OnFPSSliderChanged(float value)
    {
        ApplyFPSSetting();
    }

    // Updates the slider's maximum value based on vsync setting
    private void UpdateSliderMaxValue()
    {
        fpsSlider.maxValue = vsyncToggle.isOn ? _screenRefreshRate : maxUnlimitedFps;

        if (fpsSlider.value > fpsSlider.maxValue)
            fpsSlider.value = fpsSlider.maxValue;

        UpdateFPSText();
    }

    // Updates the FPS text display to show current value
    private void UpdateFPSText()
    {
        var fps = Mathf.RoundToInt(fpsSlider.value);
        fpsValueText.text = $"{fps} FPS";
    }

    // Applies and saves the selected frame rate setting
    private void ApplyFPSSetting()
    {
        var targetFPS = Mathf.RoundToInt(fpsSlider.value);
        Application.targetFrameRate = targetFPS;

        PlayerPrefs.SetInt(TargetFPSKey, targetFPS);
        PlayerPrefs.Save();

        UpdateFPSText();
    }
}
