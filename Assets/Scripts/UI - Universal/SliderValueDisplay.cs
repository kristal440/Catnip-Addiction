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
        UpdateValueText(slider.value);

        slider.onValueChanged.AddListener(delegate { UpdateValueText(slider.value); });
    }

    private void UpdateValueText(float value)
    {
        valueText.text = value.ToString("F0");
    }
}