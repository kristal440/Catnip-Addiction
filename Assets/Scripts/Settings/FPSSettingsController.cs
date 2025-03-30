using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class FPSSettingsController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider fpsSlider;
    [SerializeField] private Toggle vsyncToggle;
    [SerializeField] private TMP_Text fpsValueText;

    [Header("FPS Settings")]
    [SerializeField] private int minFps = 30;
    [SerializeField] private int maxUnlimitedFps = 1000;

    private const string VsyncEnabledKey = "VSyncEnabled";
    private const string TargetFPSKey = "TargetFPS";

    private int _screenRefreshRate;

    private void Start()
    {
        _screenRefreshRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.numerator /
                                              Screen.currentResolution.refreshRateRatio.denominator);

        // Initialize UI based on current settings (set by GameStartup)
        vsyncToggle.isOn = QualitySettings.vSyncCount > 0;

        fpsSlider.minValue = minFps;
        UpdateSliderMaxValue();
        fpsSlider.interactable = !vsyncToggle.isOn;

        fpsSlider.value = Mathf.Clamp(Application.targetFrameRate, minFps, fpsSlider.maxValue);
        UpdateFPSText();

        vsyncToggle.onValueChanged.AddListener(OnVSyncToggleChanged);
        fpsSlider.onValueChanged.AddListener(OnFPSSliderChanged);
    }

    private void OnVSyncToggleChanged(bool isVSyncOn)
    {
        QualitySettings.vSyncCount = isVSyncOn ? 1 : 0;
        PlayerPrefs.SetInt(VsyncEnabledKey, isVSyncOn ? 1 : 0);
        PlayerPrefs.Save();

        fpsSlider.interactable = !isVSyncOn;
        UpdateSliderMaxValue();
        ApplyFPSSetting();
    }

    private void OnFPSSliderChanged(float value)
    {
        ApplyFPSSetting();
    }

    private void UpdateSliderMaxValue()
    {
        fpsSlider.maxValue = vsyncToggle.isOn ? _screenRefreshRate : maxUnlimitedFps;

        if (fpsSlider.value > fpsSlider.maxValue)
            fpsSlider.value = fpsSlider.maxValue;

        UpdateFPSText();
    }

    private void UpdateFPSText()
    {
        var fps = Mathf.RoundToInt(fpsSlider.value);
        fpsValueText.text = $"{fps} FPS";
    }

    private void ApplyFPSSetting()
    {
        var targetFPS = Mathf.RoundToInt(fpsSlider.value);
        Application.targetFrameRate = targetFPS;

        PlayerPrefs.SetInt(TargetFPSKey, targetFPS);
        PlayerPrefs.Save();

        UpdateFPSText();
    }
}
