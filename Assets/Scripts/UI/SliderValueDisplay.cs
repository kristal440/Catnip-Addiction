using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SliderValueDisplay : MonoBehaviour
{
    [Header("UI References")]
    public Slider slider;
    public TMP_Text valueText;

    private void Start()
    {
        // Optional: Initialize the display with the current slider value
        UpdateValueText(slider.value);

        // Add a listener to detect changes on the slider
        slider.onValueChanged.AddListener(delegate { UpdateValueText(slider.value); });
    }

    // This method updates the Text display whenever the slider's value changes
    private void UpdateValueText(float value)
    {
        // Adjust formatting as needed, e.g., two decimal places: ToString("F2")
        valueText.text = value.ToString("F0");
    }
}